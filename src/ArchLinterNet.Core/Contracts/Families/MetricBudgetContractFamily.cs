using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

/// <summary>One strict or audit bound over a declared architecture metric.</summary>
public sealed class ArchitectureMetricBudgetContract : IArchitectureContract
{
    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "metric")] public string Metric { get; set; } = string.Empty;

    [YamlMember(Alias = "minimum")] public int? Minimum { get; set; }

    [YamlMember(Alias = "maximum")] public int? Maximum { get; set; }

    [YamlMember(Alias = "baseline_mode")] public string? BaselineMode { get; set; }

    [YamlMember(Alias = "max_delta")] public int? MaxDelta { get; set; }

    // Finding-level baseline entries are merged here in memory. Metric scalar baselines remain
    // separate on ArchitectureBaselineDocument.MetricBaselines.
    [YamlIgnore]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    public bool IsRelative => BaselineMode is not null;

    public int AllowedDelta => BaselineMode == "no_worse_than_baseline" ? 0 : MaxDelta ?? 0;

    // Metric budgets intentionally have no separate display-name field. Their reviewed id is the
    // stable human-readable label used by the normal contract result envelope.
    [YamlIgnore]
    public string Name => Id ?? "metric_budget";
}
