using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureCycleDetectorBenchmarkHarness
{
    private static readonly int[] _largeGraphSmallEvidenceCounts = [1, 10];

    private static List<object> MeasureLargeGraphSmallEvidenceMatrix()
    {
        LargeGraphWorkload[] workloads = [CreateLargeSparseWorkload(), CreateLargeDenseWorkload()];
        List<object> measurements = [];

        foreach (LargeGraphWorkload workload in workloads)
        {
            foreach (int evidenceCount in _largeGraphSmallEvidenceCounts)
            {
                IReadOnlyCollection<CycleCandidateEvidence> evidence =
                    CreateLargeGraphEvidence(workload, evidenceCount);
                WarmUpRecorders(workload.Graph, evidence);
                LegacyRecorderMeasurement legacy = MeasureLegacyRecorder(
                    workload.Graph,
                    evidence,
                    expectedCandidateCount: evidenceCount);
                IndexedRecorderMeasurement indexed = MeasureIndexedRecorder(
                    workload.Graph,
                    evidence,
                    expectedCandidateCount: evidenceCount);
                Assert.That(indexed.CandidateCount, Is.EqualTo(legacy.CandidateCount));
                Assert.That(indexed.CandidateDigest, Is.EqualTo(legacy.CandidateDigest));

                measurements.Add(new
                {
                    Workload = workload.Name,
                    Vertices = workload.Graph.Count,
                    Edges = workload.Graph.Values.Sum(targets => targets.Count),
                    GraphSha256 = GraphSha256(workload.Graph),
                    CandidateEvidenceCount = evidenceCount,
                    CandidateEvidenceSha256 = EvidenceSha256(evidence),
                    Legacy = ToLegacyBenchmarkResult(legacy),
                    Indexed = ToIndexedBenchmarkResult(indexed),
                });
            }
        }

        return measurements;
    }

    private static LargeGraphWorkload CreateLargeSparseWorkload()
    {
        const int VertexCount = 10_000;
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);

        for (int source = 0; source < VertexCount; source++)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            if (source + 1 < VertexCount)
            {
                targets.Add(LargeGraphNodeName(source + 1));
            }
            else
            {
                targets.Add(LargeGraphNodeName(0));
            }

            graph.Add(LargeGraphNodeName(source), targets);
        }

        (string Source, string Target)[] candidateEdges = Enumerable.Range(0, 10)
            .Select(source => (LargeGraphNodeName(source), LargeGraphNodeName(source + 1)))
            .ToArray();
        return new("sparse-cycle-v10000-e10000", graph, candidateEdges);
    }

    private static LargeGraphWorkload CreateLargeDenseWorkload()
    {
        const int VertexCount = 500;
        Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);

        for (int source = 0; source < VertexCount; source++)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            for (int target = source + 1; target < VertexCount; target++)
            {
                targets.Add(LargeGraphNodeName(target));
            }

            if (source == VertexCount - 1)
            {
                targets.Add(LargeGraphNodeName(0));
            }

            graph.Add(LargeGraphNodeName(source), targets);
        }

        (string Source, string Target)[] candidateEdges = Enumerable.Range(0, 10)
            .Select(source => (LargeGraphNodeName(source), LargeGraphNodeName(source + 1)))
            .ToArray();
        return new("dense-cycle-v500-e124751", graph, candidateEdges);
    }

    private static IReadOnlyCollection<CycleCandidateEvidence> CreateLargeGraphEvidence(
        LargeGraphWorkload workload,
        int evidenceCount)
    {
        foreach (var edge in workload.CandidateEdges.Take(evidenceCount))
        {
            Assert.That(workload.Graph[edge.Source].Contains(edge.Target), Is.True);
        }

        return Enumerable.Range(0, evidenceCount)
            .Select(index => new CycleCandidateEvidence(
                workload.CandidateEdges[index].Source,
                workload.CandidateEdges[index].Target,
                CreateCandidate(
                    index % 2 == 0 ? "strict_cycles" : "audit_cycles",
                    $"{workload.Name}-contract-{index}",
                    $"Synthetic.Source{index:D2}",
                    $"Synthetic.Target{index:D2}",
                    index)))
            .ToArray();
    }

    private static string GraphSha256(IReadOnlyDictionary<string, HashSet<string>> graph)
    {
        return Sha256(string.Join(
            "\n",
            graph.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}:{string.Join(",", entry.Value.Order(StringComparer.Ordinal))}")));
    }

    private static string LargeGraphNodeName(int index)
    {
        return $"Node{index:D5}";
    }

    private static object ToLegacyBenchmarkResult(LegacyRecorderMeasurement measurement)
    {
        return new
        {
            measurement.CandidateCount,
            CandidateSha256 = measurement.CandidateDigest,
            measurement.Samples,
            measurement.ElapsedMilliseconds,
            measurement.MedianElapsedMilliseconds,
            measurement.AllocatedBytes,
            measurement.MedianAllocatedBytes,
            measurement.RetainedManagedBytesDelta,
            measurement.PeakWorkingSetDeltaBytes,
            Work = new
            {
                measurement.Work.ReachabilitySearches,
                measurement.Work.ReachabilityNodePops,
                measurement.Work.ReachabilityVisitedVertices,
                measurement.Work.ReachabilityEdgeVisits,
                measurement.Work.CandidateEmissionCount,
            },
        };
    }

    private static object ToIndexedBenchmarkResult(IndexedRecorderMeasurement measurement)
    {
        return new
        {
            measurement.CandidateCount,
            CandidateSha256 = measurement.CandidateDigest,
            measurement.Samples,
            measurement.ElapsedMilliseconds,
            measurement.MedianElapsedMilliseconds,
            measurement.AllocatedBytes,
            measurement.MedianAllocatedBytes,
            measurement.RetainedManagedBytesDelta,
            measurement.PeakWorkingSetDeltaBytes,
            Work = new
            {
                measurement.Work.CandidateEvidenceChecks,
                measurement.Work.CandidateEdgePresenceChecks,
                measurement.Work.ComponentMembershipChecks,
                measurement.Work.CandidateEmissionCount,
                measurement.Work.ReachabilitySearches,
                measurement.Work.ReachabilityNodePops,
                measurement.Work.ReachabilityVisitedVertices,
                measurement.Work.ReachabilityEdgeVisits,
                measurement.Work.ComponentIndexBuildCount,
                IndexPreparedVertexCount = measurement.Work.PreparedVertexCount,
                IndexPreparedEdgeCount = measurement.Work.PreparedEdgeCount,
                IndexAdjacencyListsBuilt = measurement.Work.AdjacencyListsBuilt,
                IndexAdjacencyEdgeVisits = measurement.Work.AdjacencyEdgeVisits,
                IndexReverseDegreeEdgeVisits = measurement.Work.ReverseDegreeEdgeVisits,
                IndexReverseFillEdgeVisits = measurement.Work.ReverseFillEdgeVisits,
                IndexFirstPassVertexVisits = measurement.Work.FirstPassVertexVisits,
                IndexFirstPassEdgeVisits = measurement.Work.FirstPassEdgeVisits,
                IndexSecondPassVertexVisits = measurement.Work.SecondPassVertexVisits,
                IndexSecondPassEdgeVisits = measurement.Work.SecondPassEdgeVisits,
            },
        };
    }

    private sealed record LargeGraphWorkload(
        string Name,
        Dictionary<string, HashSet<string>> Graph,
        (string Source, string Target)[] CandidateEdges);
}
