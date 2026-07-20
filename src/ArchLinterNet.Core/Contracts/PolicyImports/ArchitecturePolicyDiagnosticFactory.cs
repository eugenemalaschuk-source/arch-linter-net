using ArchLinterNet.Core.Model;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicyDiagnosticFactory
{
    public static ArchitecturePolicySourceLocation Location(
        ArchitecturePolicySource source,
        string yamlPath,
        YamlNode? node = null,
        string? contractFamily = null,
        string? contractId = null)
    {
        YamlNode marker = node ?? source.Root;
        return new ArchitecturePolicySourceLocation(
            source.Descriptor,
            yamlPath,
            checked((int)Math.Max(1, marker.Start.Line + 1)),
            checked((int)Math.Max(1, marker.Start.Column + 1)),
            contractFamily,
            contractId);
    }

    public static ArchitecturePolicySourceLocation Location(
        ArchitecturePolicySourceDescriptor descriptor,
        string yamlPath = "$",
        YamlNode? node = null)
    {
        return new ArchitecturePolicySourceLocation(
            descriptor,
            yamlPath,
            node is null ? 1 : checked((int)Math.Max(1, node.Start.Line + 1)),
            node is null ? 1 : checked((int)Math.Max(1, node.Start.Column + 1)),
            null,
            null);
    }

    public static ArchitecturePolicySourceLocation ImportLocation(
        ArchitecturePolicySource source,
        int importIndex)
    {
        if (ArchitecturePolicySourceParser.TryGetChild(source.Root, "imports", out YamlNode? imports)
            && imports is YamlSequenceNode sequence
            && importIndex >= 0
            && importIndex < sequence.Children.Count)
        {
            return Location(source, $"imports[{importIndex}]", sequence.Children[importIndex]);
        }

        return Location(source, "imports");
    }

    public static ArchitecturePolicyImportException Exception(
        ArchitecturePolicyImportErrorCategory category,
        string message,
        ArchitecturePolicySourceLocation? location,
        IEnumerable<ArchitecturePolicySourceLocation>? relatedLocations = null,
        IEnumerable<string>? importChain = null)
    {
        var diagnostic = new ArchitecturePolicyDiagnostic(
            KindFor(category),
            location,
            relatedLocations?.ToArray() ?? Array.Empty<ArchitecturePolicySourceLocation>(),
            importChain?.ToArray() ?? location?.Source.ImportChain ?? Array.Empty<string>());
        return new ArchitecturePolicyImportException(category, message, diagnostic);
    }

    public static ArchitecturePolicyImportException Enrich(
        ArchitecturePolicyImportException exception,
        ArchitecturePolicySourceLocation location,
        IEnumerable<string>? importChain = null)
    {
        if (exception.Diagnostic is not null)
        {
            return exception;
        }

        return Exception(exception.Category, exception.Message, location, importChain: importChain);
    }

    public static ArchitecturePolicyImportException EnrichRoot(
        ArchitecturePolicyImportException exception,
        ArchitecturePolicySourceDescriptor root)
    {
        return Exception(
            exception.Category,
            RootMessage(exception, root.SourcePath),
            Location(root));
    }

    private static string RootMessage(ArchitecturePolicyImportException exception, string sourcePath)
    {
        return exception.Category switch
        {
            ArchitecturePolicyImportErrorCategory.MissingFile => $"Root policy file not found: {sourcePath}",
            ArchitecturePolicyImportErrorCategory.SourceShape =>
                $"Root policy '{sourcePath}' must resolve to a readable regular file.",
            ArchitecturePolicyImportErrorCategory.UnreadableFile =>
                $"Root policy '{sourcePath}' is not readable.",
            ArchitecturePolicyImportErrorCategory.PlatformFailure =>
                $"Root policy '{sourcePath}' could not be inspected ({NativeErrorContext(exception.Message)}).",
            _ => $"Root policy '{sourcePath}' could not be resolved."
        };
    }

    private static string NativeErrorContext(string message)
    {
        int start = message.LastIndexOf('(');
        return start >= 0 && message.EndsWith(')')
            ? message[(start + 1)..^1]
            : "native error";
    }

    private static ArchitecturePolicyDiagnosticKind KindFor(ArchitecturePolicyImportErrorCategory category)
    {
        return category switch
        {
            ArchitecturePolicyImportErrorCategory.SourceShape => ArchitecturePolicyDiagnosticKind.SourceShape,
            ArchitecturePolicyImportErrorCategory.CompositionConflict =>
                ArchitecturePolicyDiagnosticKind.CompositionConflict,
            _ => ArchitecturePolicyDiagnosticKind.ImportResolution
        };
    }
}
