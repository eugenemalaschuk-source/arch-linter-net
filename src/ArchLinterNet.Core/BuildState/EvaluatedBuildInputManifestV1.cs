using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ArchLinterNet.Core.BuildState;

public enum CacheEligibility
{
    VerifiedCacheEligible,
    CacheIneligible
}

public sealed record EvaluatedBuildInputManifestV1(
    string Digest,
    CacheEligibility Eligibility,
    IReadOnlyList<string> IneligibilityReasons,
    IReadOnlyList<string> Inputs);

// A deliberately static and fail-closed collector. It must not execute MSBuild while inspecting
// untrusted repository content: inputs it cannot identify become cache-ineligible instead.
public static class EvaluatedBuildInputManifestCollector
{
    private const int MaximumInputs = 10_000;
    private const long MaximumInputBytes = 64L * 1024 * 1024;

    public static EvaluatedBuildInputManifestV1 Collect(
        string projectPath,
        string repositoryRoot,
        string? configuration = null,
        string? targetFramework = null,
        string? platform = null,
        string? runtimeIdentifier = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string project = BuildStatePathResolution.ResolveAbsoluteProjectPath(root, projectPath);
        string projectDirectory = Path.GetDirectoryName(project)
            ?? throw new InvalidOperationException($"Cannot determine project directory for '{projectPath}'.");
        SortedDictionary<string, string> inputs = new(StringComparer.Ordinal);
        SortedSet<string> reasons = new(StringComparer.Ordinal);
        long collectedBytes = 0;

        AddFile(project, root, inputs, reasons, ref collectedBytes, cancellationToken);
        AddAncestorImports(projectDirectory, root, inputs, reasons, ref collectedBytes, cancellationToken);
        CollectProjectXml(project, root, inputs, reasons, ref collectedBytes, cancellationToken);

        // Default SDK compile items are deterministic under the project root. Explicit globs are
        // rejected below because static glob semantics can be changed by imports/properties.
        foreach (string source in Directory.EnumerateFiles(projectDirectory, "*.cs", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        })
                     .Where(path => !IsBuildOutput(path, projectDirectory)))
        {
            AddFile(source, root, inputs, reasons, ref collectedBytes, cancellationToken);
            if (IsBudgetExhausted(reasons))
            {
                break;
            }
        }

        AddValue("context:configuration", configuration, inputs);
        AddValue("context:targetFramework", targetFramework, inputs);
        AddValue("context:platform", platform, inputs);
        AddValue("context:runtimeIdentifier", runtimeIdentifier, inputs);

        // Static XML inspection cannot prove evaluated SDK imports, global properties,
        // generators, framework packs, or compiler task inputs. It is evidence for stale
        // diagnostics only, never authorization for a persistent cache.
        reasons.Add("evaluated-msbuild-evidence-incomplete");

