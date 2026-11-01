using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureCycleBaselineCandidateRecorder
{
    private readonly List<ArchitectureBaselineCandidate> _candidates = new();

    public IReadOnlyList<ArchitectureBaselineCandidate> Candidates => _candidates;

    internal List<ArchitectureBaselineCandidate> CandidateStore => _candidates;

    public void Record(
        bool enabled,
        IReadOnlyDictionary<string, HashSet<string>> fullGraph,
        IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence,
        ArchitectureCycleBaselineCandidateRecorderWorkCounter? workCounter = null)
    {
        if (!enabled || candidateEvidence.Count == 0)
        {
            return;
        }

        // fullGraph carries every observed edge, live or suppressed (see CycleChecker.Result):
        // component membership must be evaluated against the true reference structure, not the
        // live-only graph, or a baseline-suppressed edge whose cycle partner is also suppressed
        // could never prove the cycle still exists once neither edge remains in a live-only graph.
        if (workCounter is not null)
        {
            workCounter.ComponentIndexBuildCount++;
        }

        // CycleChecker adds each edge to FullGraph before invoking observeCandidate. Production
        // evidence therefore always has a matching full-graph edge; this guard fails closed for
        // inconsistent evidence supplied by another caller.
        var componentIndex =
            ArchitectureStronglyConnectedComponentIndex.CreateFromGraph(fullGraph, workCounter);

        foreach (CycleCandidateEvidence evidence in candidateEvidence)
        {
            if (workCounter is not null)
            {
                workCounter.CandidateEvidenceChecks++;
                workCounter.CandidateEdgePresenceChecks++;
            }

            if (!fullGraph.TryGetValue(evidence.SourceLayerName, out HashSet<string>? targets)
                || !targets.Contains(evidence.TargetLayerName))
            {
                continue;
            }

            if (workCounter is not null)
            {
                workCounter.ComponentMembershipChecks++;
            }

            if (!componentIndex.AreInSameComponent(evidence.SourceLayerName, evidence.TargetLayerName))
            {
                continue;
            }

            if (workCounter is not null)
            {
                workCounter.CandidateEmissionCount++;
            }

            _candidates.Add(evidence.Candidate);
        }
    }
}

internal sealed class ArchitectureCycleBaselineCandidateRecorderWorkCounter
    : ArchitectureStronglyConnectedComponentIndexWorkCounter
{
    internal long CandidateEvidenceChecks { get; set; }

    internal long CandidateEdgePresenceChecks { get; set; }

    internal long ComponentMembershipChecks { get; set; }

    internal long CandidateEmissionCount { get; set; }

    internal long ReachabilitySearches => 0;

    internal long ReachabilityNodePops => 0;

    internal long ReachabilityVisitedVertices => 0;

    internal long ReachabilityEdgeVisits => 0;

    internal int ComponentIndexBuildCount { get; set; }

}
