namespace ArchLinterNet.Core.Profiling;

// The analysis-profile/v1 machine-readable contract — see openspec/specs/analysis-profile/spec.md
// and docs/internal/analysis-profile-dictionary.md.
public sealed record AnalysisProfile
{
    public string SchemaId { get; init; } = AnalysisProfileId.V1;

    public required AnalysisProfileCompletionStatus CompletionStatus { get; init; }

    public required bool CancellationObserved { get; init; }

    public required AnalysisProfileCounters Counters { get; init; }

    public required IReadOnlyList<AnalysisProfilePhaseMeasurement> Phases { get; init; }

    public required AnalysisProfileOutput Output { get; init; }

    public AnalysisProfileMeasurements? Measurements { get; init; }
}
