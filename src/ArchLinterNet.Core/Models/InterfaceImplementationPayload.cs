namespace ArchLinterNet.Core.Model;

public sealed record InterfaceImplementationPayload(
    string? MatchedInterface = null,
    string? ImplementationKind = null,
    string? ExpectedImplementationLocation = null,
    string? ActualImplementationLocation = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new InterfaceImplementationDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            MatchedInterface = MatchedInterface,
            ImplementationKind = ImplementationKind,
            ExpectedImplementationLocation = ExpectedImplementationLocation,
            ActualImplementationLocation = ActualImplementationLocation
        };
}
