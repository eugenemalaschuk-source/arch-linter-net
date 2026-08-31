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

    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.MetricBudget;

    public IReadOnlyCollection<string> Contributors { get; init; } = Contributors
        .OrderBy(contributor => contributor, StringComparer.Ordinal)
        .ToArray();

    public int ContributorCount => Contributors.Count;
}
