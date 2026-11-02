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
        var counters = AnalysisProfileCounters.From(
            snapshotCounters, contractFamilyCounts, renderedSinkCount, outputSinkCount);
        if (options?.Cache is not null)
        {
            counters = counters with { Cache = options.Cache };
        }

        AnalysisProfileMeasurements? measurements = options?.Measurements;
        if (timing?.CycleRecorderMeasurementsEnabled == true)
        {
            measurements = (measurements ?? new AnalysisProfileMeasurements()) with
            {
                CycleRecorder = BuildCycleRecorderMeasurements(timing),
            };
        }

        return new AnalysisProfile
        {
            CompletionStatus = completionStatus,
            CancellationObserved = cancellationObserved,
            Counters = counters,
            Phases = phases,
            Output = options?.Output ?? CreateEmptyOutput(),
            Measurements = measurements,
        };
    }

    private static AnalysisProfileCycleRecorderMeasurements BuildCycleRecorderMeasurements(ValidationTiming timing)
    {
        AnalysisProfileCycleRecorderInvocation[] invocations = timing.CycleRecorderObservations
            .Select(observation => new AnalysisProfileCycleRecorderInvocation
            {
                Mode = observation.Mode,
                RecordingEnabled = observation.RecordingEnabled,
                Vertices = observation.Vertices,
                Edges = observation.Edges,
                CandidateEvidenceCount = observation.CandidateEvidenceCount,
                UniqueCandidateEdgePairs = observation.UniqueCandidateEdgePairs,
                EmittedCandidateCount = observation.EmittedCandidateCount,
                ElapsedMs = observation.ElapsedMs,
                ComponentIndexBuildElapsedMs = observation.ComponentIndexBuildElapsedMs,
                CandidateEvaluationElapsedMs = observation.CandidateEvaluationElapsedMs,
                AllocatedBytes = observation.AllocatedBytes,
                ObservationOverheadElapsedMs = observation.ObservationOverheadElapsedMs,
                ObservationOverheadAllocatedBytes = observation.ObservationOverheadAllocatedBytes,
                Work = new AnalysisProfileCycleRecorderWork
                {
                    ComponentIndexBuildCount = observation.Work.ComponentIndexBuildCount,
                    PreparedVertices = observation.Work.PreparedVertices,
                    PreparedEdges = observation.Work.PreparedEdges,
                    AdjacencyListsBuilt = observation.Work.AdjacencyListsBuilt,
                    AdjacencyEdgeVisits = observation.Work.AdjacencyEdgeVisits,
                    ReverseDegreeEdgeVisits = observation.Work.ReverseDegreeEdgeVisits,
                    ReverseFillEdgeVisits = observation.Work.ReverseFillEdgeVisits,
                    FirstPassVertexVisits = observation.Work.FirstPassVertexVisits,
                    FirstPassEdgeVisits = observation.Work.FirstPassEdgeVisits,
                    SecondPassVertexVisits = observation.Work.SecondPassVertexVisits,
                    SecondPassEdgeVisits = observation.Work.SecondPassEdgeVisits,
                    CandidateEvidenceChecks = observation.Work.CandidateEvidenceChecks,
                    CandidateEdgePresenceChecks = observation.Work.CandidateEdgePresenceChecks,
                    ComponentMembershipChecks = observation.Work.ComponentMembershipChecks,
                    CandidateEmissionCount = observation.Work.CandidateEmissionCount,
                },
            })
            .ToArray();

        return new AnalysisProfileCycleRecorderMeasurements
        {
            InvocationCount = invocations.Length,
            EnabledInvocationCount = invocations.Count(invocation => invocation.RecordingEnabled),
            DisabledInvocationCount = invocations.Count(invocation => !invocation.RecordingEnabled),
            EmptyEvidenceInvocationCount = invocations.Count(invocation => invocation.CandidateEvidenceCount == 0),
            NonEmptyEvidenceInvocationCount = invocations.Count(invocation => invocation.CandidateEvidenceCount > 0),
            Vertices = invocations.Sum(invocation => (long)invocation.Vertices),
            Edges = invocations.Sum(invocation => invocation.Edges),
            CandidateEvidenceCount = invocations.Sum(invocation => (long)invocation.CandidateEvidenceCount),
            UniqueCandidateEdgePairs = invocations.Sum(invocation => (long)invocation.UniqueCandidateEdgePairs),
            EmittedCandidateCount = invocations.Sum(invocation => invocation.EmittedCandidateCount),
            ElapsedMs = invocations.Sum(invocation => invocation.ElapsedMs),
            ComponentIndexBuildElapsedMs = invocations.Sum(invocation => invocation.ComponentIndexBuildElapsedMs),
            CandidateEvaluationElapsedMs = invocations.Sum(invocation => invocation.CandidateEvaluationElapsedMs),
            AllocatedBytes = invocations.Sum(invocation => invocation.AllocatedBytes),
            ObservationOverheadElapsedMs = invocations.Sum(invocation => invocation.ObservationOverheadElapsedMs),
            ObservationOverheadAllocatedBytes = invocations.Sum(invocation => invocation.ObservationOverheadAllocatedBytes),
            Invocations = invocations,
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
        return new()
        {
            CommittedSinkCount = 0,
            FailedSinkCount = 0,
            StagedSinkCount = 0,
            UncommittedSinkCount = 0,
            OutputFailed = false,
        };
    }
}
