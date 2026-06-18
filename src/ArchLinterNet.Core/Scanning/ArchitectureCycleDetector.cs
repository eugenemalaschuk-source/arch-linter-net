namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureCycleDetector
{
    public static IReadOnlyCollection<string> FindCycles(Dictionary<string, HashSet<string>> graph)
    {
        return graph.Keys
            .OrderBy(key => key)
            .SelectMany(start =>
            {
                Stack<string> path = new();
                HashSet<string> visited = new(StringComparer.Ordinal);

                return FindCyclesFrom(start, start, graph, visited, path);
            })
            .Distinct()
            .OrderBy(cycle => cycle)
            .ToArray();
    }

    private static IEnumerable<string> FindCyclesFrom(
        string start,
        string current,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        Stack<string> path)
    {
        visited.Add(current);
        path.Push(current);

        foreach (string next in graph[current].OrderBy(layer => layer))
        {
            if (next == start)
            {
                string[] cycle = path.Reverse().Concat(new[] { start }).ToArray();
                yield return string.Join(" -> ", cycle);
                continue;
            }

            if (!visited.Contains(next))
            {
                foreach (string cycle in FindCyclesFrom(start, next, graph, visited, path))
                {
                    yield return cycle;
                }
            }
        }

        path.Pop();
        visited.Remove(current);
    }
}
