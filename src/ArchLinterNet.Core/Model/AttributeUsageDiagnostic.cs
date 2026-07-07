namespace ArchLinterNet.Core.Model;

public sealed record AttributeUsageDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.AttributeUsage;

    public string? MatchedAttribute { get; init; }
    public string? AttributeUsageKind { get; init; }
    public string? ExpectedAttributeLocation { get; init; }
    public string? ActualAttributeLocation { get; init; }
}
