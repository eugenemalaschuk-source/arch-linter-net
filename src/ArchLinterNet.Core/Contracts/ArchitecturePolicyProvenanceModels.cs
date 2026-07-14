namespace ArchLinterNet.Core.Model;

public enum ArchitecturePolicyDocumentRole
{
    Root,
    Fragment
}

public sealed record ArchitecturePolicySourceDescriptor(
    string RootPath,
    string SourcePath,
    ArchitecturePolicyDocumentRole Role,
    int SourceOrdinal,
    string? DeclaringSourcePath,
    string? AuthoredImportPath,
    IReadOnlyList<string> ImportChain);

public sealed record ArchitecturePolicySourceLocation(
    ArchitecturePolicySourceDescriptor Source,
    string YamlPath,
    int Line,
    int Column,
    string? ContractFamily,
    string? ContractId,
    int EncounterOrdinal = 0)
{
    public string RootPath => Source.RootPath;

    public string SourcePath => Source.SourcePath;

    public ArchitecturePolicyDocumentRole Role => Source.Role;

    public int SourceOrdinal => Source.SourceOrdinal;
}

public enum ArchitecturePolicyDiagnosticKind
{
    ImportResolution,
    SourceShape,
    CompositionConflict,
    SemanticValidation
}

public sealed record ArchitecturePolicyDiagnostic(
    ArchitecturePolicyDiagnosticKind Kind,
    ArchitecturePolicySourceLocation? Location,
    IReadOnlyList<ArchitecturePolicySourceLocation> RelatedLocations,
    IReadOnlyList<string> ImportChain);

public sealed class ArchitecturePolicyValidationException : InvalidOperationException
{
    public ArchitecturePolicyValidationException(
        string message,
        ArchitecturePolicyDiagnostic diagnostic,
        Exception innerException)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }

    public ArchitecturePolicyDiagnostic Diagnostic { get; }
}
