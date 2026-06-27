namespace ArchLinterNet.Core.Model;

public sealed record PolicyConsistencyDiagnostic(
    string ContractName,
    string? ContractId,
    string CheckKind,
    string Reason,
    IReadOnlyCollection<string> ConflictingContractIds,
    IReadOnlyCollection<string> ConflictingContractNames,
    IReadOnlyCollection<string> Layers)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.PolicyConsistency;

    public string? RepresentativeType { get; init; }
}
