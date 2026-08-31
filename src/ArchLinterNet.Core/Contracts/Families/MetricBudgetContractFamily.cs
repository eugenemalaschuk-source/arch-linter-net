using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

/// <summary>One strict or audit absolute bound over a declared architecture metric.</summary>
public sealed class ArchitectureMetricBudgetContract : IArchitectureContract
{
    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "metric")] public string Metric { get; set; } = string.Empty;

    [YamlMember(Alias = "minimum")] public int? Minimum { get; set; }

    [YamlMember(Alias = "maximum")] public int? Maximum { get; set; }

    // Metric budgets intentionally have no separate display-name field. Their reviewed id is the
    // stable human-readable label used by the normal contract result envelope.
    [YamlIgnore]
    public string Name => Id ?? "metric_budget";
}
