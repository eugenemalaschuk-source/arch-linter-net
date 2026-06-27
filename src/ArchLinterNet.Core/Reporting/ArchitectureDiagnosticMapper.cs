using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public static class ArchitectureDiagnosticMapper
{
    public static ArchitectureDiagnostic FromViolation(ArchitectureViolation violation)
    {
        if (violation.TemplateName != null || violation.ContainerNamespace != null || violation.DependencyPaths != null)
        {
            return new ConfigurationDiagnostic(
                violation.ContractName, violation.ContractId, violation.SourceType,
                violation.ForbiddenNamespace, violation.ForbiddenReferences)
            {
                TemplateName = violation.TemplateName,
                ContainerNamespace = violation.ContainerNamespace,
                DependencyPaths = violation.DependencyPaths,
                MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
            };
        }

        if (violation.ForbiddenExternalGroup != null)
        {
            return new ExternalDependencyDiagnostic(
                violation.ContractName, violation.ContractId, violation.SourceType,
                violation.ForbiddenNamespace, violation.ForbiddenReferences, violation.ForbiddenExternalGroup)
            {
                MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
            };
        }

        return new DependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            SourceLayer = violation.SourceLayer,
            TargetLayer = violation.TargetLayer,
            AllowedImporters = violation.AllowedImporters,
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
