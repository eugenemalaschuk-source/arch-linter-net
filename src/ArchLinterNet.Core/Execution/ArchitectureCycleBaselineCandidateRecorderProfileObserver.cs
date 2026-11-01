using System.Diagnostics;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureCycleBaselineCandidateRecorderProfileObserver
{
    internal static void Record(
        ArchitectureCycleBaselineCandidateRecorder recorder,
        bool recordingEnabled,
        IReadOnlyDictionary<string, HashSet<string>> fullGraph,
        IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence,
        string? contractGroup,
        ValidationTiming? timing)
    {
        if (timing?.CycleRecorderMeasurementsEnabled != true)
        {
            recorder.Record(recordingEnabled, fullGraph, candidateEvidence);
            return;
        }

        long observationStartTicks = Stopwatch.GetTimestamp();
        long observationAllocatedBytesStart = GC.GetAllocatedBytesForCurrentThread();
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();

        int vertexCount = fullGraph.Count;
        long edgeCount = 0;
        foreach (HashSet<string> targets in fullGraph.Values)
        {
            edgeCount += targets.Count;
        }

        int evidenceCount = candidateEvidence.Count;
        int uniqueEvidencePairCount = CountUniqueEvidencePairs(candidateEvidence);

        long recorderStartTicks = Stopwatch.GetTimestamp();
        long recorderAllocatedBytesStart = GC.GetAllocatedBytesForCurrentThread();
        recorder.Record(recordingEnabled, fullGraph, candidateEvidence, workCounter);
        long recorderAllocatedBytes = Math.Max(
            0, GC.GetAllocatedBytesForCurrentThread() - recorderAllocatedBytesStart);
        long recorderElapsedTicks = Stopwatch.GetTimestamp() - recorderStartTicks;

        long observationElapsedTicks = Stopwatch.GetTimestamp() - observationStartTicks;
        long observationAllocatedBytes = Math.Max(
            0,
            GC.GetAllocatedBytesForCurrentThread()
            - observationAllocatedBytesStart
            - recorderAllocatedBytes);

        timing.RecordCycleRecorderObservation(new CycleRecorderObservation(
            contractGroup switch
            {
                "strict_cycles" => "strict",
                "audit_cycles" => "audit",
                _ => "other",
            },
            recordingEnabled,
            vertexCount,
            edgeCount,
            evidenceCount,
            uniqueEvidencePairCount,
            workCounter.CandidateEmissionCount,
            ToMilliseconds(recorderElapsedTicks),
            ToMilliseconds(workCounter.ComponentIndexBuildElapsedTicks),
            ToMilliseconds(workCounter.CandidateEvaluationElapsedTicks),
            recorderAllocatedBytes,
            ToMilliseconds(Math.Max(0, observationElapsedTicks - recorderElapsedTicks)),
            observationAllocatedBytes,
            new CycleRecorderWorkObservation(
                workCounter.ComponentIndexBuildCount,
                workCounter.PreparedVertexCount,
                workCounter.PreparedEdgeCount,
                workCounter.AdjacencyListsBuilt,
                workCounter.AdjacencyEdgeVisits,
                workCounter.ReverseDegreeEdgeVisits,
                workCounter.ReverseFillEdgeVisits,
                workCounter.FirstPassVertexVisits,
                workCounter.FirstPassEdgeVisits,
                workCounter.SecondPassVertexVisits,
                workCounter.SecondPassEdgeVisits,
                workCounter.CandidateEvidenceChecks,
                workCounter.CandidateEdgePresenceChecks,
                workCounter.ComponentMembershipChecks,
                workCounter.CandidateEmissionCount)));
    }

    private static int CountUniqueEvidencePairs(IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence)
    {
        if (candidateEvidence.Count == 0)
        {
            return 0;
        }

        HashSet<(string Source, string Target)> uniquePairs = new();
        foreach (CycleCandidateEvidence evidence in candidateEvidence)
        {
            uniquePairs.Add((evidence.SourceLayerName, evidence.TargetLayerName));
        }

        return uniquePairs.Count;
    }

    private static double ToMilliseconds(long stopwatchTicks) =>
        stopwatchTicks * 1000d / Stopwatch.Frequency;
}
