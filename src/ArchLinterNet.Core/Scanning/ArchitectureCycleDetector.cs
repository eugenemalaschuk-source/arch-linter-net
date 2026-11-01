namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureCycleDetector
{
    internal static IReadOnlyCollection<string> FindCycles(
        Dictionary<string, HashSet<string>> graph,
        ArchitectureCycleDetectorWorkCounter? workCounter = null)
    {
        string[] nodes = graph.Keys.OrderBy(key => key).ToArray();
        if (nodes.Length == 0)
        {
            return Array.Empty<string>();
        }

        var nodeIndices = nodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index, StringComparer.Ordinal);
        int[][] adjacency = BuildOrderedAdjacency(
            graph,
            nodes,
            nodeIndices,
            workCounter,
            out bool[] selfEdges,
            out long edgeCount);
        var componentIndex =
            ArchitectureStronglyConnectedComponentIndex.Create(
                nodes,
                nodeIndices,
                adjacency,
                edgeCount,
                workCounter);
        int[] componentByNode = componentIndex.ComponentByNode;
        bool[] cyclicComponents = FindCyclicComponents(
            selfEdges,
            componentIndex.ComponentByNode,
            componentIndex.ComponentSizes);
        int[][] cycleAdjacency = BuildCycleAdjacency(
            adjacency,
            componentIndex.ComponentByNode,
            cyclicComponents,
            workCounter);

        HashSet<string> cycles = new();
        bool[] pathVisited = new bool[nodes.Length];
        List<int> path = [];
        for (int start = 0; start < nodes.Length; start++)
        {
            if (!cyclicComponents[componentByNode[start]])
            {
                continue;
            }

            EnumerateCyclesFrom(
                start,
                start,
                nodes,
                componentIndex.ComponentByNode,
                cycleAdjacency,
                pathVisited,
                path,
                cycles,
                workCounter);
        }

        return cycles.OrderBy(cycle => cycle).ToArray();
    }

    private static int[][] BuildOrderedAdjacency(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyList<string> nodes,
        IReadOnlyDictionary<string, int> nodeIndices,
        ArchitectureCycleDetectorWorkCounter? workCounter,
        out bool[] selfEdges,
        out long edgeCount)
    {
        int[][] adjacency = new int[nodes.Count][];
        selfEdges = new bool[nodes.Count];
        edgeCount = 0;
        for (int source = 0; source < nodes.Count; source++)
        {
            HashSet<string> targetLayers = graph[nodes[source]];
            int[] targets = new int[targetLayers.Count];
            int targetIndex = 0;
            foreach (string target in targetLayers.OrderBy(target => target))
            {
                targets[targetIndex++] = nodeIndices[target];
                if (workCounter is not null)
                {
                    workCounter.AdjacencyEdgeVisits++;
                }

                if (target == nodes[source])
                {
                    selfEdges[source] = true;
                }
            }

            adjacency[source] = targets;
            edgeCount += targets.Length;
        }

        if (workCounter is not null)
        {
            workCounter.AdjacencyListsBuilt = nodes.Count;
            workCounter.AdjacencyListsSorted = nodes.Count;
        }

        return adjacency;
    }

    private static bool[] FindCyclicComponents(
        IReadOnlyList<bool> selfEdges,
        IReadOnlyList<int> componentByNode,
        IReadOnlyList<int> componentSizes)
    {
        bool[] cyclicComponents = new bool[componentSizes.Count];
        for (int component = 0; component < componentSizes.Count; component++)
        {
            cyclicComponents[component] = componentSizes[component] > 1;
        }

        for (int node = 0; node < selfEdges.Count; node++)
        {
            if (cyclicComponents[componentByNode[node]])
            {
                continue;
            }

            if (selfEdges[node])
            {
                cyclicComponents[componentByNode[node]] = true;
            }
        }

        return cyclicComponents;
    }

    private static int[][] BuildCycleAdjacency(
        IReadOnlyList<int[]> adjacency,
        IReadOnlyList<int> componentByNode,
        IReadOnlyList<bool> cyclicComponents,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        int[][] cycleAdjacency = new int[adjacency.Count][];
        for (int source = 0; source < adjacency.Count; source++)
        {
            int component = componentByNode[source];
            if (!cyclicComponents[component])
            {
                cycleAdjacency[source] = Array.Empty<int>();
                continue;
            }

            int[] targets = adjacency[source];
            int inComponentCount = 0;
            foreach (int target in targets)
            {
                if (workCounter is not null)
                {
                    workCounter.CycleAdjacencyFilterEdgeVisits++;
                }

                if (componentByNode[target] == component)
                {
                    inComponentCount++;
                }
            }

            if (inComponentCount == targets.Length)
            {
                cycleAdjacency[source] = targets;
                continue;
            }

            int[] internalTargets = new int[inComponentCount];
            int targetIndex = 0;
            foreach (int target in targets)
            {
                if (workCounter is not null)
                {
                    workCounter.CycleAdjacencyCopyEdgeVisits++;
                }

                if (componentByNode[target] == component)
                {
                    internalTargets[targetIndex++] = target;
                }
            }

            cycleAdjacency[source] = internalTargets;
        }

        return cycleAdjacency;
    }

    private static void EnumerateCyclesFrom(
        int start,
        int current,
        IReadOnlyList<string> nodes,
        IReadOnlyList<int> componentByNode,
        IReadOnlyList<int[]> cycleAdjacency,
        bool[] visited,
        List<int> path,
        HashSet<string> cycles,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        visited[current] = true;
        path.Add(current);
        if (workCounter is not null)
        {
            workCounter.CycleSearchVertexVisits++;
        }

        foreach (int next in cycleAdjacency[current])
        {
            if (workCounter is not null)
            {
                workCounter.CycleSearchEdgeCandidates++;
            }

            if (next == start)
            {
                if (cycles.Add(FormatCycle(path, start, nodes)) && workCounter is not null)
                {
                    workCounter.EmittedCycleCount++;
                }

                continue;
            }

            if (componentByNode[next] == componentByNode[start] && !visited[next])
            {
                EnumerateCyclesFrom(
                    start,
                    next,
                    nodes,
                    componentByNode,
                    cycleAdjacency,
                    visited,
                    path,
                    cycles,
                    workCounter);
            }
        }

        path.RemoveAt(path.Count - 1);
        visited[current] = false;
    }

    private static string FormatCycle(IReadOnlyList<int> path, int start, IReadOnlyList<string> nodes)
    {
        string[] names = new string[path.Count + 1];
        for (int index = 0; index < path.Count; index++)
        {
            names[index] = nodes[path[index]];
        }

        names[^1] = nodes[start];
        return string.Join(" -> ", names);
    }
}

internal sealed class ArchitectureCycleDetectorWorkCounter : ArchitectureStronglyConnectedComponentIndexWorkCounter
{
    internal int AdjacencyListsSorted { get; set; }

    internal long CycleAdjacencyFilterEdgeVisits { get; set; }

    internal long CycleAdjacencyCopyEdgeVisits { get; set; }

    internal long CycleSearchVertexVisits { get; set; }

    internal long CycleSearchEdgeCandidates { get; set; }

    internal long EmittedCycleCount { get; set; }
}
