namespace ArchLinterNet.Core.Model;

public sealed record ContextAllowOnlyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ContextAllowOnly;

    public string? SourceRole { get; init; }
    public IReadOnlyDictionary<string, object>? SourceMetadata { get; init; }
    public string? TargetRole { get; init; }
    public IReadOnlyDictionary<string, object>? TargetMetadata { get; init; }

    // Always "none": an allow-only violation is a target matching no allowed selector, so unlike the
    // dependency family's "forbidden" match evidence, there is no specific selector to name — "none"
    // is itself the stable, structured evidence that every allowed selector was checked and none
    // matched, present in both JSON and human output (distinct from a missing/omitted field).
    public string? MatchedSelector { get; init; }
}
