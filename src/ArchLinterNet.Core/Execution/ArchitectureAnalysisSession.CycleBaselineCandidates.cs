using ArchLinterNet.Core.Execution.Checkers;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private void AddCycleBaselineCandidates(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence)
    {
        if (!EnableUnmatchedIgnoreTracking)
        {
            return;
        }

        foreach (CycleCandidateEvidence evidence in candidateEvidence)
        {
            if (EdgeParticipatesInCycle(graph, evidence.SourceLayerName, evidence.TargetLayerName))
            {
                _baselineCandidates.Add(evidence.Candidate);
            }
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
