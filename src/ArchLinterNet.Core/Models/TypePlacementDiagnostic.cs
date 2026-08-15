namespace ArchLinterNet.Core.Model;

public sealed record TypePlacementDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.TypePlacement;

    public string? ExpectedTypeLocation { get; init; }
    public string? ActualTypeLocation { get; init; }
    public string? ExpectedTypeName { get; init; }
    public string? ActualTypeName { get; init; }
}
