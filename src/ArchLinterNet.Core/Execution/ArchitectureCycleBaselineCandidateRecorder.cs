using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureCycleBaselineCandidateRecorder
{
    private readonly List<ArchitectureBaselineCandidate> _candidates = new();

    public IReadOnlyList<ArchitectureBaselineCandidate> Candidates => _candidates;

    internal List<ArchitectureBaselineCandidate> CandidateStore => _candidates;

    public void Record(
        bool enabled,
        IReadOnlyDictionary<string, HashSet<string>> fullGraph,
        IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence)
    {
        if (!enabled)
        {
            return;
        }

        // fullGraph carries every observed edge, live or suppressed (see CycleChecker.Result):
        // reachability must be evaluated against the true reference structure, not the live-only
        // graph, or a baseline-suppressed edge whose cycle partner is also suppressed could never
        // prove the cycle still exists once neither edge remains in a live-only graph.
        foreach (CycleCandidateEvidence evidence in candidateEvidence.Where(
            evidence => EdgeParticipatesInCycle(fullGraph, evidence.SourceLayerName, evidence.TargetLayerName)))
        {
            _candidates.Add(evidence.Candidate);
        }
    }

    private static bool EdgeParticipatesInCycle(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        string sourceLayerName,
        string targetLayerName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(targetLayerName);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == sourceLayerName)
            {
                return true;
            }

            foreach (string next in graph[current])
            {
                pending.Push(next);
            }
        }

        return false;
    }
}
