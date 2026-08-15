namespace ArchLinterNet.Core.Model;

public sealed record BaselineLifecycleDiagnostic(
    string ContractName,
    string? ContractId,
    string ContractGroup,
    string SourceType,
    string ForbiddenReference,
    string? Reason,
    string? Issue,
    BaselineEntryDisposition Disposition,
    bool Suppresses,
    ArchitectureViolationIdentity? StructuredIdentity)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Baseline;
}
