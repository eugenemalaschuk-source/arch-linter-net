namespace ArchLinterNet.Core.Model;

public sealed record DependencyPayload(
    string? SourceLayer = null,
    string? TargetLayer = null,
    IReadOnlyCollection<string>? AllowedImporters = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new DependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            SourceLayer = SourceLayer,
            TargetLayer = TargetLayer,
            AllowedImporters = AllowedImporters
        };
}
