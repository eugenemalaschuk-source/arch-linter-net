namespace ArchLinterNet.Core.Model;

public sealed record ProjectMetadataDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ProjectMetadata;

    public string? ProjectMetadataKind { get; init; }

    public string? ProjectMetadataKey { get; init; }

    public string? ProjectMetadataExpectedValue { get; init; }

    public string? ProjectMetadataActualValue { get; init; }

    public string? ProjectMetadataSourcePath { get; init; }
}
