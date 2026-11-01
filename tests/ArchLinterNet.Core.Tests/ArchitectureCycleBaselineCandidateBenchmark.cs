using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureCycleDetectorBenchmarkHarness
{
    private static readonly int[] _baselineCandidateSizes = [100, 1_000, 10_000];

    [Test]
    public void RunBaselineCandidateRecorderMatrix()
    {
        Dictionary<string, HashSet<string>> graph = CreateBaselineCandidateGraph();
        string graphSha256 = Sha256(string.Join(
            "\n",
            graph.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key}:{string.Join(",", entry.Value.Order(StringComparer.Ordinal))}")));
        List<object> measurements = [];

        foreach (int evidenceCount in _baselineCandidateSizes)
        {
            IReadOnlyCollection<CycleCandidateEvidence> evidence = CreateBaselineCandidateEvidence(evidenceCount);
            WarmUpRecorders(graph, evidence);
            LegacyRecorderMeasurement legacy = MeasureLegacyRecorder(graph, evidence, evidenceCount);
            IndexedRecorderMeasurement indexed = MeasureIndexedRecorder(graph, evidence, evidenceCount);
            Assert.That(indexed.CandidateCount, Is.EqualTo(legacy.CandidateCount));
            Assert.That(indexed.CandidateSha256, Is.EqualTo(legacy.CandidateSha256));

            measurements.Add(new
            {
                CandidateEvidenceCount = evidenceCount,
                CandidateEvidenceSha256 = EvidenceSha256(evidence),
                Legacy = new
                {
                    legacy.CandidateCount,
                    legacy.CandidateSha256,
                    legacy.Samples,
                    legacy.ElapsedMilliseconds,
                    legacy.MedianElapsedMilliseconds,
                    legacy.AllocatedBytes,
                    legacy.MedianAllocatedBytes,
                    legacy.RetainedManagedBytesDelta,
                    legacy.PeakWorkingSetDeltaBytes,
                    Work = new
                    {
                        legacy.Work.ReachabilitySearches,
                        legacy.Work.ReachabilityNodePops,
                        legacy.Work.ReachabilityVisitedVertices,
                        legacy.Work.ReachabilityEdgeVisits,
                        legacy.Work.CandidateEmissionCount,
                    },
                },
                Indexed = new
                {
                    indexed.CandidateCount,
                    indexed.CandidateSha256,
                    indexed.Samples,
                    indexed.ElapsedMilliseconds,
                    indexed.MedianElapsedMilliseconds,
                    indexed.AllocatedBytes,
                    indexed.MedianAllocatedBytes,
                    indexed.RetainedManagedBytesDelta,
                    indexed.PeakWorkingSetDeltaBytes,
                    Work = new
                    {
                        indexed.Work.CandidateEvidenceChecks,
                        indexed.Work.CandidateEdgePresenceChecks,
                        indexed.Work.ComponentMembershipChecks,
                        indexed.Work.CandidateEmissionCount,
                        indexed.Work.ReachabilitySearches,
                        indexed.Work.ReachabilityNodePops,
                        indexed.Work.ReachabilityVisitedVertices,
                        indexed.Work.ReachabilityEdgeVisits,
                        indexed.Work.ComponentIndexBuildCount,
                        IndexPreparedVertexCount = indexed.Work.PreparedVertexCount,
                        IndexPreparedEdgeCount = indexed.Work.PreparedEdgeCount,
                        IndexAdjacencyListsBuilt = indexed.Work.AdjacencyListsBuilt,
                        IndexAdjacencyEdgeVisits = indexed.Work.AdjacencyEdgeVisits,
                        IndexReverseDegreeEdgeVisits = indexed.Work.ReverseDegreeEdgeVisits,
                        IndexReverseFillEdgeVisits = indexed.Work.ReverseFillEdgeVisits,
                        IndexFirstPassVertexVisits = indexed.Work.FirstPassVertexVisits,
                        IndexFirstPassEdgeVisits = indexed.Work.FirstPassEdgeVisits,
                        IndexSecondPassVertexVisits = indexed.Work.SecondPassVertexVisits,
                        IndexSecondPassEdgeVisits = indexed.Work.SecondPassEdgeVisits,
                    },
                },
            });
        }

        TestContext.Progress.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release",
            SourceIdentity = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_RECORDER_SOURCE_IDENTITY") ?? "unreported",
            LegacyImplementation = "previous per-evidence reachability traversal, reproduced in this explicit harness",
            IndexedImplementation = "one fullGraph SCC index followed by candidate edge and component membership checks",
            GraphShape = "fixed two-SCC graph with a three-vertex acyclic tail",
            Vertices = graph.Count,
            Edges = graph.Values.Sum(targets => targets.Count),
            GraphSha256 = graphSha256,
            CandidatePattern = "A->B, G->H, C->D, E->F repeated in that order",
            TimingBoundary = "direct Record call; output recorder/evidence/graph construction excluded",
            Measurements = measurements,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WarmUpRecorders(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence)
    {
        LegacyArchitectureCycleBaselineCandidateRecorder legacy = new();
        legacy.Record(graph, evidence);
        ArchitectureCycleBaselineCandidateRecorder indexed = new();
        indexed.Record(enabled: true, graph, evidence);
        GC.KeepAlive(legacy);
        GC.KeepAlive(indexed);
    }

    private static LegacyRecorderMeasurement MeasureLegacyRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int evidenceCount)
    {
        const int SampleCount = 5;
        List<double> elapsedMilliseconds = [];
        List<long> allocatedBytes = [];
        LegacyArchitectureCycleBaselineCandidateRecorder? retainedRecorder = null;
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;

        for (int sample = 0; sample < SampleCount; sample++)
        {
            LegacyArchitectureCycleBaselineCandidateRecorder recorder = new();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            recorder.Record(graph, evidence);
            stopwatch.Stop();
            allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            Assert.That(recorder.Candidates, Has.Count.EqualTo(evidenceCount / 2));
            retainedRecorder = recorder;
        }

        long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
        LegacyRecorderWorkCounter workCounter = new();
        LegacyArchitectureCycleBaselineCandidateRecorder? countedRecorder = new();
        countedRecorder.Record(graph, evidence, workCounter);
        string candidateSha256 = CandidateSha256(countedRecorder.Candidates);
        Assert.That(candidateSha256, Is.EqualTo(CandidateSha256(retainedRecorder!.Candidates)));
        int candidateCount = countedRecorder.Candidates.Count;
        countedRecorder = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        LegacyRecorderMeasurement measurement = new(
            candidateCount,
            candidateSha256,
            SampleCount,
            elapsedMilliseconds,
            Median(elapsedMilliseconds),
            allocatedBytes,
            Median(allocatedBytes),
            retainedAfter - retainedBefore,
            Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
            workCounter);
        GC.KeepAlive(retainedRecorder);
        return measurement;
    }

    private static IndexedRecorderMeasurement MeasureIndexedRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int evidenceCount)
    {
        const int SampleCount = 5;
        List<double> elapsedMilliseconds = [];
        List<long> allocatedBytes = [];
        ArchitectureCycleBaselineCandidateRecorder? retainedRecorder = null;
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;

        for (int sample = 0; sample < SampleCount; sample++)
        {
            ArchitectureCycleBaselineCandidateRecorder recorder = new();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            recorder.Record(enabled: true, graph, evidence);
            stopwatch.Stop();
            allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            Assert.That(recorder.Candidates, Has.Count.EqualTo(evidenceCount / 2));
            retainedRecorder = recorder;
        }

        long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder? countedRecorder = new();
        countedRecorder.Record(enabled: true, graph, evidence, workCounter);
        string candidateSha256 = CandidateSha256(countedRecorder.Candidates);
        Assert.That(candidateSha256, Is.EqualTo(CandidateSha256(retainedRecorder!.Candidates)));
        int candidateCount = countedRecorder.Candidates.Count;
        countedRecorder = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        IndexedRecorderMeasurement measurement = new(
            candidateCount,
            candidateSha256,
            SampleCount,
            elapsedMilliseconds,
            Median(elapsedMilliseconds),
            allocatedBytes,
            Median(allocatedBytes),
            retainedAfter - retainedBefore,
            Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
            workCounter);
        GC.KeepAlive(retainedRecorder);
        return measurement;
    }

    private static Dictionary<string, HashSet<string>> CreateBaselineCandidateGraph()
    {
        return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["A"] = new(StringComparer.Ordinal) { "B" },
            ["B"] = new(StringComparer.Ordinal) { "C" },
            ["C"] = new(StringComparer.Ordinal) { "A", "D" },
            ["D"] = new(StringComparer.Ordinal) { "E" },
            ["E"] = new(StringComparer.Ordinal) { "F" },
            ["F"] = new(StringComparer.Ordinal),
            ["G"] = new(StringComparer.Ordinal) { "H" },
            ["H"] = new(StringComparer.Ordinal) { "G" },
        };
    }

    private static IReadOnlyCollection<CycleCandidateEvidence> CreateBaselineCandidateEvidence(int evidenceCount)
    {
        ArchitectureBaselineCandidate[] candidates =
        [
            CreateCandidate("strict_cycles", "contract-a", "Synthetic.SourceA", "Synthetic.TargetB", 0),
            CreateCandidate("audit_cycles", "contract-b", "Synthetic.SourceG", "Synthetic.TargetH", 1),
            CreateCandidate("strict_cycles", "contract-a", "Synthetic.SourceC", "Synthetic.TargetD", 2),
            CreateCandidate("audit_cycles", "contract-b", "Synthetic.SourceE", "Synthetic.TargetF", 3),
        ];
        (string Source, string Target)[] edges = [("A", "B"), ("G", "H"), ("C", "D"), ("E", "F")];

        return Enumerable.Range(0, evidenceCount)
            .Select(index => new CycleCandidateEvidence(
                edges[index % edges.Length].Source,
                edges[index % edges.Length].Target,
                candidates[index % candidates.Length]))
            .ToArray();
    }

    private static ArchitectureBaselineCandidate CreateCandidate(
        string group,
        string contractId,
        string sourceType,
        string targetType,
        int occurrence)
    {
        return new ArchitectureBaselineCandidate(
            group,
            contractId,
            sourceType,
            targetType,
            new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                "cycles",
                "dependency",
                contractId,
                "Synthetic.Assembly",
                sourceType,
                null,
                "Synthetic.Assembly",
                targetType,
                null,
                occurrence));
    }

    private static string EvidenceSha256(IReadOnlyCollection<CycleCandidateEvidence> evidence)
    {
        string payload = string.Join("\n", evidence.Select(item =>
            $"{item.SourceLayerName}|{item.TargetLayerName}|{item.Candidate.ContractGroup}|{item.Candidate.ContractId}|"
            + $"{item.Candidate.SourceType}|{item.Candidate.ForbiddenReference}|{item.Candidate.Identity?.Occurrence}"));
        return Sha256(payload);
    }

    private static string CandidateSha256(IReadOnlyCollection<ArchitectureBaselineCandidate> candidates)
    {
        string payload = JsonSerializer.Serialize(candidates.Select(candidate => new
        {
            candidate.ContractGroup,
            candidate.ContractId,
            candidate.SourceType,
            candidate.ForbiddenReference,
            candidate.Identity,
        }));
        return Sha256(payload);
    }

    private sealed record LegacyRecorderMeasurement(
        int CandidateCount,
        string CandidateSha256,
        int Samples,
        IReadOnlyList<double> ElapsedMilliseconds,
        double MedianElapsedMilliseconds,
        IReadOnlyList<long> AllocatedBytes,
        long MedianAllocatedBytes,
        long RetainedManagedBytesDelta,
        long PeakWorkingSetDeltaBytes,
        LegacyRecorderWorkCounter Work);

    private sealed record IndexedRecorderMeasurement(
        int CandidateCount,
        string CandidateSha256,
        int Samples,
        IReadOnlyList<double> ElapsedMilliseconds,
        double MedianElapsedMilliseconds,
        IReadOnlyList<long> AllocatedBytes,
        long MedianAllocatedBytes,
        long RetainedManagedBytesDelta,
        long PeakWorkingSetDeltaBytes,
        ArchitectureCycleBaselineCandidateRecorderWorkCounter Work);

    private sealed class LegacyRecorderWorkCounter
    {
        internal int ReachabilitySearches { get; set; }

        internal long ReachabilityNodePops { get; set; }

        internal long ReachabilityVisitedVertices { get; set; }

        internal long ReachabilityEdgeVisits { get; set; }

        internal int CandidateEmissionCount { get; set; }
    }

    // Reproduces the per-evidence traversal in the #1125 base revision dcc3b300.
    // Keep this control in the explicit harness so both paths share the same inputs.
    private sealed class LegacyArchitectureCycleBaselineCandidateRecorder
    {
        private readonly List<ArchitectureBaselineCandidate> _candidates = [];

        internal IReadOnlyCollection<ArchitectureBaselineCandidate> Candidates => _candidates;

        internal void Record(
            IReadOnlyDictionary<string, HashSet<string>> graph,
            IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence,
            LegacyRecorderWorkCounter? workCounter = null)
        {
            foreach (CycleCandidateEvidence evidence in candidateEvidence.Where(
                evidence => EdgeParticipatesInCycle(
                    graph,
                    evidence.SourceLayerName,
                    evidence.TargetLayerName,
                    workCounter)))
            {
                _candidates.Add(evidence.Candidate);
                if (workCounter is not null)
                {
                    workCounter.CandidateEmissionCount++;
                }
            }
        }

        private static bool EdgeParticipatesInCycle(
            IReadOnlyDictionary<string, HashSet<string>> graph,
            string sourceLayerName,
            string targetLayerName,
            LegacyRecorderWorkCounter? workCounter)
        {
            if (workCounter is not null)
            {
                workCounter.ReachabilitySearches++;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(targetLayerName);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (workCounter is not null)
                {
                    workCounter.ReachabilityNodePops++;
                }

                if (!visited.Add(current))
                {
                    continue;
                }

                if (workCounter is not null)
                {
                    workCounter.ReachabilityVisitedVertices++;
                }

                if (current == sourceLayerName)
                {
                    return true;
                }

                foreach (string next in graph[current])
                {
                    if (workCounter is not null)
                    {
                        workCounter.ReachabilityEdgeVisits++;
                    }

                    pending.Push(next);
                }
            }

            return false;
        }
    }
}
