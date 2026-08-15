namespace ArchLinterNet.Core.Model;

public sealed record PortBoundaryDiagnostic(
    string ContractName, string? ContractId, string SourceType,
    string ForbiddenNamespace, IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.PortBoundary;
    public string? SourceRole { get; init; }
    public IReadOnlyDictionary<string, object>? SourceMetadata { get; init; }
    public string? TargetRole { get; init; }
    public IReadOnlyDictionary<string, object>? TargetMetadata { get; init; }
    public string? EvidenceKind { get; init; }
    public string? ExpectedSeam { get; init; }
    public string? RemediationHint { get; init; }
}
