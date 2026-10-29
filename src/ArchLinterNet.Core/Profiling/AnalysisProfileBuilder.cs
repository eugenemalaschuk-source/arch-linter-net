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
        AnalysisProfileBuildOptions? options = null)
    {
        List<AnalysisProfilePhaseMeasurement> phases = BuildPhases(snapshotCounters, timing);
        Dictionary<string, int> contractFamilyCounts = BuildContractFamilyCounts(timing);
        AnalysisProfileCounters counters = AnalysisProfileCounters.From(
            snapshotCounters, contractFamilyCounts, renderedSinkCount, outputSinkCount);
        if (options?.Cache is not null)
        {
            counters = counters with { Cache = options.Cache };
        }

        return new AnalysisProfile
        {
            CompletionStatus = completionStatus,
            CancellationObserved = cancellationObserved,
            Counters = counters,
            Phases = phases,
            Output = options?.Output ?? CreateEmptyOutput(),
            Measurements = options?.Measurements,
        };
    }

    private static List<AnalysisProfilePhaseMeasurement> BuildPhases(
        ArchitectureAnalysisSnapshotCounters snapshotCounters,
        ValidationTiming? timing)
    {
        List<AnalysisProfilePhaseMeasurement> phases = new();
        bool selectorPhaseMeasured = false;
        if (timing is not null)
        {
            foreach (ValidationTiming.Entry entry in timing.Entries)
            {
                bool isSelectorPhase = entry.Name == "selector_predicate_evaluation";
                selectorPhaseMeasured |= isSelectorPhase;
                phases.Add(new AnalysisProfilePhaseMeasurement(
                    entry.Name,
                    entry.Indent,
                    entry.Ordinal,
                    isSelectorPhase ? snapshotCounters.SelectorPredicateEvaluations : entry.Count,
                    entry.HighResolutionElapsedMs ?? entry.ElapsedMs,
                    entry.ProcessorTimeMs));
            }
        }

        if (snapshotCounters.SelectorPredicateEvaluations > 0 && !selectorPhaseMeasured)
        {
            int ordinal = phases.Count == 0 ? 0 : phases.Max(phase => phase.Ordinal) + 1;
            phases.Add(new AnalysisProfilePhaseMeasurement(
                "selector_predicate_evaluation",
                1,
                ordinal,
                snapshotCounters.SelectorPredicateEvaluations,
                null,
                null));
        }

        return phases;
    }

    private static Dictionary<string, int> BuildContractFamilyCounts(ValidationTiming? timing)
    {
        // Contract-family entries carry execution counts; ordinary timing entries do not.
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

        return contractFamilyCounts;
    }

    private static AnalysisProfileOutput CreateEmptyOutput()
    {
        return new AnalysisProfileOutput
        {
            CommittedSinkCount = 0,
            FailedSinkCount = 0,
            StagedSinkCount = 0,
            UncommittedSinkCount = 0,
            OutputFailed = false,
        };
    }
}
