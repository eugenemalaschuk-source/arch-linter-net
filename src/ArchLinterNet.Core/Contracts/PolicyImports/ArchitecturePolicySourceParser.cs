using System.Text;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal sealed record ArchitecturePolicySource(
    ArchitecturePolicySourceDescriptor Descriptor,
    string FullPath,
    string PhysicalPath,
    string FileIdentity,
    YamlMappingNode Root,
    IReadOnlyList<string> Imports)
{
    public ArchitecturePolicyDocumentRole Role => Descriptor.Role;

    public string PortableIdentity => Descriptor.SourcePath;
}

internal sealed partial class ArchitecturePolicySourceParser
{
    private const string ImportsField = "imports";

    private static readonly HashSet<string> _allowedRootFields = new(StringComparer.Ordinal)
    {
        "version", "name", ImportsField, "layers", "external_dependencies", "packages",
        "legacy_runtime_layers", "analysis", "contracts", "classification"
    };

    private static readonly HashSet<string> _mergeableFields = new(StringComparer.Ordinal)
    {
        "layers", "external_dependencies", "packages", "legacy_runtime_layers",
        "analysis", "contracts", "classification"
    };

    public static bool ContainsImports(string yaml, ArchitecturePolicySourceDescriptor descriptor)
    {
        YamlMappingNode root;
        try
        {
            root = ParseMapping(yaml, descriptor.SourcePath, requireMapping: true)!;
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw ArchitecturePolicyDiagnosticFactory.Enrich(
                exception,
                ArchitecturePolicyDiagnosticFactory.Location(descriptor));
        }

        return TryGetChild(root, ImportsField, out _);
    }

    public ArchitecturePolicySource Parse(
        ArchitecturePolicySourceDescriptor descriptor,
        string fullPath,
        string physicalPath,
        string fileIdentity,
        string yaml)
    {
        YamlMappingNode root;
        try
        {
            root = ParseMapping(yaml, descriptor.SourcePath, requireMapping: true)!;
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw ArchitecturePolicyDiagnosticFactory.Enrich(
                exception,
                ArchitecturePolicyDiagnosticFactory.Location(descriptor));
        }

        ValidateTopLevelFields(root, descriptor);
        IReadOnlyList<string> imports = ReadImports(root, descriptor);

        if (descriptor.Role == ArchitecturePolicyDocumentRole.Root)
        {
            RequireField(root, "version", descriptor);
            RequireField(root, "name", descriptor);
        }
        else
        {
            bool hasContribution = root.Children.Keys
                .OfType<YamlScalarNode>()
                .Any(key => key.Value is not null && _mergeableFields.Contains(key.Value));
            if (!hasContribution && imports.Count == 0)
            {
                throw Shape(
                    $"Policy fragment '{descriptor.SourcePath}' must contain a mergeable section or imports.",
                    descriptor,
                    "$",
                    root);
            }
        }

        return new ArchitecturePolicySource(descriptor, fullPath, physicalPath, fileIdentity, root, imports);
    }

    public static void ValidatePortableImport(
        string importPath,
        ArchitecturePolicySource declaringSource,
        int importIndex)
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
            throw Portable(importPath, declaringSource, importIndex);
        }

        string[] segments = importPath.Split('/');
        if (segments.Length == 0 || segments.Any(string.IsNullOrEmpty))
        {
            throw Portable(importPath, declaringSource, importIndex);
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
                throw Portable(importPath, declaringSource, importIndex);
            }

            string basename = segment.Split('.', 2)[0];
            if (ReservedBasenamePattern().IsMatch(basename))
            {
                throw Portable(importPath, declaringSource, importIndex);
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
        ArchitecturePolicySourceDescriptor descriptor)
    {
        foreach (YamlNode keyNode in root.Children.Keys)
        {
            if (keyNode is not YamlScalarNode { Value: { } key } || !_allowedRootFields.Contains(key))
            {
                string rendered = keyNode is YamlScalarNode scalar ? scalar.Value ?? "<null>" : "<non-scalar>";
                throw Shape(
                    $"Policy source '{descriptor.SourcePath}' contains unknown top-level field '{rendered}'.",
                    descriptor,
                    rendered,
                    keyNode);
            }

            if (descriptor.Role == ArchitecturePolicyDocumentRole.Fragment && key is "version" or "name")
            {
                throw Shape(
                    $"Policy fragment '{descriptor.SourcePath}' cannot declare root-only field '{key}'.",
                    descriptor,
                    key,
                    keyNode);
            }
        }
    }

    private static IReadOnlyList<string> ReadImports(
        YamlMappingNode root,
        ArchitecturePolicySourceDescriptor descriptor)
    {
        if (!TryGetChild(root, ImportsField, out YamlNode? node))
        {
            return Array.Empty<string>();
        }

        if (node is not YamlSequenceNode { Children.Count: > 0 } sequence)
        {
            throw Shape(
                $"Policy source '{descriptor.SourcePath}' field '{ImportsField}' must be a non-empty sequence.",
                descriptor,
                ImportsField,
                node!);
        }

        var imports = new List<string>(sequence.Children.Count);
        for (int index = 0; index < sequence.Children.Count; index++)
        {
            YamlNode child = sequence.Children[index];
            if (child is not YamlScalarNode { Value: { } value } || string.IsNullOrWhiteSpace(value))
            {
                throw Shape(
                    $"Policy source '{descriptor.SourcePath}' contains a non-scalar or empty import entry.",
                    descriptor,
                    $"{ImportsField}[{index}]",
                    child);
            }

            imports.Add(value);
        }

        return imports;
    }

    private static void RequireField(
        YamlMappingNode root,
        string field,
        ArchitecturePolicySourceDescriptor descriptor)
    {
        if (!TryGetChild(root, field, out _))
        {
            throw Shape(
                $"Root policy '{descriptor.SourcePath}' must declare '{field}'.",
                descriptor,
                "$",
                root);
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

    private static ArchitecturePolicyImportException Portable(
        string importPath,
        ArchitecturePolicySource declaringSource,
        int importIndex)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.PortablePath,
            $"Policy import '{importPath}' declared by '{declaringSource.PortableIdentity}' is not a portable relative path.",
            ArchitecturePolicyDiagnosticFactory.ImportLocation(declaringSource, importIndex),
            importChain: declaringSource.Descriptor.ImportChain.Append(importPath));
    }

    private static ArchitecturePolicyImportException Shape(string message)
    {
        return new ArchitecturePolicyImportException(ArchitecturePolicyImportErrorCategory.SourceShape, message);
    }

    private static ArchitecturePolicyImportException Shape(
        string message,
        ArchitecturePolicySourceDescriptor descriptor,
        string yamlPath,
        YamlNode node)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            message,
            ArchitecturePolicyDiagnosticFactory.Location(descriptor, yamlPath, node));
    }

    [GeneratedRegex("%[^%]+%", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsInterpolationPattern();

    [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9\\x00B9\\x00B2\\x00B3]|LPT[1-9\\x00B9\\x00B2\\x00B3])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedBasenamePattern();
}
