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
        string graphSha256 = GraphSha256(graph);
        List<object> measurements = [];

        foreach (int evidenceCount in _baselineCandidateSizes)
        {
            IReadOnlyCollection<CycleCandidateEvidence> evidence = CreateBaselineCandidateEvidence(evidenceCount);
            WarmUpRecorders(graph, evidence);
            AssertPairedOrderedCandidateOutput(graph, evidence);
            RecorderTimingSamples timing = MeasureAlternatingRecorderSamples(graph, evidence, evidenceCount / 2);
            LegacyRecorderMeasurement legacy = MeasureLegacyRecorder(graph, evidence, evidenceCount / 2, timing);
            IndexedRecorderMeasurement indexed = MeasureIndexedRecorder(graph, evidence, evidenceCount / 2, timing);
            Assert.That(indexed.CandidateCount, Is.EqualTo(legacy.CandidateCount));
            Assert.That(indexed.CandidateDigest, Is.EqualTo(legacy.CandidateDigest));

            measurements.Add(new
            {
                CandidateEvidenceCount = evidenceCount,
                UniqueCandidateEdgePairs = CountUniqueCandidateEdgePairs(evidence),
                CandidateEvidenceSha256 = EvidenceSha256(evidence),
                Legacy = ToLegacyBenchmarkResult(legacy),
                Indexed = ToIndexedBenchmarkResult(indexed),
            });
        }

        List<object> largeGraphMeasurements = MeasureLargeGraphSmallEvidenceMatrix();

        TestContext.Progress.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release",
            SourceIdentity = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_RECORDER_SOURCE_IDENTITY") ?? "unreported",
            LegacyImplementation = "previous per-evidence reachability traversal, reproduced in this explicit harness",
            IndexedImplementation = "one fullGraph SCC index followed by candidate edge and component membership checks",
            TimingSampleOrder = "5 interleaved pairs: legacy then SCC, SCC then legacy, alternating",
            GraphShape = "fixed graph with two cyclic SCCs and a three-vertex acyclic tail",
            Vertices = graph.Count,
            Edges = graph.Values.Sum(targets => targets.Count),
            GraphSha256 = graphSha256,
            CandidatePattern = "A->B, G->H, C->D, E->F repeated in that order",
            TimingBoundary = "direct Record call; output recorder/evidence/graph construction excluded",
            Measurements = measurements,
            LargeGraphSmallEvidenceMeasurements = largeGraphMeasurements,
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

    private static void AssertPairedOrderedCandidateOutput(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence)
    {
        LegacyArchitectureCycleBaselineCandidateRecorder legacy = new();
        legacy.Record(graph, evidence);
        ArchitectureCycleBaselineCandidateRecorder indexed = new();
        indexed.Record(enabled: true, graph, evidence);
        ArchitectureBaselineCandidate[] legacyCandidates = legacy.Candidates.ToArray();
        ArchitectureBaselineCandidate[] indexedCandidates = indexed.Candidates.ToArray();

        Assert.That(indexedCandidates, Is.EqualTo(legacyCandidates));
        Assert.That(CandidateSha256(indexedCandidates), Is.EqualTo(CandidateSha256(legacyCandidates)));
        Assert.That(indexedCandidates.Length, Is.EqualTo(legacyCandidates.Length));
        for (int index = 0; index < legacyCandidates.Length; index++)
        {
            Assert.That(indexedCandidates[index], Is.SameAs(legacyCandidates[index]));
        }
    }

    private static int CountUniqueCandidateEdgePairs(IReadOnlyCollection<CycleCandidateEvidence> evidence) =>
        evidence.Select(item => (item.SourceLayerName, item.TargetLayerName)).Distinct().Count();

    private static LegacyRecorderMeasurement MeasureLegacyRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int expectedCandidateCount,
        RecorderTimingSamples timing)
    {
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;
        LegacyArchitectureCycleBaselineCandidateRecorder retainedRecorder = new();
        retainedRecorder.Record(graph, evidence);
        Assert.That(retainedRecorder.Candidates, Has.Count.EqualTo(expectedCandidateCount));

        long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
        LegacyRecorderObservation observation = ObserveLegacyRecorder(graph, evidence);
        Assert.That(observation.CandidateCount, Is.EqualTo(expectedCandidateCount));
        Assert.That(observation.CandidateDigest, Is.EqualTo(CandidateSha256(retainedRecorder.Candidates)));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        LegacyRecorderMeasurement measurement = new(
            observation.CandidateCount,
            observation.CandidateDigest,
            timing.LegacyElapsedMilliseconds.Count,
            timing.LegacyElapsedMilliseconds,
            Median(timing.LegacyElapsedMilliseconds),
            timing.LegacyAllocatedBytes,
            Median(timing.LegacyAllocatedBytes),
            retainedAfter - retainedBefore,
            Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
            observation.Work);
        GC.KeepAlive(retainedRecorder);
        return measurement;
    }

    private static IndexedRecorderMeasurement MeasureIndexedRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int expectedCandidateCount,
        RecorderTimingSamples timing)
    {
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;
        ArchitectureCycleBaselineCandidateRecorder retainedRecorder = new();
        retainedRecorder.Record(enabled: true, graph, evidence);
        Assert.That(retainedRecorder.Candidates, Has.Count.EqualTo(expectedCandidateCount));

        long peakWorkingSetAfter = Process.GetCurrentProcess().PeakWorkingSet64;
        IndexedRecorderObservation observation = ObserveIndexedRecorder(graph, evidence);
        Assert.That(observation.CandidateCount, Is.EqualTo(expectedCandidateCount));
        Assert.That(observation.CandidateDigest, Is.EqualTo(CandidateSha256(retainedRecorder.Candidates)));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        IndexedRecorderMeasurement measurement = new(
            observation.CandidateCount,
            observation.CandidateDigest,
            timing.IndexedElapsedMilliseconds.Count,
            timing.IndexedElapsedMilliseconds,
            Median(timing.IndexedElapsedMilliseconds),
            timing.IndexedAllocatedBytes,
            Median(timing.IndexedAllocatedBytes),
            retainedAfter - retainedBefore,
            Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
            observation.Work);
        GC.KeepAlive(retainedRecorder);
        return measurement;
    }

    private static RecorderTimingSamples MeasureAlternatingRecorderSamples(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int expectedCandidateCount)
    {
        const int SampleCount = 5;
        List<double> legacyElapsedMilliseconds = new(SampleCount);
        List<long> legacyAllocatedBytes = new(SampleCount);
        List<double> indexedElapsedMilliseconds = new(SampleCount);
        List<long> indexedAllocatedBytes = new(SampleCount);

        for (int sample = 0; sample < SampleCount; sample++)
        {
            if (sample % 2 == 0)
            {
                MeasureLegacyRecorderSample(graph, evidence, expectedCandidateCount, legacyElapsedMilliseconds, legacyAllocatedBytes);
                MeasureIndexedRecorderSample(graph, evidence, expectedCandidateCount, indexedElapsedMilliseconds, indexedAllocatedBytes);
            }
            else
            {
                MeasureIndexedRecorderSample(graph, evidence, expectedCandidateCount, indexedElapsedMilliseconds, indexedAllocatedBytes);
                MeasureLegacyRecorderSample(graph, evidence, expectedCandidateCount, legacyElapsedMilliseconds, legacyAllocatedBytes);
            }
        }

        return new(legacyElapsedMilliseconds, legacyAllocatedBytes, indexedElapsedMilliseconds, indexedAllocatedBytes);
    }

    private static void MeasureLegacyRecorderSample(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int expectedCandidateCount,
        ICollection<double> elapsedMilliseconds,
        ICollection<long> allocatedBytes)
    {
        LegacyArchitectureCycleBaselineCandidateRecorder recorder = new();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        recorder.Record(graph, evidence);
        stopwatch.Stop();
        allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
        Assert.That(recorder.Candidates, Has.Count.EqualTo(expectedCandidateCount));
    }

    private static void MeasureIndexedRecorderSample(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence,
        int expectedCandidateCount,
        ICollection<double> elapsedMilliseconds,
        ICollection<long> allocatedBytes)
    {
        ArchitectureCycleBaselineCandidateRecorder recorder = new();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        recorder.Record(enabled: true, graph, evidence);
        stopwatch.Stop();
        allocatedBytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
        Assert.That(recorder.Candidates, Has.Count.EqualTo(expectedCandidateCount));
    }

    private static LegacyRecorderObservation ObserveLegacyRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence)
    {
        LegacyRecorderWorkCounter workCounter = new();
        LegacyArchitectureCycleBaselineCandidateRecorder recorder = new();
        recorder.Record(graph, evidence, workCounter);
        return new(recorder.Candidates.Count, CandidateSha256(recorder.Candidates), workCounter);
    }

    private static IndexedRecorderObservation ObserveIndexedRecorder(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> evidence)
    {
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder recorder = new();
        recorder.Record(enabled: true, graph, evidence, workCounter);
        return new(recorder.Candidates.Count, CandidateSha256(recorder.Candidates), workCounter);
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
        string CandidateDigest,
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
        string CandidateDigest,
        int Samples,
        IReadOnlyList<double> ElapsedMilliseconds,
        double MedianElapsedMilliseconds,
        IReadOnlyList<long> AllocatedBytes,
        long MedianAllocatedBytes,
        long RetainedManagedBytesDelta,
        long PeakWorkingSetDeltaBytes,
        ArchitectureCycleBaselineCandidateRecorderWorkCounter Work);

    private sealed record RecorderTimingSamples(
        IReadOnlyList<double> LegacyElapsedMilliseconds,
        IReadOnlyList<long> LegacyAllocatedBytes,
        IReadOnlyList<double> IndexedElapsedMilliseconds,
        IReadOnlyList<long> IndexedAllocatedBytes);

    private sealed record LegacyRecorderObservation(
        int CandidateCount,
        string CandidateDigest,
        LegacyRecorderWorkCounter Work);

    private sealed record IndexedRecorderObservation(
        int CandidateCount,
        string CandidateDigest,
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
