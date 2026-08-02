namespace ArchLinterNet.Core.Profiling;

// Optional host-provided evidence attached while assembling an analysis profile.
public sealed record AnalysisProfileBuildOptions
{
    public AnalysisProfileMeasurements? Measurements { get; init; }

    public AnalysisProfileOutput? Output { get; init; }
}
