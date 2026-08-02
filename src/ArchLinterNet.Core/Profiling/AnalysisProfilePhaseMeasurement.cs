namespace ArchLinterNet.Core.Profiling;

// Structural fields are deterministic for a request; elapsed and processor measurements depend on
// the environment and are null when no timing instance backed the run.
public sealed record AnalysisProfilePhaseMeasurement(
    string Name,
    int Indent,
    int Ordinal,
    int? Count,
    double? ElapsedMs,
    double? ProcessorTimeMs);
