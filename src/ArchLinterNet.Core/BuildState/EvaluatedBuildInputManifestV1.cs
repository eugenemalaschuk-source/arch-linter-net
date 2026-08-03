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
        ManifestCollectionBudget budget = new();

        AddFile(project, root, inputs, reasons, budget, cancellationToken);
        if (!budget.IsExhausted)
        {
            AddAncestorImports(projectDirectory, root, inputs, reasons, budget, cancellationToken);
        }
        if (!budget.IsExhausted)
        {
            CollectProjectXml(project, root, inputs, reasons, budget, cancellationToken);
        }

        // Default SDK compile items are deterministic under the project root. Explicit globs are
        // rejected below because static glob semantics can be changed by imports/properties.
        if (!budget.IsExhausted)
        {
            foreach (string source in Directory.EnumerateFiles(projectDirectory, "*.cs", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
                         .Where(path => !IsBuildOutput(path, projectDirectory)))
            {
                AddFile(source, root, inputs, reasons, budget, cancellationToken);
                if (budget.IsExhausted)
                {
                    break;
                }
            }
        }

        AddContextValue("context:configuration", configuration, inputs);
        AddContextValue("context:targetFramework", targetFramework, inputs);
        AddContextValue("context:platform", platform, inputs);
        AddContextValue("context:runtimeIdentifier", runtimeIdentifier, inputs);

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
        ManifestCollectionBudget budget, CancellationToken cancellationToken)
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

        XElement project = document.Root!;
        if (!string.Equals(project.Name.LocalName, "Project", StringComparison.Ordinal))
        {
            reasons.Add("project-xml-uninspectable");
            return;
        }

        string directory = Path.GetDirectoryName(projectPath)!;
        AddOptionalValue("sdk", project.Attribute("Sdk")?.Value, inputs, reasons, budget);
        foreach (XElement element in project.Descendants())
        {
            if (budget.IsExhausted)
            {
                break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            CollectElement(element, directory, root, inputs, reasons, budget, cancellationToken);
        }
    }

    private static void CollectElement(XElement element, string directory, string root, SortedDictionary<string, string> inputs,
        SortedSet<string> reasons, ManifestCollectionBudget budget, CancellationToken cancellationToken)
    {
        string name = element.Name.LocalName;
        if (name is "Import" or "Compile" or "AdditionalFiles" or "EditorConfigFiles" or "Analyzer")
        {
            CollectPathInput(element, name, directory, root, inputs, reasons, budget, cancellationToken);
        }
        else if (name is "PackageReference" or "ProjectReference" or "FrameworkReference" or "Reference")
        {
            CollectReferenceInput(element, name, directory, root, inputs, reasons, budget, cancellationToken);
        }
    }

    private static void CollectPathInput(XElement element, string name, string directory, string root,
        SortedDictionary<string, string> inputs, SortedSet<string> reasons, ManifestCollectionBudget budget,
        CancellationToken cancellationToken)
    {
        string? include = element.Attribute("Include")?.Value ?? element.Attribute("Project")?.Value;
        if (string.IsNullOrWhiteSpace(include) || ContainsDynamicExpression(include))
        {
            reasons.Add($"uninspectable-{name.ToLowerInvariant()}-input");
            return;
        }

        AddFile(Path.GetFullPath(Path.Combine(directory, include)), root, inputs, reasons,
            budget, cancellationToken, name.ToLowerInvariant());
        if (name == "Analyzer") reasons.Add("analyzer-or-generator-identity-unverified");
    }

    private static void CollectReferenceInput(XElement element, string name, string directory, string root,
        SortedDictionary<string, string> inputs, SortedSet<string> reasons, ManifestCollectionBudget budget,
        CancellationToken cancellationToken)
    {
        string identity = element.Attribute("Include")?.Value ?? string.Empty;
        string version = element.Attribute("Version")?.Value ?? element.Element(element.Name.Namespace + "Version")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identity) || ContainsDynamicExpression(identity) || ContainsDynamicExpression(version))
        {
            reasons.Add($"uninspectable-{name.ToLowerInvariant()}-identity");
            return;
        }

        AddOptionalValue($"reference:{name}:{identity}", version, inputs, reasons, budget);
        if (name == "ProjectReference")
        {
            AddFile(Path.GetFullPath(Path.Combine(directory, identity)), root, inputs, reasons,
                budget, cancellationToken, "projectreference");
            return;
        }

        reasons.Add(name switch
        {
            "PackageReference" => "package-reference-identity-unverified",
            "FrameworkReference" => "framework-reference-identity-unverified",
            _ => "assembly-reference-identity-unverified"
        });
    }

    private static void AddAncestorImports(string directory, string root, SortedDictionary<string, string> inputs,
        SortedSet<string> reasons, ManifestCollectionBudget budget, CancellationToken cancellationToken)
    {
        for (DirectoryInfo? current = new(directory); current != null; current = current.Parent)
        {
            if (budget.IsExhausted)
            {
                return;
            }
            foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp", "Directory.Packages.props" })
            {
                if (budget.IsExhausted)
                {
                    return;
                }
                string path = Path.Combine(current.FullName, name);
                if (File.Exists(path))
                {
                    AddFile(path, root, inputs, reasons, budget, cancellationToken);
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
        ManifestCollectionBudget budget, CancellationToken cancellationToken, string? kind = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (budget.IsExhausted)
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

        if (!budget.TryAddFile(file.Length, reasons))
        {
            return;
        }

        string logicalPath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        string digest = BuildStateCanonicalHasher.ComputeContentDigest(fullPath, cancellationToken);
        if (!inputs.TryAdd($"file:{logicalPath}", digest))
        {
            reasons.Add("ambiguous-input-path");
        }
    }

    private static bool ContainsDynamicExpression(string value) =>
        value.Contains("$()", StringComparison.Ordinal) || value.Contains('$')
        || value.Contains("@(", StringComparison.Ordinal) || value.Contains('*') || value.Contains('?');

    private static void AddOptionalValue(string name, string? value, SortedDictionary<string, string> inputs,
        SortedSet<string> reasons, ManifestCollectionBudget budget)
    {
        if (!inputs.ContainsKey(name) && !budget.TryAddOptionalValue(reasons))
        {
            return;
        }

        inputs[name] = value ?? string.Empty;
    }

    private static void AddContextValue(string name, string? value, SortedDictionary<string, string> inputs) =>
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

    private sealed class ManifestCollectionBudget
    {
        public int OptionalInputCount { get; private set; }
        public long CollectedBytes { get; private set; }
        public bool IsExhausted { get; private set; }

        public bool TryAddOptionalValue(SortedSet<string> reasons)
        {
            if (IsExhausted || OptionalInputCount >= MaximumInputs)
            {
                IsExhausted = true;
                reasons.Add("input-limit-exceeded");
                return false;
            }

            OptionalInputCount++;
            if (OptionalInputCount == MaximumInputs)
            {
                IsExhausted = true;
                reasons.Add("input-count-budget-exhausted");
            }

            return true;
        }

        public bool TryAddFile(long length, SortedSet<string> reasons)
        {
            if (!TryAddOptionalValue(reasons))
            {
                return false;
            }

            if (length > MaximumInputBytes - CollectedBytes)
            {
                OptionalInputCount--;
                IsExhausted = true;
                reasons.Add("input-byte-limit-exceeded");
                return false;
            }

            CollectedBytes += length;
            if (CollectedBytes == MaximumInputBytes)
            {
                IsExhausted = true;
                reasons.Add("input-byte-budget-exhausted");
            }

            return true;
        }
    }

    private static bool HasReparsePointAncestor(string path, string root)
    {
        DirectoryInfo? current = new(Path.GetDirectoryName(path)!);
        while (current != null)
        {
            if (!current.Exists)
            {
                current = current.Parent;
                continue;
            }

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
