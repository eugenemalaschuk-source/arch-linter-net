namespace ArchLinterNet.Core.Caching;

// Persistent cache is disabled unless a caller opts in — see openspec/specs/analysis-cache/spec.md,
// "Cache location defaults are opt-in and never authored by content".
public enum AnalysisCacheMode
{
    Disabled,
    Auto,
    ExplicitPath,
}
