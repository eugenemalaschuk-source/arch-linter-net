namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferenceAllowOnlyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences,
    IReadOnlyCollection<string> AllowedFrameworkGroups,
    IReadOnlyCollection<FrameworkReferenceEvidence> Evidence)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.FrameworkReferenceAllowOnly;
}
