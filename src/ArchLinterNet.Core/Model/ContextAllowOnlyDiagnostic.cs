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

    // Always null: an allow-only violation is a target matching no allowed selector, so there is no
    // specific selector to name (distinct from the dependency family's "forbidden" match evidence).
    // Field kept for structural parity with ContextDependencyDiagnostic.
    public string? MatchedSelector { get; init; }
}
