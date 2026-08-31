namespace ArchLinterNet.Core.Model;

/// <summary>Typed payload used to map metric-budget threshold violations.</summary>
public sealed record MetricBudgetPayload(
    string BudgetId,
    string MetricId,
    string MetricKind,
    string? NativeSubject,
    string EffectiveScope,
    int MeasuredValue,
    string BreachedBound,
    int ConfiguredLimit,
    IReadOnlyCollection<string> Contributors)
    : IArchitectureDiagnosticPayload
{
    public IReadOnlyCollection<string> Contributors { get; init; } = Contributors
        .OrderBy(contributor => contributor, StringComparer.Ordinal)
        .ToArray();

    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new MetricBudgetDiagnostic(
            violation.ContractName,
            violation.ContractId,
            violation.SourceType,
            violation.ForbiddenNamespace,
            violation.ForbiddenReferences,
            BudgetId,
            MetricId,
            MetricKind,
            NativeSubject,
            EffectiveScope,
            MeasuredValue,
            BreachedBound,
            ConfiguredLimit,
            Contributors)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
        };
}
