using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public static class ArchitectureDiagnosticMapper
{
    public static ArchitectureDiagnostic FromViolation(ArchitectureViolation violation)
    {
        if (violation.Payload != null)
        {
            return violation.Payload.ToDiagnostic(violation);
        }

        return new DependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
    }

    public static CycleDiagnostic FromCycle(string path, string contractName, string? contractId)
    {
        return new CycleDiagnostic(contractName, contractId, path);
    }

    public static UnmatchedIgnoreDiagnostic FromUnmatchedIgnore(ArchitectureUnmatchedIgnoredViolation unmatched)
    {
        return new UnmatchedIgnoreDiagnostic(
            unmatched.ContractName, unmatched.ContractId, unmatched.IgnoreIndex,
            unmatched.SourceType, unmatched.ForbiddenReference, unmatched.Reason);
    }
}
