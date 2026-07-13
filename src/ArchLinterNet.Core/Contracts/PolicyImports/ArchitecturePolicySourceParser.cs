using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal enum ArchitecturePolicySourceRole
{
    Root,
    Fragment
}

internal sealed record ArchitecturePolicySource(
    ArchitecturePolicySourceRole Role,
    string FullPath,
    string PhysicalPath,
    string PortableIdentity,
    YamlMappingNode Root,
    IReadOnlyList<string> Imports);

internal sealed partial class ArchitecturePolicySourceParser
{
    private static readonly HashSet<string> _allowedRootFields = new(StringComparer.Ordinal)
    {
        "version", "name", "imports", "layers", "external_dependencies", "packages",
        "legacy_runtime_layers", "analysis", "contracts", "classification"
    };

    private static readonly HashSet<string> _mergeableFields = new(StringComparer.Ordinal)
    {
        "layers", "external_dependencies", "packages", "legacy_runtime_layers",
        "analysis", "contracts", "classification"
    };

    public bool ContainsImports(string yaml)
    {
        YamlMappingNode? root = ParseMapping(yaml, "root policy", requireMapping: false);
        return root is not null && TryGetChild(root, "imports", out _);
    }

    public ArchitecturePolicySource Parse(
        ArchitecturePolicySourceRole role,
        string fullPath,
        string physicalPath,
        string portableIdentity,
        string yaml)
    {
        YamlMappingNode root = ParseMapping(yaml, portableIdentity, requireMapping: true)!;
        ValidateTopLevelFields(root, role, portableIdentity);
        IReadOnlyList<string> imports = ReadImports(root, portableIdentity);

        if (role == ArchitecturePolicySourceRole.Root)
        {
            RequireField(root, "version", portableIdentity);
            RequireField(root, "name", portableIdentity);
        }
        else
        {
            bool hasContribution = root.Children.Keys
                .OfType<YamlScalarNode>()
                .Any(key => key.Value is not null && _mergeableFields.Contains(key.Value));
            if (!hasContribution && imports.Count == 0)
            {
                throw Shape($"Policy fragment '{portableIdentity}' must contain a mergeable section or imports.");
            }
        }

        return new ArchitecturePolicySource(role, fullPath, physicalPath, portableIdentity, root, imports);
    }

    public void ValidatePortableImport(string importPath, string declaringSource)
    {
        if (string.IsNullOrWhiteSpace(importPath)
            || !importPath.IsNormalized(NormalizationForm.FormC)
            || importPath.StartsWith('~')
            || importPath.StartsWith('/')
            || importPath.Contains('\\')
            || importPath.Contains("${", StringComparison.Ordinal)
            || importPath.Contains("$(", StringComparison.Ordinal)
            || WindowsInterpolationPattern().IsMatch(importPath))
        {
            throw Portable(importPath, declaringSource);
        }

        string[] segments = importPath.Split('/');
        if (segments.Length == 0 || segments.Any(string.IsNullOrEmpty))
        {
            throw Portable(importPath, declaringSource);
        }

        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                continue;
            }

            if (segment.EndsWith('.')
                || segment.EndsWith(' ')
                || segment.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)))
            {
                throw Portable(importPath, declaringSource);
            }

            string basename = segment.Split('.', 2)[0];
            if (ReservedBasenamePattern().IsMatch(basename))
            {
                throw Portable(importPath, declaringSource);
            }
        }
    }

    private static YamlMappingNode? ParseMapping(string yaml, string source, bool requireMapping)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                if (!requireMapping && stream.Documents.Count == 0)
                {
                    return null;
                }

                throw Shape($"Policy source '{source}' must contain exactly one YAML mapping document.");
            }

            return mapping;
        }
        catch (ArchitecturePolicyImportException)
        {
            throw;
        }
        catch (Exception exception) when (
            requireMapping && exception is InvalidOperationException or ArgumentException or YamlException)
        {
            throw new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.SourceShape,
                $"Policy source '{source}' is not a valid mapping document: {exception.Message}");
        }
    }

    private static void ValidateTopLevelFields(
        YamlMappingNode root,
        ArchitecturePolicySourceRole role,
        string source)
    {
        foreach (YamlNode keyNode in root.Children.Keys)
        {
            if (keyNode is not YamlScalarNode { Value: { } key } || !_allowedRootFields.Contains(key))
            {
                string rendered = keyNode is YamlScalarNode scalar ? scalar.Value ?? "<null>" : "<non-scalar>";
                throw Shape($"Policy source '{source}' contains unknown top-level field '{rendered}'.");
            }

            if (role == ArchitecturePolicySourceRole.Fragment && key is "version" or "name")
            {
                throw Shape($"Policy fragment '{source}' cannot declare root-only field '{key}'.");
            }
        }
    }

    private static IReadOnlyList<string> ReadImports(YamlMappingNode root, string source)
    {
        if (!TryGetChild(root, "imports", out YamlNode? node))
        {
            return Array.Empty<string>();
        }

        if (node is not YamlSequenceNode { Children.Count: > 0 } sequence)
        {
            throw Shape($"Policy source '{source}' field 'imports' must be a non-empty sequence.");
        }

        var imports = new List<string>(sequence.Children.Count);
        foreach (YamlNode child in sequence.Children)
        {
            if (child is not YamlScalarNode { Value: { } value } || string.IsNullOrWhiteSpace(value))
            {
                throw Shape($"Policy source '{source}' contains a non-scalar or empty import entry.");
            }

            imports.Add(value);
        }

        return imports;
    }

    private static void RequireField(YamlMappingNode root, string field, string source)
    {
        if (!TryGetChild(root, field, out _))
        {
            throw Shape($"Root policy '{source}' must declare '{field}'.");
        }
    }

    internal static bool TryGetChild(YamlMappingNode mapping, string key, out YamlNode? value)
    {
        foreach ((YamlNode candidateKey, YamlNode candidateValue) in mapping.Children)
        {
            if (candidateKey is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                value = candidateValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static ArchitecturePolicyImportException Portable(string importPath, string declaringSource)
    {
        return new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.PortablePath,
            $"Policy import '{importPath}' declared by '{declaringSource}' is not a portable relative path.");
    }

    private static ArchitecturePolicyImportException Shape(string message)
    {
        return new ArchitecturePolicyImportException(ArchitecturePolicyImportErrorCategory.SourceShape, message);
    }

    [GeneratedRegex("%[^%]+%", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsInterpolationPattern();

    [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedBasenamePattern();
}
