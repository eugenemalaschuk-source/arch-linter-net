using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Profiling;

// Assembles an AnalysisProfile from already-existing instrumentation (ArchitectureAnalysisSnapshotCounters,
// ValidationTiming) without modifying either — see openspec/specs/analysis-profile/spec.md,
// "A versioned, machine-readable analysis profile is available".
public static class AnalysisProfileBuilder
{
    public static AnalysisProfile Build(
        ArchitectureAnalysisSnapshotCounters snapshotCounters,
        ValidationTiming? timing,
        int renderedSinkCount,
        int outputSinkCount,
        AnalysisProfileCompletionStatus completionStatus,
        bool cancellationObserved,
        AnalysisProfileMeasurements? measurements = null)
    {
        IReadOnlyList<AnalysisProfilePhaseMeasurement> phases = timing is null
            ? Array.Empty<AnalysisProfilePhaseMeasurement>()
            : timing.Entries
                .Select(entry => new AnalysisProfilePhaseMeasurement(entry.Name, entry.Indent, entry.Ordinal, entry.Count, entry.ElapsedMs))
                .ToList();

        // Only MeasureContractFamily entries carry a Count (see ArchitectureContractExecutor);
        // plain Measure phases (policy_composition, load_and_setup, ...) never set it, so this
        // filter naturally selects exactly the per-family execution counts.
        Dictionary<string, int> contractFamilyCounts = new(StringComparer.Ordinal);
        if (timing is not null)
        {
            foreach (ValidationTiming.Entry entry in timing.Entries)
            {
                if (entry.Count.HasValue)
                {
                    contractFamilyCounts.TryGetValue(entry.Name, out int existingCount);
                    contractFamilyCounts[entry.Name] = existingCount + entry.Count.Value;
                }
            }
        }

        AnalysisProfileCounters counters = AnalysisProfileCounters.From(
            snapshotCounters, contractFamilyCounts, renderedSinkCount, outputSinkCount);

        return new AnalysisProfile
        {
            CompletionStatus = completionStatus,
            CancellationObserved = cancellationObserved,
            Counters = counters,
            Phases = phases,
            Measurements = measurements,
        };
    }
}
