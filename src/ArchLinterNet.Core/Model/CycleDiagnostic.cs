namespace ArchLinterNet.Core.Model;

public sealed record CycleDiagnostic(string ContractName, string? ContractId, string Path)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Cycle;
}
