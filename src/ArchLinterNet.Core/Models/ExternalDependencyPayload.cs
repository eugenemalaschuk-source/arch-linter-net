namespace ArchLinterNet.Core.Model;

public sealed record ExternalDependencyPayload(string ForbiddenExternalGroup) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ExternalDependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, ForbiddenExternalGroup)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
