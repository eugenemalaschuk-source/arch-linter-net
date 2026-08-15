namespace ArchLinterNet.Core.Model;

public sealed record PackageAllowOnlyPayload(IReadOnlyCollection<string> AllowedPackageGroups) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new PackageAllowOnlyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, AllowedPackageGroups)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
