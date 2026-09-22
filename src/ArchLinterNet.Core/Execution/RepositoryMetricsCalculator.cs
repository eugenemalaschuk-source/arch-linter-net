using System.Reflection;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Projects repository observability from one already-materialized analysis session. This is not a
// policy evaluator: none of these values participate in findings, health, or exit-code decisions.
internal static class RepositoryMetricsCalculator
{
    internal static RepositoryMetricsSnapshot Calculate(ArchitectureAnalysisSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        List<string> reasons = [];
        Type[] types = session.TypeIndex.AllTypes();
        if (!session.TypeIndex.HasCompleteTypeUniverse)
        {
            reasons.Add(RepositoryMetricsReasonCodes.IncompleteTypeUniverse);
        }

        // The source inventory is lazy because most policies do not need it. Metrics do: force
        // the existing fact index once so an otherwise source-unrelated policy still gets the
        // same readable-file and physical-line evidence without a second traversal.
        _ = session.SourceFileFactIndex.AllFacts;
        IReadOnlyDictionary<string, int> sourceFiles = session.SourceFileFactIndex.SourceFileLineCounts;
        if (session.SourceFileFactIndex.UnreadableSourceInputPaths.Count > 0)
        {
            reasons.Add(RepositoryMetricsReasonCodes.UnreadableSource);
        }

        ProjectGraph graph = BuildProjectGraph(session, reasons);
        TarjanResult scc = TarjanResult.Build(graph.Adjacency);
        RepositoryStructureMetrics structure = BuildStructureMetrics(graph, scc);
        RepositoryCouplingMetrics coupling = BuildCouplingMetrics(graph);

        RepositoryMetricsAvailability availability = reasons.Count == 0
            ? RepositoryMetricsAvailability.Complete
            : RepositoryMetricsAvailability.Partial;
        return new RepositoryMetricsSnapshot(
            RepositoryMetricsSnapshot.CurrentSchemaVersion,
            RepositoryMetricsSnapshot.CurrentKind,
            availability,
            reasons,
            new RepositorySizeMetrics(
                sourceFiles.Values.Sum(),
                sourceFiles.Count,
                graph.Projects.Count,
                types.Length,
                types.Count(IsPublicType)),
            coupling,
            structure);
    }

