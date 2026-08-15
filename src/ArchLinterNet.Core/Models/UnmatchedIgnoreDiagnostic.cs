namespace ArchLinterNet.Core.Model;

public sealed record UnmatchedIgnoreDiagnostic(
    string ContractName,
    string? ContractId,
    int IgnoreIndex,
    string SourceType,
    string ForbiddenReference,
    string Reason)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.UnmatchedIgnore;
}
