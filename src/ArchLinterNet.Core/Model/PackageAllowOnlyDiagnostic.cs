namespace ArchLinterNet.Core.Model;

public sealed record PackageAllowOnlyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences,
    IReadOnlyCollection<string> AllowedPackageGroups)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.PackageAllowOnly;
}
