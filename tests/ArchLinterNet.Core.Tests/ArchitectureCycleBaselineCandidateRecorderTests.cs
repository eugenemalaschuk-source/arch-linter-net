using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureCycleBaselineCandidateRecorderTests
{
    [Test]
    public void Record_UsesFullGraphComponentsAndPreservesOrderMultiplicityAndIdentity()
    {
        Dictionary<string, HashSet<string>> fullGraph = CreateGraphWithTailsAndSelfEdge();
        ArchitectureBaselineCandidate cycleCandidate = Candidate("strict_cycles", "cycle-a", 0);
        ArchitectureBaselineCandidate secondCycleCandidate = Candidate("audit_cycles", "cycle-b", 1);
        CycleCandidateEvidence[] evidence =
        [
            new("A", "B", cycleCandidate),
            new("A", "B", cycleCandidate),
            new("C", "D", Candidate("strict_cycles", "tail-a", 2)),
            new("E", "F", Candidate("audit_cycles", "tail-b", 3)),
            new("G", "H", secondCycleCandidate),
            new("X", "X", Candidate("strict_cycles", "self-edge", 4)),
            new("Y", "Y", Candidate("audit_cycles", "missing-self-edge", 5)),
            new("A", "A", Candidate("strict_cycles", "missing-edge-in-scc", 6)),
            new("Missing", "B", Candidate("strict_cycles", "missing-source", 7)),
            new("A", "Missing", Candidate("strict_cycles", "missing-target", 8)),
        ];
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder recorder = new();

        recorder.Record(enabled: true, fullGraph, evidence, workCounter);

        Assert.That(recorder.Candidates, Is.EqualTo(new[]
        {
            cycleCandidate,
            cycleCandidate,
            secondCycleCandidate,
            evidence[5].Candidate,
        }));
        Assert.That(recorder.Candidates[0], Is.SameAs(recorder.Candidates[1]));
        Assert.That(recorder.Candidates[0].Identity, Is.EqualTo(cycleCandidate.Identity));
        Assert.That(workCounter.ComponentIndexBuildCount, Is.EqualTo(1));
        Assert.That(workCounter.CandidateEvidenceChecks, Is.EqualTo(evidence.Length));
        Assert.That(workCounter.CandidateEdgePresenceChecks, Is.EqualTo(evidence.Length));
        Assert.That(workCounter.ComponentMembershipChecks, Is.EqualTo(6));
        Assert.That(workCounter.CandidateEmissionCount, Is.EqualTo(4));
        Assert.That(workCounter.ReachabilitySearches, Is.Zero);
    }

    [Test]
    public void Record_KeepsSuppressedMutualEdgesAsCandidatesFromFullGraph()
    {
        Dictionary<string, HashSet<string>> fullGraph = new(StringComparer.Ordinal)
        {
            ["A"] = new(StringComparer.Ordinal) { "B" },
            ["B"] = new(StringComparer.Ordinal) { "A" },
            ["C"] = new(StringComparer.Ordinal),
        };
        CycleCandidateEvidence[] suppressedEvidence =
        [
            new("A", "B", Candidate("strict_cycles", "suppressed-a", 0)),
            new("B", "A", Candidate("audit_cycles", "suppressed-b", 1)),
        ];
        ArchitectureCycleBaselineCandidateRecorder recorder = new();

        recorder.Record(enabled: true, fullGraph, suppressedEvidence);

        Assert.That(recorder.Candidates, Is.EqualTo(suppressedEvidence.Select(item => item.Candidate)));
    }

    [Test]
    public void Record_WhenDisabled_DoesNotBuildComponentIndexOrInspectEvidence()
    {
        Dictionary<string, HashSet<string>> graph = CreateScaleGraph();
        CycleCandidateEvidence[] evidence = CreateScaleEvidence(4);
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder recorder = new();

        recorder.Record(enabled: false, graph, evidence, workCounter);

        Assert.That(recorder.Candidates, Is.Empty);
        Assert.That(workCounter.ComponentIndexBuildCount, Is.Zero);
        Assert.That(workCounter.CandidateEvidenceChecks, Is.Zero);
        Assert.That(workCounter.PreparedVertexCount, Is.Zero);
    }

    [Test]
    public void Record_WhenEnabledWithoutEvidence_DoesNotBuildComponentIndex()
    {
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder recorder = new();

        recorder.Record(
            enabled: true,
            CreateScaleGraph(),
            Array.Empty<CycleCandidateEvidence>(),
            workCounter);

        Assert.That(workCounter.ComponentIndexBuildCount, Is.Zero);
        Assert.That(workCounter.CandidateEvidenceChecks, Is.Zero);
    }

    [TestCase(100)]
    [TestCase(1_000)]
    [TestCase(10_000)]
    public void Record_ScaleUsesOneGraphIndexAndOneMembershipCheckPerEvidence(int evidenceCount)
    {
        Dictionary<string, HashSet<string>> graph = CreateScaleGraph();
        IReadOnlyCollection<CycleCandidateEvidence> evidence = CreateScaleEvidence(evidenceCount);
        ArchitectureCycleBaselineCandidateRecorderWorkCounter workCounter = new();
        ArchitectureCycleBaselineCandidateRecorder recorder = new();

        recorder.Record(enabled: true, graph, evidence, workCounter);

        Assert.That(recorder.Candidates.Count, Is.EqualTo(evidenceCount / 2));
        Assert.That(workCounter.ComponentIndexBuildCount, Is.EqualTo(1));
        Assert.That(workCounter.PreparedVertexCount, Is.EqualTo(8));
        Assert.That(workCounter.PreparedEdgeCount, Is.EqualTo(8));
        Assert.That(workCounter.AdjacencyListsBuilt, Is.EqualTo(8));
        Assert.That(workCounter.AdjacencyEdgeVisits, Is.EqualTo(8));
        Assert.That(workCounter.ReverseDegreeEdgeVisits, Is.EqualTo(8));
        Assert.That(workCounter.ReverseFillEdgeVisits, Is.EqualTo(8));
        Assert.That(workCounter.FirstPassVertexVisits, Is.EqualTo(8));
        Assert.That(workCounter.FirstPassEdgeVisits, Is.EqualTo(8));
        Assert.That(workCounter.SecondPassVertexVisits, Is.EqualTo(8));
        Assert.That(workCounter.SecondPassEdgeVisits, Is.EqualTo(8));
        Assert.That(workCounter.CandidateEvidenceChecks, Is.EqualTo(evidenceCount));
        Assert.That(workCounter.CandidateEdgePresenceChecks, Is.EqualTo(evidenceCount));
        Assert.That(workCounter.ComponentMembershipChecks, Is.EqualTo(evidenceCount));
        Assert.That(workCounter.CandidateEmissionCount, Is.EqualTo(evidenceCount / 2));
        Assert.That(workCounter.ReachabilitySearches, Is.Zero);
        Assert.That(workCounter.ReachabilityVisitedVertices, Is.Zero);
        Assert.That(workCounter.ReachabilityEdgeVisits, Is.Zero);
    }

    private static Dictionary<string, HashSet<string>> CreateGraphWithTailsAndSelfEdge()
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
            ["X"] = new(StringComparer.Ordinal) { "X" },
            ["Y"] = new(StringComparer.Ordinal),
        };
    }

    private static Dictionary<string, HashSet<string>> CreateScaleGraph()
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

    private static CycleCandidateEvidence[] CreateScaleEvidence(int evidenceCount)
    {
        ArchitectureBaselineCandidate[] candidates =
        [
            Candidate("strict_cycles", "cycle-a", 0),
            Candidate("audit_cycles", "cycle-b", 1),
            Candidate("strict_cycles", "tail-a", 2),
            Candidate("audit_cycles", "tail-b", 3),
        ];
        (string Source, string Target)[] edges = [("A", "B"), ("G", "H"), ("C", "D"), ("E", "F")];

        return Enumerable.Range(0, evidenceCount)
            .Select(index => new CycleCandidateEvidence(
                edges[index % edges.Length].Source,
                edges[index % edges.Length].Target,
                candidates[index % candidates.Length]))
            .ToArray();
    }

    private static ArchitectureBaselineCandidate Candidate(string group, string contractId, int occurrence)
    {
        string sourceType = $"Synthetic.Source{occurrence}";
        string targetType = $"Synthetic.Target{occurrence}";
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
}
