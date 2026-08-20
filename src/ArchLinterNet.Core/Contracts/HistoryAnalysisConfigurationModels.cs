using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed class HistoryAnalysisConfiguration
{
    [YamlMember(Alias = "extractors")]
    public List<HistoryTaskExtractorConfiguration> Extractors { get; set; } = [];

    [YamlMember(Alias = "paths")]
    public HistoryPathConfiguration Paths { get; set; } = new();

    [YamlMember(Alias = "ignore")]
    public List<string> Ignore { get; set; } = [];

    [YamlMember(Alias = "weights")]
    public HistoryAnalysisWeightProfiles Weights { get; set; } = new();

    [YamlMember(Alias = "thresholds")]
    public HistoryAnalysisThresholds Thresholds { get; set; } = new();
}

public sealed class HistoryTaskExtractorConfiguration
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace")]
    public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "pattern")]
    public HistoryTaskExtractorPattern Pattern { get; set; } = new();
}

public sealed class HistoryTaskExtractorPattern
{
    [YamlMember(Alias = "prefix")]
    public string Prefix { get; set; } = string.Empty;

    [YamlMember(Alias = "suffix")]
    public string Suffix { get; set; } = string.Empty;
}

public sealed class HistoryPathConfiguration
{
    [YamlMember(Alias = "production")]
    public List<string> Production { get; set; } = [];

    [YamlMember(Alias = "tests")]
    public List<string> Tests { get; set; } = [];

    [YamlMember(Alias = "docs")]
    public List<string> Docs { get; set; } = [];

    [YamlMember(Alias = "generated")]
    public List<string> Generated { get; set; } = [];

    [YamlMember(Alias = "build_ci")]
    public List<string> BuildCi { get; set; } = [];

    [YamlMember(Alias = "samples_examples")]
    public List<string> SamplesExamples { get; set; } = [];
}

public sealed class HistoryAnalysisWeightProfiles
{
    [YamlMember(Alias = "hotspot")]
    public HistoryHotspotWeightProfile Hotspot { get; set; } = new();

    [YamlMember(Alias = "co_change")]
    public HistoryCoChangeWeightProfile CoChange { get; set; } = new();

    [YamlMember(Alias = "bottleneck")]
    public HistoryBottleneckWeightProfile Bottleneck { get; set; } = new();

    [YamlMember(Alias = "ocp")]
    public HistoryOcpWeightProfile Ocp { get; set; } = new();
}

public sealed class HistoryHotspotWeightProfile
{
    [YamlMember(Alias = "commit")]
    public decimal Commit { get; set; } = 0.30m;

    [YamlMember(Alias = "churn")]
    public decimal Churn { get; set; } = 0.25m;

    [YamlMember(Alias = "task")]
    public decimal Task { get; set; } = 0.25m;

    [YamlMember(Alias = "author")]
    public decimal Author { get; set; } = 0.10m;

    [YamlMember(Alias = "temporal")]
    public decimal Temporal { get; set; } = 0.10m;
}

public sealed class HistoryCoChangeWeightProfile
{
    [YamlMember(Alias = "commit")]
    public decimal Commit { get; set; } = 0.75m;

    [YamlMember(Alias = "task")]
    public decimal Task { get; set; } = 0.25m;
}

public sealed class HistoryBottleneckWeightProfile
{
    [YamlMember(Alias = "independent_task")]
    public decimal IndependentTask { get; set; } = 0.35m;

    [YamlMember(Alias = "author")]
    public decimal Author { get; set; } = 0.15m;

    [YamlMember(Alias = "temporal")]
    public decimal Temporal { get; set; } = 0.20m;

    [YamlMember(Alias = "degree")]
    public decimal Degree { get; set; } = 0.20m;

    [YamlMember(Alias = "centrality")]
    public decimal Centrality { get; set; } = 0.10m;
}

public sealed class HistoryOcpWeightProfile
{
    [YamlMember(Alias = "independent_task")]
    public decimal IndependentTask { get; set; } = 0.40m;

    [YamlMember(Alias = "centrality")]
    public decimal Centrality { get; set; } = 0.25m;

    [YamlMember(Alias = "repeated_edit")]
    public decimal RepeatedEdit { get; set; } = 0.25m;

    [YamlMember(Alias = "role_hint")]
    public decimal RoleHint { get; set; } = 0.10m;
}

public sealed class HistoryAnalysisThresholds
{
    [YamlMember(Alias = "co_change_significance")]
    public decimal? CoChangeSignificance { get; set; }
}
