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
            int sampleCount = 5;
            List<double> elapsedMilliseconds = [];
            List<long> allocatedBytes = [];
            ArchitectureCycleBaselineCandidateRecorder? retainedRecorder = null;
            long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
            long peakWorkingSetBefore = Process.GetCurrentProcess().PeakWorkingSet64;

            for (int sample = 0; sample < sampleCount; sample++)
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
            ArchitectureCycleBaselineCandidateRecorder countedRecorder = new();
            countedRecorder.Record(enabled: true, graph, evidence, workCounter);
            string candidateSha256 = CandidateSha256(countedRecorder.Candidates);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long retainedAfter = GC.GetTotalMemory(forceFullCollection: true);

            measurements.Add(new
            {
                CandidateEvidenceCount = evidenceCount,
                CandidateEvidenceSha256 = EvidenceSha256(evidence),
                CandidateCount = countedRecorder.Candidates.Count,
                CandidateSha256 = candidateSha256,
                Samples = sampleCount,
                ElapsedMilliseconds = elapsedMilliseconds,
                MedianElapsedMilliseconds = Median(elapsedMilliseconds),
                AllocatedBytes = allocatedBytes,
                MedianAllocatedBytes = Median(allocatedBytes),
                RetainedManagedBytesBefore = retainedBefore,
                RetainedManagedBytesAfter = retainedAfter,
                RetainedManagedBytesDelta = retainedAfter - retainedBefore,
                PeakWorkingSetBytesBefore = peakWorkingSetBefore,
                PeakWorkingSetBytesAfter = peakWorkingSetAfter,
                PeakWorkingSetDeltaBytes = Math.Max(0, peakWorkingSetAfter - peakWorkingSetBefore),
                Work = new
                {
                    workCounter.CandidateEvidenceChecks,
                    workCounter.CandidateEdgePresenceChecks,
                    workCounter.ComponentMembershipChecks,
                    workCounter.CandidateEmissionCount,
                    workCounter.ReachabilitySearches,
                    workCounter.ReachabilityNodePops,
                    workCounter.ReachabilityVisitedVertices,
                    workCounter.ReachabilityEdgeVisits,
                    workCounter.ComponentIndexBuildCount,
                    IndexPreparedVertexCount = workCounter.PreparedVertexCount,
                    IndexPreparedEdgeCount = workCounter.PreparedEdgeCount,
                    IndexAdjacencyListsBuilt = workCounter.AdjacencyListsBuilt,
                    IndexAdjacencyEdgeVisits = workCounter.AdjacencyEdgeVisits,
                    IndexReverseDegreeEdgeVisits = workCounter.ReverseDegreeEdgeVisits,
                    IndexReverseFillEdgeVisits = workCounter.ReverseFillEdgeVisits,
                    IndexFirstPassVertexVisits = workCounter.FirstPassVertexVisits,
                    IndexFirstPassEdgeVisits = workCounter.FirstPassEdgeVisits,
                    IndexSecondPassVertexVisits = workCounter.SecondPassVertexVisits,
                    IndexSecondPassEdgeVisits = workCounter.SecondPassEdgeVisits,
                },
            });

            GC.KeepAlive(retainedRecorder);
        }

        TestContext.Progress.WriteLine(JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Configuration = "Release",
            SourceIdentity = Environment.GetEnvironmentVariable("ARCHLINTER_CYCLE_BASELINE_SOURCE_IDENTITY") ?? "unreported",
            GraphShape = "fixed two-SCC graph with a three-vertex acyclic tail",
            Vertices = graph.Count,
            Edges = graph.Values.Sum(targets => targets.Count),
            GraphSha256 = graphSha256,
            CandidatePattern = "A->B, G->H, C->D, E->F repeated in that order",
            TimingBoundary = "direct Record call; output recorder/evidence/graph construction excluded",
            Measurements = measurements,
        }, new JsonSerializerOptions { WriteIndented = true }));
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
}
