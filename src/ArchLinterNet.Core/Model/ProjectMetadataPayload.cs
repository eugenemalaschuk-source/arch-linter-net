namespace ArchLinterNet.Core.Model;

public sealed record ProjectMetadataPayload(
    string? ProjectMetadataKind = null,
    string? ProjectMetadataKey = null,
    string? ProjectMetadataExpectedValue = null,
    string? ProjectMetadataActualValue = null,
    string? ProjectMetadataSourcePath = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ProjectMetadataDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            ProjectMetadataKind = ProjectMetadataKind,
            ProjectMetadataKey = ProjectMetadataKey,
            ProjectMetadataExpectedValue = ProjectMetadataExpectedValue,
            ProjectMetadataActualValue = ProjectMetadataActualValue,
            ProjectMetadataSourcePath = ProjectMetadataSourcePath
        };
}
