namespace ArchLinterNet.Core.Model;

public sealed record LayoutConventionDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.LayoutConvention;

    public string? MatchedFilePath { get; init; }
    public string? ExpectedTypeKind { get; init; }
    public string? ActualTypeKind { get; init; }
    public string? ExpectedTypeName { get; init; }
    public string? ActualTypeName { get; init; }
    public string? ExpectedCounterpartName { get; init; }
    public bool DataUnavailable { get; init; }
}
