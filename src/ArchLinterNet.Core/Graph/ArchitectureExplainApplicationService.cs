using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

// Depends on the concrete ArchitectureGraphApplicationService (not just its interface) so it can
// reuse the internal BuildSession + richer ArchitectureDependencyGraphBuilder.Build overload,
// getting both the graph and the exact ArchitectureViolation instances behind each edge in a single
// contract-execution pass, rather than running contract execution a second time just to recover CEL
// expression participation. See design.md Decision D3 (publish-cel-diagnostics-docs).
public sealed class ArchitectureExplainApplicationService(ArchitectureGraphApplicationService graphApplicationService)
    : IArchitectureExplainApplicationService
{
    public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request)
    {
        if (request.Level == ArchitectureGraphLevel.Assembly)
        {
            throw new ArgumentException(
                "Assembly-level explain is not supported: assembly graphs only support direct-edge presence " +
                "checks, not path resolution. Use 'graph --level assembly' to inspect direct references instead.",
                nameof(request));
        }

        ArchitectureGraphRequest graphRequest = new()
        {
            PolicyPath = request.PolicyPath,
            Mode = request.Mode,
            Level = request.Level,
            ConditionSetName = request.ConditionSetName,
        };

        ArchitectureAnalysisSession session = graphApplicationService.BuildSession(graphRequest, out List<ArchitectureViolation> violations);
        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
            session, request.Level, violations,
            out IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>> edgeViolations);

        ArchitectureExplainOutcome outcome = FindShortestPath(graph, request.Source, request.Target);
        return outcome with { ExpressionParticipation = CollectExpressionParticipation(outcome.Path, edgeViolations) };
    }

    // Attributes CEL `when` predicate results to the resolved path's hops, using the exact
    // ArchitectureViolation instances the single contract-execution pass already produced - no
    // re-evaluation of any selector. Deduplicated by (ContractId, Source) since more than one
    // violation on the same hop can share the same contributing expression.
    private static IReadOnlyList<ExplainExpressionParticipation> CollectExpressionParticipation(
        IReadOnlyList<string>? path,
        IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>> edgeViolations)
    {
        if (path == null || path.Count < 2)
        {
            return Array.Empty<ExplainExpressionParticipation>();
        }

        List<ExplainExpressionParticipation> participation = new();
        HashSet<(string ContractId, string Source)> seen = new();

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!edgeViolations.TryGetValue((path[i], path[i + 1]), out IReadOnlyList<ArchitectureViolation>? hopViolations))
            {
                continue;
            }

            foreach (ArchitectureViolation violation in hopViolations)
            {
                ExpressionParticipation? whenExpression = GetWhenExpression(violation.Payload);
                if (whenExpression == null || violation.ContractId == null)
                {
                    continue;
                }

                if (!seen.Add((violation.ContractId, whenExpression.Source)))
                {
                    continue;
                }

                participation.Add(new ExplainExpressionParticipation(
                    violation.ContractId, whenExpression.Source, whenExpression.YamlPath, whenExpression.Result));
            }
        }

        return participation;
    }

    private static ExpressionParticipation? GetWhenExpression(IArchitectureDiagnosticPayload? payload) => payload switch
    {
        ContextDependencyPayload p => p.WhenExpression,
        ContextAllowOnlyPayload p => p.WhenExpression,
        LayoutConventionPayload p => p.WhenExpression,
        _ => null,
    };

    private static ArchitectureExplainOutcome FindShortestPath(
        ArchitectureDependencyGraph graph,
        string source,
        string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            return new ArchitectureExplainOutcome(source, target, new List<string> { source }, Array.Empty<string>());
        }

        Dictionary<string, List<(string Target, IReadOnlyList<string> ContractIds)>> adjacency = new(StringComparer.Ordinal);
        foreach (ArchitectureGraphEdge edge in graph.Edges)
        {
            if (!adjacency.TryGetValue(edge.SourceId, out List<(string Target, IReadOnlyList<string> ContractIds)>? neighbors))
            {
                neighbors = new List<(string Target, IReadOnlyList<string> ContractIds)>();
                adjacency[edge.SourceId] = neighbors;
            }

            neighbors.Add((edge.TargetId, edge.ContractIds));
        }

        Queue<string> queue = new();
        Dictionary<string, (string Previous, IReadOnlyList<string> ContractIds)> visited = new(StringComparer.Ordinal);
        queue.Enqueue(source);
        visited[source] = (string.Empty, Array.Empty<string>());

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out List<(string Target, IReadOnlyList<string> ContractIds)>? neighbors))
            {
                continue;
            }

            foreach ((string next, IReadOnlyList<string> contractIds) in neighbors.OrderBy(n => n.Target, StringComparer.Ordinal))
            {
                if (visited.ContainsKey(next))
                {
                    continue;
                }

                visited[next] = (current, contractIds);

                if (string.Equals(next, target, StringComparison.Ordinal))
                {
                    List<string> path = ReconstructPath(visited, source, target);
                    List<string> contractIdUnion = CollectContractIds(visited, path);
                    return new ArchitectureExplainOutcome(source, target, path, contractIdUnion);
                }

                queue.Enqueue(next);
            }
        }

        return new ArchitectureExplainOutcome(source, target, null, Array.Empty<string>());
    }

    private static List<string> ReconstructPath(
        Dictionary<string, (string Previous, IReadOnlyList<string> ContractIds)> visited,
        string source,
        string target)
    {
        List<string> path = new() { target };
        string current = target;

        while (!string.Equals(current, source, StringComparison.Ordinal))
        {
            current = visited[current].Previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static List<string> CollectContractIds(
        Dictionary<string, (string Previous, IReadOnlyList<string> ContractIds)> visited,
        List<string> path)
    {
        HashSet<string> contractIds = new(StringComparer.Ordinal);

        for (int i = 1; i < path.Count; i++)
        {
            foreach (string contractId in visited[path[i]].ContractIds)
            {
                contractIds.Add(contractId);
            }
        }

        return contractIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }
}
