namespace ArchLinterNet.Core.Scanning;

internal abstract class ArchitectureStronglyConnectedComponentIndexWorkCounter
{
    internal int PreparedVertexCount { get; set; }

    internal long PreparedEdgeCount { get; set; }

    internal int AdjacencyListsBuilt { get; set; }

    internal long AdjacencyEdgeVisits { get; set; }

    internal long ReverseDegreeEdgeVisits { get; set; }

    internal long ReverseFillEdgeVisits { get; set; }

    internal int FirstPassVertexVisits { get; set; }

    internal long FirstPassEdgeVisits { get; set; }

    internal int SecondPassVertexVisits { get; set; }

    internal long SecondPassEdgeVisits { get; set; }
}

internal sealed class ArchitectureStronglyConnectedComponentIndex
{
    private readonly IReadOnlyDictionary<string, int> _nodeIndices;

    private ArchitectureStronglyConnectedComponentIndex(
        IReadOnlyDictionary<string, int> nodeIndices,
        int[] componentByNode,
        int[] componentSizes)
    {
        _nodeIndices = nodeIndices;
        ComponentByNode = componentByNode;
        ComponentSizes = componentSizes;
    }

    internal int[] ComponentByNode { get; }

    internal int[] ComponentSizes { get; }

    internal bool AreInSameComponent(string source, string target)
    {
        return _nodeIndices.TryGetValue(source, out int sourceIndex)
            && _nodeIndices.TryGetValue(target, out int targetIndex)
            && ComponentByNode[sourceIndex] == ComponentByNode[targetIndex];
    }

    internal static ArchitectureStronglyConnectedComponentIndex Create(
        IReadOnlyList<string> nodes,
        IReadOnlyDictionary<string, int> nodeIndices,
        IReadOnlyList<int[]> adjacency,
        long edgeCount,
        ArchitectureStronglyConnectedComponentIndexWorkCounter? workCounter = null)
    {
        if (workCounter is not null)
        {
            workCounter.PreparedVertexCount = nodes.Count;
            workCounter.PreparedEdgeCount = edgeCount;
        }

        int[][] reverseAdjacency = BuildReverseAdjacency(adjacency, workCounter);
        List<int> finishingOrder = FindFinishingOrder(adjacency, workCounter);
        (int[] componentByNode, int[] componentSizes) = AssignComponents(
            reverseAdjacency,
            finishingOrder,
            workCounter);

        return new ArchitectureStronglyConnectedComponentIndex(nodeIndices, componentByNode, componentSizes);
    }

    internal static ArchitectureStronglyConnectedComponentIndex CreateFromGraph(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        ArchitectureStronglyConnectedComponentIndexWorkCounter? workCounter = null)
    {
        string[] nodes = graph.Keys.ToArray();
        var nodeIndices = nodes
            .Select((node, index) => (node, index))
            .ToDictionary(item => item.node, item => item.index, StringComparer.Ordinal);
        int[][] adjacency = new int[nodes.Length][];
        long edgeCount = 0;

        for (int source = 0; source < nodes.Length; source++)
        {
            HashSet<string> targets = graph[nodes[source]];
            int[] targetIndices = new int[targets.Count];
            int targetIndex = 0;
            foreach (string target in targets)
            {
                targetIndices[targetIndex++] = nodeIndices[target];
                edgeCount++;
                if (workCounter is not null)
                {
                    workCounter.AdjacencyEdgeVisits++;
                }
            }

            adjacency[source] = targetIndices;
            if (workCounter is not null)
            {
                workCounter.AdjacencyListsBuilt++;
            }
        }

        return Create(nodes, nodeIndices, adjacency, edgeCount, workCounter);
    }

    private static int[][] BuildReverseAdjacency(
        IReadOnlyList<int[]> adjacency,
        ArchitectureStronglyConnectedComponentIndexWorkCounter? workCounter)
    {
        int[] inDegree = new int[adjacency.Count];
        foreach (int[] targets in adjacency)
        {
            foreach (int target in targets)
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
        ArchitectureStronglyConnectedComponentIndexWorkCounter? workCounter)
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
        ArchitectureStronglyConnectedComponentIndexWorkCounter? workCounter)
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
}