    private static ProjectGraph BuildProjectGraph(
        ArchitectureAnalysisSession session,
        List<string> reasons)
    {
        ProjectDiscoveryResult? discovery = session.Context.ProjectDiscovery;
        if (discovery is null)
        {
            reasons.Add(RepositoryMetricsReasonCodes.MissingProjectDiscovery);
            return BuildAssemblyFallback(session);
        }

        if (discovery.Diagnostics.Count > 0)
        {
            reasons.Add(RepositoryMetricsReasonCodes.DiscoveryDiagnostics);
        }

        ArchitectureDiscoveredProject[] projects = discovery.DiscoveredProjects
            .GroupBy(project => ProjectPathNormalizer.Normalize(project.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(project => project.AssemblyName, StringComparer.Ordinal).First())
            .OrderBy(project => ProjectPathNormalizer.Normalize(project.Path), StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> indexByPath = projects
            .Select((project, index) => (Path: ProjectPathNormalizer.Normalize(project.Path), index))
            .ToDictionary(item => item.Path, item => item.index, StringComparer.OrdinalIgnoreCase);
        List<ProjectNode> nodes = projects
            .Select(project => new ProjectNode(
                ProjectPathNormalizer.Normalize(project.Path),
                string.IsNullOrWhiteSpace(project.AssemblyName)
                    ? Path.GetFileNameWithoutExtension(project.Path)
                    : project.AssemblyName))
            .ToList();
        List<HashSet<int>> adjacency = nodes.Select(_ => new HashSet<int>()).ToList();

        for (int source = 0; source < projects.Length; source++)
        {
            foreach (ArchitectureDiscoveredProjectReference reference in projects[source].ProjectReferences)
            {
                string targetPath = NormalizeProjectPath(session.Context.RepositoryRoot, reference.Path);
                if (indexByPath.TryGetValue(targetPath, out int target))
                {
                    adjacency[source].Add(target);
                }
            }
        }

        return new ProjectGraph(nodes, adjacency);
    }

    private static string NormalizeProjectPath(string repositoryRoot, string path)
    {
        string candidate = path;
        if (Path.IsPathRooted(candidate))
        {
            try
            {
                candidate = Path.GetRelativePath(repositoryRoot, candidate);
            }
            catch (Exception) when (candidate.Length > 0)
            {
                // Keep the normalized original so an invalid external reference remains
                // deterministic and simply does not become an internal edge.
            }
        }

        return ProjectPathNormalizer.Normalize(candidate);
    }

    private static ProjectGraph BuildAssemblyFallback(ArchitectureAnalysisSession session)
    {
        Assembly[] assemblies = session.Context.TargetAssemblies
            .Distinct()
            .OrderBy(assembly => assembly.GetName().Name ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        ProjectNode[] projects = assemblies
            .Select(assembly =>
            {
                string name = assembly.GetName().Name ?? string.Empty;
                return new ProjectNode("assembly:" + name, name);
            })
            .ToArray();
        Dictionary<string, int> indexByName = projects
            .Select((project, index) => (project.Name, index))
            .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        List<HashSet<int>> adjacency = projects.Select(_ => new HashSet<int>()).ToList();
        for (int source = 0; source < assemblies.Length; source++)
        {
            foreach (AssemblyName reference in assemblies[source].GetReferencedAssemblies())
            {
                if (reference.Name is not null
                    && indexByName.TryGetValue(reference.Name, out int target))
                {
                    adjacency[source].Add(target);
                }
            }
        }

        return new ProjectGraph(projects, adjacency);
    }

    private static RepositoryCouplingMetrics BuildCouplingMetrics(ProjectGraph graph)
    {
        int projectCount = graph.Projects.Count;
        int[] fanIn = new int[projectCount];
        int[] fanOut = graph.Adjacency.Select(edges => edges.Count).ToArray();
        int dependencyCount = 0;
        for (int source = 0; source < projectCount; source++)
        {
            dependencyCount += graph.Adjacency[source].Count;
            foreach (int target in graph.Adjacency[source])
            {
                fanIn[target]++;
            }
        }

        RepositoryProjectCoupling[] projects = graph.Projects
            .Select((project, index) => new RepositoryProjectCoupling(
                project.Identity,
                project.Name,
                fanIn[index],
                fanOut[index],
                fanIn[index],
                fanOut[index],
                fanIn[index] + fanOut[index] == 0
                    ? 0d
                    : (double)fanOut[index] / (fanIn[index] + fanOut[index])))
            .ToArray();
        double density = projectCount <= 1
            ? 0d
            : (double)dependencyCount / (projectCount * (projectCount - 1));
        return new RepositoryCouplingMetrics(
            dependencyCount,
            projectCount == 0 ? 0d : (double)dependencyCount / projectCount,
            density,
            fanIn.Length == 0 ? 0 : fanIn.Max(),
            fanOut.Length == 0 ? 0 : fanOut.Max(),
            projects);
    }

    private static RepositoryStructureMetrics BuildStructureMetrics(ProjectGraph graph, TarjanResult scc)
    {
        int projectCount = graph.Projects.Count;
        int cyclicComponentCount = 0;
        int cyclicProjectCount = 0;
        int largestSccSize = scc.Components.Count == 0 ? 0 : scc.Components.Max(component => component.Count);
        foreach (IReadOnlyList<int> component in scc.Components)
        {
            bool selfLoop = component.Count == 1 && graph.Adjacency[component[0]].Contains(component[0]);
            if (component.Count > 1 || selfLoop)
            {
                cyclicComponentCount++;
                cyclicProjectCount += component.Count;
            }
        }

        double cyclicRatio = projectCount == 0 ? 0d : (double)cyclicProjectCount / projectCount;
        double largestRatio = projectCount == 0 ? 0d : (double)largestSccSize / projectCount;
        return new RepositoryStructureMetrics(
            MaxDepth(graph, scc),
            cyclicComponentCount,
            cyclicProjectCount,
            cyclicRatio,
            largestSccSize,
            largestRatio);
    }

    private static int MaxDepth(ProjectGraph graph, TarjanResult scc)
    {
        if (scc.Components.Count == 0)
        {
            return 0;
        }

        HashSet<(int Source, int Target)> edges = [];
        foreach (IReadOnlyList<int> component in scc.Components)
        {
            foreach (int source in component)
            {
                foreach (int target in graph.Adjacency[source])
                {
                    int sourceComponent = scc.ComponentByNode[source];
                    int targetComponent = scc.ComponentByNode[target];
                    if (sourceComponent != targetComponent)
                    {
                        edges.Add((sourceComponent, targetComponent));
                    }
                }
            }
        }

        List<HashSet<int>> outgoing = Enumerable.Range(0, scc.Components.Count)
            .Select(_ => new HashSet<int>())
            .ToList();
        int[] incoming = new int[scc.Components.Count];
        foreach ((int source, int target) in edges)
        {
            outgoing[source].Add(target);
            incoming[target]++;
        }

        Queue<int> ready = new(incoming
            .Select((count, index) => (count, index))
            .Where(item => item.count == 0)
            .Select(item => item.index));
        int[] depth = new int[scc.Components.Count];
        int maximum = 0;
        while (ready.TryDequeue(out int source))
        {
            foreach (int target in outgoing[source])
            {
                depth[target] = Math.Max(depth[target], depth[source] + 1);
                maximum = Math.Max(maximum, depth[target]);
                if (--incoming[target] == 0)
                {
                    ready.Enqueue(target);
                }
            }
        }

        return maximum;
    }

    private static bool IsPublicType(Type type)
    {
        try
        {
            return type.IsPublic || type.IsNestedPublic;
        }
        catch
        {
            return false;
        }
    }

    private sealed record ProjectNode(string Identity, string Name);

    private sealed record ProjectGraph(
        IReadOnlyList<ProjectNode> Projects,
        IReadOnlyList<HashSet<int>> Adjacency);

    private sealed class TarjanResult
    {
        private readonly IReadOnlyList<HashSet<int>> _adjacency;
        private readonly int[] _indices;
        private readonly int[] _lowLinks;
        private readonly bool[] _onStack;
        private readonly Stack<int> _stack = new();
        private int _nextIndex;

        private TarjanResult(IReadOnlyList<HashSet<int>> adjacency)
        {
            _adjacency = adjacency;
            _indices = Enumerable.Repeat(-1, adjacency.Count).ToArray();
            _lowLinks = new int[adjacency.Count];
            _onStack = new bool[adjacency.Count];
            Components = [];
            ComponentByNode = new int[adjacency.Count];
        }

        internal List<IReadOnlyList<int>> Components { get; }

        internal int[] ComponentByNode { get; }

        internal static TarjanResult Build(IReadOnlyList<HashSet<int>> adjacency)
        {
            var result = new TarjanResult(adjacency);
            for (int node = 0; node < adjacency.Count; node++)
            {
                if (result._indices[node] == -1)
                {
                    result.Visit(node);
                }
            }

            for (int component = 0; component < result.Components.Count; component++)
            {
                foreach (int node in result.Components[component])
                {
                    result.ComponentByNode[node] = component;
                }
            }

            return result;
        }

        private void Visit(int node)
        {
            _indices[node] = _nextIndex;
            _lowLinks[node] = _nextIndex++;
            _stack.Push(node);
            _onStack[node] = true;
            foreach (int target in _adjacency[node].OrderBy(static value => value))
            {
                if (_indices[target] == -1)
                {
                    Visit(target);
                    _lowLinks[node] = Math.Min(_lowLinks[node], _lowLinks[target]);
                }
                else if (_onStack[target])
                {
                    _lowLinks[node] = Math.Min(_lowLinks[node], _indices[target]);
                }
            }

            if (_lowLinks[node] != _indices[node])
            {
                return;
            }

            List<int> component = [];
            int member;
            do
            {
                member = _stack.Pop();
                _onStack[member] = false;
                component.Add(member);
            }
            while (member != node);
            component.Sort();
            Components.Add(component);
        }
    }
}
