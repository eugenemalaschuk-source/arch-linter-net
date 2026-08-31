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
    /// <summary>Relative mode, when this is a baseline-relative budget finding.</summary>
    public string? BaselineMode { get; init; }

    /// <summary>Reviewed scalar metric value used for a relative comparison.</summary>
    public int? BaselineValue { get; init; }

    /// <summary>Current metric value minus the reviewed baseline value.</summary>
    public int? Delta { get; init; }

    /// <summary>Allowed increase over the reviewed value.</summary>
    public int? AllowedDelta { get; init; }

    /// <summary>Effective upper threshold after applying the relative allowance and cap.</summary>
    public long? EffectiveThreshold { get; init; }

    /// <summary>Optional configured absolute maximum cap.</summary>
    public int? AbsoluteCap { get; init; }

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
            BaselineMode = BaselineMode,
            BaselineValue = BaselineValue,
            Delta = Delta,
            AllowedDelta = AllowedDelta,
            EffectiveThreshold = EffectiveThreshold,
            AbsoluteCap = AbsoluteCap,
        };
}
