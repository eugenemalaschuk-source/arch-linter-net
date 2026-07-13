using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public static class ArchitectureDiagnosticMapper
{
    public static ArchitectureDiagnostic FromViolation(ArchitectureViolation violation)
    {
        ArchitectureDiagnostic diagnostic;
        if (violation.Payload != null)
        {
            diagnostic = violation.Payload.ToDiagnostic(violation);
        }
        else
        {
            diagnostic = new DependencyDiagnostic(
                violation.ContractName, violation.ContractId, violation.SourceType,
                violation.ForbiddenNamespace, violation.ForbiddenReferences)
            {
                MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
            };
        }

        return diagnostic with
        {
            PolicyLocation = violation.PolicyLocation,
            RelatedPolicyLocations = violation.RelatedPolicyLocations
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
            unmatched.SourceType, unmatched.ForbiddenReference, unmatched.Reason)
        {
            PolicyLocation = unmatched.PolicyLocation
        };
    }
}
