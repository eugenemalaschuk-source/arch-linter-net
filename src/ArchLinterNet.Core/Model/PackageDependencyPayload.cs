namespace ArchLinterNet.Core.Model;

public sealed record PackageDependencyPayload(string ForbiddenPackageGroup) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new PackageDependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, ForbiddenPackageGroup)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
