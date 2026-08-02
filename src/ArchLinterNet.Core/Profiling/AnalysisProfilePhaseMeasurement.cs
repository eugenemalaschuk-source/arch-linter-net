namespace ArchLinterNet.Core.Profiling;

// Name/Indent/Ordinal/Count are deterministic (identical every run for the same request);
// ElapsedMs is the environment-dependent measurement and is null unless a real ValidationTiming
// instance measured it. See openspec/specs/analysis-profile/spec.md, "Deterministic counters are
// separated from environment-dependent measurements".
public sealed record AnalysisProfilePhaseMeasurement(
    string Name,
    int Indent,
    int Ordinal,
    int? Count,
    double? ElapsedMs);