        string canonical = string.Join('\n', inputs.Select(entry => $"{entry.Key}:{entry.Value}"));
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "analysis-build-state/v1\nmanifest\n" + canonical)));
        return new EvaluatedBuildInputManifestV1(
            digest,
            reasons.Count == 0 ? CacheEligibility.VerifiedCacheEligible : CacheEligibility.CacheIneligible,
            reasons.ToArray(),
            inputs.Select(entry => entry.Key).ToArray());
    }

    private static void CollectProjectXml(
        string projectPath, string root, SortedDictionary<string, string> inputs, SortedSet<string> reasons,
        ref long collectedBytes, CancellationToken cancellationToken)
    {
        XDocument document;
        try
        {
            using XmlReader reader = XmlReader.Create(projectPath, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            reasons.Add("project-xml-uninspectable");
            return;
        }

        XElement? project = document.Root;
        if (project == null || !string.Equals(project.Name.LocalName, "Project", StringComparison.Ordinal))
        {
            reasons.Add("project-xml-uninspectable");
            return;
        }

        string directory = Path.GetDirectoryName(projectPath)!;
        AddValue("sdk", project.Attribute("Sdk")?.Value, inputs);
        foreach (XElement element in project.Descendants())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = element.Name.LocalName;
            string? include = element.Attribute("Include")?.Value ?? element.Attribute("Project")?.Value;
            if (name is "Import" or "Compile" or "AdditionalFiles" or "EditorConfigFiles" or "Analyzer")
            {
                if (string.IsNullOrWhiteSpace(include) || ContainsDynamicExpression(include))
                {
                    reasons.Add($"uninspectable-{name.ToLowerInvariant()}-input");
                    continue;
                }

                string candidate = Path.GetFullPath(Path.Combine(directory, include));
                AddFile(candidate, root, inputs, reasons, ref collectedBytes, cancellationToken, name.ToLowerInvariant());
                if (name == "Analyzer")
                {
                    // A path digest proves a local analyzer but not an arbitrary package-selected
                    // analyzer/generator graph; no cache can reuse it without that evidence.
                    reasons.Add("analyzer-or-generator-identity-unverified");
                }
            }
            else if (name is "PackageReference" or "ProjectReference" or "FrameworkReference" or "Reference")
            {
                string identity = element.Attribute("Include")?.Value ?? string.Empty;
                string version = element.Attribute("Version")?.Value ?? element.Element(element.Name.Namespace + "Version")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(identity) || ContainsDynamicExpression(identity) || ContainsDynamicExpression(version))
                {
                    reasons.Add($"uninspectable-{name.ToLowerInvariant()}-identity");
                }
                else
                {
                    AddValue($"reference:{name}:{identity}", version, inputs);
                    if (name == "ProjectReference")
                    {
                        AddFile(Path.GetFullPath(Path.Combine(directory, identity)), root, inputs, reasons,
                            ref collectedBytes, cancellationToken, "projectreference");
                    }
                    else if (name == "PackageReference")
                    {
                        reasons.Add("package-reference-identity-unverified");
                    }
                    else if (name == "FrameworkReference")
                    {
                        reasons.Add("framework-reference-identity-unverified");
                    }
                    else
                    {
                        reasons.Add("assembly-reference-identity-unverified");
                    }
                }
            }
        }
    }

    private static void AddAncestorImports(string directory, string root, SortedDictionary<string, string> inputs,
        SortedSet<string> reasons, ref long collectedBytes, CancellationToken cancellationToken)
    {
        for (DirectoryInfo? current = new(directory); current != null; current = current.Parent)
        {
            foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp", "Directory.Packages.props" })
            {
                string path = Path.Combine(current.FullName, name);
                if (File.Exists(path))
                {
                    AddFile(path, root, inputs, reasons, ref collectedBytes, cancellationToken);
                }
            }

            if (PathsEqual(current.FullName, root))
            {
                return;
            }
        }

        reasons.Add("project-outside-repository");
    }

    private static void AddFile(string path, string root, SortedDictionary<string, string> inputs, SortedSet<string> reasons,
        ref long collectedBytes, CancellationToken cancellationToken, string? kind = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsBudgetExhausted(reasons))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (!IsContained(fullPath, root))
        {
            reasons.Add("repository-escape");
            return;
        }

        if (HasReparsePointAncestor(fullPath, root))
        {
            reasons.Add("symlink-input-unverified");
            return;
        }

        if (!File.Exists(fullPath))
        {
            reasons.Add(kind == null ? "missing-input" : $"missing-{kind}-input");
            return;
        }

        FileInfo file = new(fullPath);
        if (file.LinkTarget != null || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            reasons.Add("symlink-input-unverified");
            return;
        }

        if (inputs.Count >= MaximumInputs)
        {
            reasons.Add("input-limit-exceeded");
            return;
        }

        if (file.Length > MaximumInputBytes - collectedBytes)
        {
            reasons.Add("input-byte-limit-exceeded");
            return;
        }

        string logicalPath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        string digest = BuildStateCanonicalHasher.ComputeContentDigest(fullPath, cancellationToken);
        collectedBytes += file.Length;
        if (!inputs.TryAdd($"file:{logicalPath}", digest))
        {
            reasons.Add("ambiguous-input-path");
        }
    }

    private static bool ContainsDynamicExpression(string value) =>
        value.Contains("$()", StringComparison.Ordinal) || value.Contains("$", StringComparison.Ordinal)
        || value.Contains("@(", StringComparison.Ordinal) || value.Contains('*') || value.Contains('?');

    private static void AddValue(string name, string? value, SortedDictionary<string, string> inputs) =>
        inputs[name] = value ?? string.Empty;

    private static bool IsBuildOutput(string path, string projectDirectory)
    {
        string relative = Path.GetRelativePath(projectDirectory, path);
        return relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContained(string path, string root) =>
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || PathsEqual(path, root);

    private static bool IsBudgetExhausted(IReadOnlySet<string> reasons) =>
        reasons.Contains("input-limit-exceeded") || reasons.Contains("input-byte-limit-exceeded");

    private static bool HasReparsePointAncestor(string path, string root)
    {
        DirectoryInfo? current = new(Path.GetDirectoryName(path)!);
        while (current != null)
        {
            if (current.LinkTarget != null || (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            if (PathsEqual(current.FullName, root))
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.Ordinal);
}
