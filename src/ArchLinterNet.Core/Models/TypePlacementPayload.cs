namespace ArchLinterNet.Core.Model;

public sealed record TypePlacementPayload(
    string? ExpectedTypeLocation = null,
    string? ActualTypeLocation = null,
    string? ExpectedTypeName = null,
    string? ActualTypeName = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new TypePlacementDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            ExpectedTypeLocation = ExpectedTypeLocation,
            ActualTypeLocation = ActualTypeLocation,
            ExpectedTypeName = ExpectedTypeName,
            ActualTypeName = ActualTypeName
        };
}
