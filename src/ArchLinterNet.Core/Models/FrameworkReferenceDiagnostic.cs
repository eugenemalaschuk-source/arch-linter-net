namespace ArchLinterNet.Core.Model;

public sealed record FrameworkReferenceDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences,
    string ForbiddenFrameworkGroup,
    IReadOnlyCollection<FrameworkReferenceEvidence> Evidence)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.FrameworkReference;
}
