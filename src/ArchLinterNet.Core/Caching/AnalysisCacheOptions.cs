namespace ArchLinterNet.Core.Caching;

// CLI --cache and Testing WithCache() both resolve to this shape. Never sourced from policy,
// fragment, baseline, snapshot, receipt, or cache content — only from explicit caller configuration.
public sealed record AnalysisCacheOptions(AnalysisCacheMode Mode, string? ExplicitPath = null)
{
    public static AnalysisCacheOptions Disabled { get; } = new(AnalysisCacheMode.Disabled);

    public static AnalysisCacheOptions Auto { get; } = new(AnalysisCacheMode.Auto);

    public static AnalysisCacheOptions AtPath(string path) => new(AnalysisCacheMode.ExplicitPath, path);

    // Stable, non-sensitive category for instrumentation (AnalysisProfileCacheCounters.Mode) —
    // never the resolved absolute path itself.
    public string ModeCategory => Mode switch
    {
        AnalysisCacheMode.Disabled => "disabled",
        AnalysisCacheMode.Auto => "auto",
        AnalysisCacheMode.ExplicitPath => "path",
        _ => "unknown",
    };
}
