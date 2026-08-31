namespace ArchLinterNet.Core.Model;

/// <summary>Structured evidence for one absolute metric-budget threshold breach.</summary>
public sealed record MetricBudgetDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences,
    string BudgetId,
    string MetricId,
    string MetricKind,
    string? NativeSubject,
    string EffectiveScope,
    int MeasuredValue,
    string BreachedBound,
    int ConfiguredLimit,
    IReadOnlyCollection<string> Contributors)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.MetricBudget;

    public IReadOnlyCollection<string> Contributors { get; init; } = Contributors
        .OrderBy(contributor => contributor, StringComparer.Ordinal)
        .ToArray();

    public int ContributorCount => Contributors.Count;
}
