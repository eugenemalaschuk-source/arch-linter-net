namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureCycleDetector
{
    public static IReadOnlyCollection<string> FindCycles(Dictionary<string, HashSet<string>> graph)
    {
        return FindCycles(graph, workCounter: null);
    }

    internal static IReadOnlyCollection<string> FindCycles(
        Dictionary<string, HashSet<string>> graph,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        string[] nodes = graph.Keys.OrderBy(key => key).ToArray();
        if (nodes.Length == 0)
        {
            return Array.Empty<string>();
        }

        Dictionary<string, int> nodeIndices = nodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index, StringComparer.Ordinal);
        int[][] adjacency = BuildOrderedAdjacency(graph, nodes, nodeIndices, workCounter, out bool[] selfEdges);
        int[][] reverseAdjacency = BuildReverseAdjacency(adjacency, workCounter);
        List<int> finishingOrder = FindFinishingOrder(adjacency, workCounter);
        (int[] componentByNode, int[] componentSizes) = AssignComponents(reverseAdjacency, finishingOrder, workCounter);
        bool[] cyclicComponents = FindCyclicComponents(selfEdges, componentByNode, componentSizes);
        int[][] cycleAdjacency = BuildCycleAdjacency(adjacency, componentByNode, cyclicComponents, workCounter);

        HashSet<string> cycles = new();
        bool[] pathVisited = new bool[nodes.Length];
        List<int> path = [];
        for (int start = 0; start < nodes.Length; start++)
        {
            if (!cyclicComponents[componentByNode[start]])
            {
                continue;
            }

            EnumerateCyclesFrom(start, start, nodes, componentByNode, cycleAdjacency, pathVisited, path, cycles, workCounter);
        }

        return cycles.OrderBy(cycle => cycle).ToArray();
    }

    private static int[][] BuildOrderedAdjacency(
        Dictionary<string, HashSet<string>> graph,
        IReadOnlyList<string> nodes,
        IReadOnlyDictionary<string, int> nodeIndices,
        ArchitectureCycleDetectorWorkCounter? workCounter,
        out bool[] selfEdges)
    {
        int[][] adjacency = new int[nodes.Count][];
        selfEdges = new bool[nodes.Count];
        long edgeCount = 0;
        for (int source = 0; source < nodes.Count; source++)
        {
            HashSet<string> targetLayers = graph[nodes[source]];
            int[] targets = new int[targetLayers.Count];
            int targetIndex = 0;
            foreach (string target in targetLayers.OrderBy(target => target))
            {
                targets[targetIndex++] = nodeIndices[target];
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
            workCounter.PreparedVertexCount = nodes.Count;
            workCounter.PreparedEdgeCount = edgeCount;
            workCounter.AdjacencyListsSorted = nodes.Count;
        }

        return adjacency;
    }

    private static int[][] BuildReverseAdjacency(
        IReadOnlyList<int[]> adjacency,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        int[] inDegree = new int[adjacency.Count];
        for (int source = 0; source < adjacency.Count; source++)
        {
            foreach (int target in adjacency[source])
            {
                inDegree[target]++;
                if (workCounter is not null)
                {
                    workCounter.ReverseDegreeEdgeVisits++;
                }
            }
        }

        int[][] reverseAdjacency = new int[adjacency.Count][];
        int[] nextReverseIndex = new int[adjacency.Count];
        for (int node = 0; node < adjacency.Count; node++)
        {
            reverseAdjacency[node] = new int[inDegree[node]];
        }

        for (int source = 0; source < adjacency.Count; source++)
        {
            foreach (int target in adjacency[source])
            {
                reverseAdjacency[target][nextReverseIndex[target]++] = source;
                if (workCounter is not null)
                {
                    workCounter.ReverseFillEdgeVisits++;
                }
            }
        }

        return reverseAdjacency;
    }

    private static List<int> FindFinishingOrder(
        IReadOnlyList<int[]> adjacency,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        bool[] visited = new bool[adjacency.Count];
        int[] nextNeighborIndices = new int[adjacency.Count];
        List<int> finishingOrder = new(adjacency.Count);
        Stack<int> pending = new();

        for (int root = 0; root < adjacency.Count; root++)
        {
            if (visited[root])
            {
                continue;
            }

            visited[root] = true;
            pending.Push(root);
            if (workCounter is not null)
            {
                workCounter.FirstPassVertexVisits++;
            }

            while (pending.Count > 0)
            {
                int source = pending.Peek();
                int neighborIndex = nextNeighborIndices[source];
                if (neighborIndex < adjacency[source].Length)
                {
                    int target = adjacency[source][neighborIndex];
                    nextNeighborIndices[source] = neighborIndex + 1;
                    if (workCounter is not null)
                    {
                        workCounter.FirstPassEdgeVisits++;
                    }

                    if (!visited[target])
                    {
                        visited[target] = true;
                        pending.Push(target);
                        if (workCounter is not null)
                        {
                            workCounter.FirstPassVertexVisits++;
                        }
                    }

                    continue;
                }

                pending.Pop();
                finishingOrder.Add(source);
            }
        }

        return finishingOrder;
    }

    private static (int[] ComponentByNode, int[] ComponentSizes) AssignComponents(
        IReadOnlyList<int[]> reverseAdjacency,
        IReadOnlyList<int> finishingOrder,
        ArchitectureCycleDetectorWorkCounter? workCounter)
    {
        int[] componentByNode = Enumerable.Repeat(-1, reverseAdjacency.Count).ToArray();
        int[] componentSizes = new int[reverseAdjacency.Count];
        int componentCount = 0;
        Stack<int> pending = new();

        for (int orderIndex = finishingOrder.Count - 1; orderIndex >= 0; orderIndex--)
        {
            int root = finishingOrder[orderIndex];
            if (componentByNode[root] >= 0)
            {
                continue;
            }

            int component = componentCount++;
            componentByNode[root] = component;
            pending.Push(root);
            if (workCounter is not null)
            {
                workCounter.SecondPassVertexVisits++;
            }

            while (pending.TryPop(out int source))
            {
                componentSizes[component]++;
                foreach (int target in reverseAdjacency[source])
                {
                    if (workCounter is not null)
                    {
                        workCounter.SecondPassEdgeVisits++;
                    }

                    if (componentByNode[target] >= 0)
                    {
                        continue;
                    }

                    componentByNode[target] = component;
                    pending.Push(target);
                    if (workCounter is not null)
                    {
                        workCounter.SecondPassVertexVisits++;
                    }
                }
            }
        }

        return (componentByNode, componentSizes);
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

internal sealed class ArchitectureCycleDetectorWorkCounter
{
    internal int PreparedVertexCount { get; set; }

    internal long PreparedEdgeCount { get; set; }

    internal int AdjacencyListsSorted { get; set; }

    internal long ReverseDegreeEdgeVisits { get; set; }

    internal long ReverseFillEdgeVisits { get; set; }

    internal int FirstPassVertexVisits { get; set; }

    internal long FirstPassEdgeVisits { get; set; }

    internal int SecondPassVertexVisits { get; set; }

    internal long SecondPassEdgeVisits { get; set; }

    internal long CycleAdjacencyFilterEdgeVisits { get; set; }

    internal long CycleAdjacencyCopyEdgeVisits { get; set; }

    internal long CycleSearchVertexVisits { get; set; }

    internal long CycleSearchEdgeCandidates { get; set; }

    internal long EmittedCycleCount { get; set; }
}
