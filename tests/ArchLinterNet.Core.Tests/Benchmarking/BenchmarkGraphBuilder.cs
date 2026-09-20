namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkGraphBuilder
{
    public static IReadOnlyList<BenchmarkProjectNode> CreateProjects(int projectCount)
    {
        return Enumerable.Range(0, projectCount)
            .Select(index => new BenchmarkProjectNode(
                ProjectId(index),
                $"Synthetic.Project{index + 1:000}"))
            .ToList();
    }

    public static IReadOnlyList<BenchmarkProjectEdge> CreateEdges(
        BenchmarkTopologyShape shape,
        IReadOnlyList<BenchmarkProjectNode> projects)
    {
        return shape switch
        {
            BenchmarkTopologyShape.Linear => Linear(projects),
            BenchmarkTopologyShape.WideFanOutFanIn => Wide(projects),
            BenchmarkTopologyShape.Diamond => Diamond(projects),
            BenchmarkTopologyShape.Dense => Dense(projects),
            BenchmarkTopologyShape.CyclicScc => Cyclic(projects),
            BenchmarkTopologyShape.ManyProjectsFewTypes => Linear(projects),
            BenchmarkTopologyShape.FewProjectsManyTypes => Linear(projects),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown benchmark topology shape."),
        };
    }

    public static IReadOnlyList<BenchmarkProjectEdge> CreateEdges(
        BenchmarkTopologyShape shape,
        IReadOnlyList<BenchmarkProjectNode> projects,
        int referencesPerProject)
    {
        List<BenchmarkProjectEdge> edges = CreateEdges(shape, projects).ToList();
        if (referencesPerProject <= 2 || projects.Count < 2)
        {
            return edges;
        }

        HashSet<(string From, string To)> existing = edges
            .Select(edge => (edge.FromProjectId, edge.ToProjectId))
            .ToHashSet();
        for (int from = 0; from < projects.Count; from++)
        {
            int current = edges.Count(edge => edge.FromProjectId == projects[from].Id);
            for (int distance = 1; current < referencesPerProject && from + distance < projects.Count; distance++)
            {
                int to = from + distance;
                if (!existing.Add((projects[from].Id, projects[to].Id)))
                {
                    continue;
                }

                edges.Add(new BenchmarkProjectEdge(projects[from].Id, projects[to].Id));
                current++;
            }
        }

        return edges
            .OrderBy(edge => edge.FromProjectId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ToProjectId, StringComparer.Ordinal)
            .ToList();
    }

    public static BenchmarkTopologySummary Summarize(
        IReadOnlyList<BenchmarkProjectNode> projects,
        IReadOnlyList<BenchmarkProjectEdge> edges,
        BenchmarkTopologyShape shape)
    {
        Dictionary<string, List<string>> adjacency = projects.ToDictionary(
            project => project.Id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (BenchmarkProjectEdge edge in edges)
        {
            adjacency[edge.FromProjectId].Add(edge.ToProjectId);
        }

        int componentCount = CountStronglyConnectedComponents(adjacency);
        int alternatePaths = shape == BenchmarkTopologyShape.Diamond ? 2 : 0;
        return new BenchmarkTopologySummary
        {
            ProjectCount = projects.Count,
            ReferenceEdgeCount = edges.Count,
            StronglyConnectedComponentCount = componentCount,
            ContainsCycle = componentCount < projects.Count || edges.Any(edge => edge.FromProjectId == edge.ToProjectId),
            AlternatePathCount = alternatePaths,
        };
    }

    public static string ProjectId(int index) => $"project-{index + 1:000}";

    private static IReadOnlyList<BenchmarkProjectEdge> Linear(IReadOnlyList<BenchmarkProjectNode> projects)
    {
        return projects
            .Zip(projects.Skip(1), (from, to) => new BenchmarkProjectEdge(from.Id, to.Id))
            .ToList();
    }

    private static IReadOnlyList<BenchmarkProjectEdge> Wide(IReadOnlyList<BenchmarkProjectNode> projects)
    {
        BenchmarkProjectNode source = projects[0];
        BenchmarkProjectNode sink = projects[^1];
        var edges = new List<BenchmarkProjectEdge>();
        foreach (BenchmarkProjectNode middle in projects.Skip(1).SkipLast(1))
        {
            edges.Add(new BenchmarkProjectEdge(source.Id, middle.Id));
            edges.Add(new BenchmarkProjectEdge(middle.Id, sink.Id));
        }

        return edges;
    }

    private static IReadOnlyList<BenchmarkProjectEdge> Diamond(IReadOnlyList<BenchmarkProjectNode> projects)
    {
        BenchmarkProjectNode source = projects[0];
        BenchmarkProjectNode left = projects[1];
        BenchmarkProjectNode right = projects[2];
        BenchmarkProjectNode sink = projects[3];
        var edges = new List<BenchmarkProjectEdge>
        {
            new(source.Id, left.Id),
            new(source.Id, right.Id),
            new(left.Id, sink.Id),
            new(right.Id, sink.Id),
        };

        for (int i = 4; i < projects.Count; i++)
        {
            edges.Add(new BenchmarkProjectEdge(sink.Id, projects[i].Id));
            if (i < projects.Count - 1)
            {
                edges.Add(new BenchmarkProjectEdge(projects[i].Id, projects[i + 1].Id));
            }
        }

        return edges;
    }

    private static IReadOnlyList<BenchmarkProjectEdge> Dense(IReadOnlyList<BenchmarkProjectNode> projects)
    {
        var edges = new List<BenchmarkProjectEdge>();
        for (int from = 0; from < projects.Count; from++)
        {
            for (int to = from + 1; to < projects.Count; to++)
            {
                edges.Add(new BenchmarkProjectEdge(projects[from].Id, projects[to].Id));
            }
        }

        return edges;
    }

    private static IReadOnlyList<BenchmarkProjectEdge> Cyclic(IReadOnlyList<BenchmarkProjectNode> projects)
    {
        return projects
            .Select((project, index) => new BenchmarkProjectEdge(project.Id, projects[(index + 1) % projects.Count].Id))
            .ToList();
    }

    private static int CountStronglyConnectedComponents(Dictionary<string, List<string>> adjacency)
    {
        int index = 0;
        int componentCount = 0;
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string node)
        {
            indexes[node] = index;
            lowLinks[node] = index++;
            stack.Push(node);
            onStack.Add(node);
            foreach (string target in adjacency[node])
            {
                if (!indexes.ContainsKey(target))
                {
                    Visit(target);
                    lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                }
                else if (onStack.Contains(target))
                {
                    lowLinks[node] = Math.Min(lowLinks[node], indexes[target]);
                }
            }

            if (lowLinks[node] != indexes[node])
            {
                return;
            }

            while (true)
            {
                string member = stack.Pop();
                onStack.Remove(member);
                if (member == node)
                {
                    break;
                }
            }

            componentCount++;
        }

        foreach (string node in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            if (!indexes.ContainsKey(node))
            {
                Visit(node);
            }
        }

        return componentCount;
    }
}
