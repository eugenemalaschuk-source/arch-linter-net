using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Evidence;

// Builds the graph only from canonical evidence produced by ingestion. In particular, it never
// revisits source spellings, raw paths, or rename decisions; it projects the settled evidence.
internal sealed class CoChangeGraphBuilder(HistoryAnalysisConfiguration configuration)
{
    public CoChangeGraph Build(
        IReadOnlyList<LogicalFile> files,
        IReadOnlyList<CommitEvidence> commits,
        IReadOnlyList<RenameComponent> renameComponents)
    {
        HistoryPathClassifier classifier = new(configuration);
        List<CoChangeVertex> vertices = BuildVertices(files, renameComponents, classifier);
        Dictionary<string, List<CoChangeVertex>> verticesByCommit = IndexVerticesByCommit(vertices);
        Dictionary<CoChangePairKey, CoChangePairAccumulator> pairs = [];
        List<CommitEvidence> orderedCommits = [.. commits];
        orderedCommits.Sort(static (left, right) => GitCommit.CompareCanonical(left.Commit, right.Commit));

        AddCommitEvidence(pairs, verticesByCommit, orderedCommits);
        AddTaskEvidence(pairs, verticesByCommit, orderedCommits);

        (IReadOnlyList<CoChangePair> allPairs, IReadOnlyList<CoChangePair> baseEdges) = BuildPairs(pairs);
        IReadOnlyList<CoChangeCluster> clusters = BuildClusters(baseEdges);
        return new CoChangeGraph(
            configuration.Weights.CoChange.Commit,
            configuration.Weights.CoChange.Task,
            configuration.Thresholds.CoChangeSignificance,
            vertices,
            allPairs,
            baseEdges,
            clusters);
    }

    private static List<CoChangeVertex> BuildVertices(
        IReadOnlyList<LogicalFile> files,
        IReadOnlyList<RenameComponent> renameComponents,
        HistoryPathClassifier classifier)
    {
        List<CoChangeVertex> vertices = [];
        foreach (LogicalFile file in files)
        {
            HistoryPathClassification classification = classifier.Classify(file.CanonicalPath);
            if (classification.IsIgnored)
            {
                continue;
            }

            HashSet<string> paths = new(file.Aliases, StringComparer.Ordinal) { file.CanonicalPath };
            IReadOnlyList<RenameComponent> relatedComponents = renameComponents
                .Where(component => component.Candidates.Any(candidate => paths.Contains(candidate.SourcePath) || paths.Contains(candidate.DestinationPath)))
                .OrderBy(static component => component.Index)
                .ToArray();
            vertices.Add(new CoChangeVertex(file, classification.Category, relatedComponents));
        }

        vertices.Sort(CompareVertices);
        return vertices;
    }

    private static Dictionary<string, List<CoChangeVertex>> IndexVerticesByCommit(IReadOnlyList<CoChangeVertex> vertices)
    {
        Dictionary<string, List<CoChangeVertex>> indexed = new(StringComparer.Ordinal);
        foreach (CoChangeVertex vertex in vertices)
        {
            foreach (FileEvent fileEvent in vertex.File.Events)
            {
                if (!indexed.TryGetValue(fileEvent.CommitId, out List<CoChangeVertex>? touching))
                {
                    touching = [];
                    indexed[fileEvent.CommitId] = touching;
                }

                touching.Add(vertex);
            }
        }

        foreach (List<CoChangeVertex> touching in indexed.Values)
        {
            touching.Sort(CompareVertices);
        }

        return indexed;
    }

    private static void AddCommitEvidence(
        Dictionary<CoChangePairKey, CoChangePairAccumulator> pairs,
        Dictionary<string, List<CoChangeVertex>> verticesByCommit,
        IReadOnlyList<CommitEvidence> commits)
    {
        foreach (CommitEvidence commit in commits)
        {
            if (!verticesByCommit.TryGetValue(commit.Commit.Id.Hex, out List<CoChangeVertex>? touching))
            {
                continue;
            }

            AddPairs(touching, pair => pair.CommitIds.Add(commit.Commit.Id.Hex), pairs);
        }
    }

    private static void AddTaskEvidence(
        Dictionary<CoChangePairKey, CoChangePairAccumulator> pairs,
        Dictionary<string, List<CoChangeVertex>> verticesByCommit,
        IReadOnlyList<CommitEvidence> commits)
    {
        Dictionary<TaskKey, HashSet<CoChangeVertex>> verticesByTask = [];
        foreach (CommitEvidence commit in commits)
        {
            if (!verticesByCommit.TryGetValue(commit.Commit.Id.Hex, out List<CoChangeVertex>? touching))
            {
                continue;
            }

            foreach (TaskKey key in commit.TaskKeys.Distinct())
            {
                if (!verticesByTask.TryGetValue(key, out HashSet<CoChangeVertex>? episode))
                {
                    episode = [];
                    verticesByTask[key] = episode;
                }

                foreach (CoChangeVertex vertex in touching)
                {
                    episode.Add(vertex);
                }
            }
        }

        foreach ((TaskKey key, HashSet<CoChangeVertex> episode) in verticesByTask.OrderBy(static entry => entry.Key))
        {
            List<CoChangeVertex> orderedEpisode = [.. episode];
            orderedEpisode.Sort(CompareVertices);
            AddPairs(orderedEpisode, pair => pair.TaskKeys.Add(key), pairs);
        }
    }

    private static void AddPairs(
        List<CoChangeVertex> vertices,
        Action<CoChangePairAccumulator> addEvidence,
        Dictionary<CoChangePairKey, CoChangePairAccumulator> pairs)
    {
        for (int first = 0; first < vertices.Count; first++)
        {
            for (int second = first + 1; second < vertices.Count; second++)
            {
                CoChangePairKey key = new(vertices[first].CanonicalPath, vertices[second].CanonicalPath);
                if (!pairs.TryGetValue(key, out CoChangePairAccumulator? pair))
                {
                    pair = new CoChangePairAccumulator(vertices[first], vertices[second]);
                    pairs[key] = pair;
                }

                addEvidence(pair);
            }
        }
    }

    private (IReadOnlyList<CoChangePair> AllPairs, IReadOnlyList<CoChangePair> BaseEdges) BuildPairs(
        IReadOnlyDictionary<CoChangePairKey, CoChangePairAccumulator> accumulators)
    {
        Dictionary<CoChangePairAccumulator, CoChangeComponents> components = NormalizeBaseEdges(accumulators.Values);
        Dictionary<CoChangePairAccumulator, int> ranks = RankBaseEdges(components);
        List<CoChangePair> pairs = [];
        foreach (CoChangePairAccumulator accumulator in accumulators.Values)
        {
            CoChangeComponents? normalized = components.TryGetValue(accumulator, out CoChangeComponents value)
                ? value
                : null;
            int? cohortRank = ranks.TryGetValue(accumulator, out int rank) ? rank : null;
            pairs.Add(new CoChangePair(
                accumulator.First,
                accumulator.Second,
                accumulator.Cohort,
                accumulator.CommitIds,
                accumulator.TaskKeys,
                normalized?.Commit,
                normalized?.Task,
                normalized?.Combined,
                cohortRank));
        }

        pairs.Sort(ComparePairs);
        IReadOnlyList<CoChangePair> baseEdges = pairs
            .Where(static pair => pair.IsBaseEdge)
            .OrderBy(static pair => pair.Cohort)
            .ThenBy(static pair => pair.CohortRank)
            .ToArray();
        return (pairs, baseEdges);
    }

    private Dictionary<CoChangePairAccumulator, CoChangeComponents> NormalizeBaseEdges(
        IEnumerable<CoChangePairAccumulator> allAccumulators)
    {
        Dictionary<CoChangePairAccumulator, CoChangeComponents> normalized = [];
        foreach (IGrouping<CoChangeCohort, CoChangePairAccumulator> group in allAccumulators
                     .Where(static accumulator => accumulator.CommitIds.Count > 0)
                     .GroupBy(static accumulator => accumulator.Cohort))
        {
            List<CoChangePairAccumulator> edges = [.. group];
            int maximumCommitCount = edges.Max(static edge => edge.CommitIds.Count);
            int maximumTaskCount = edges.Max(static edge => edge.TaskKeys.Count);
            foreach (CoChangePairAccumulator edge in edges)
            {
                decimal commit = Quantize((decimal)edge.CommitIds.Count / maximumCommitCount);
                decimal task = maximumTaskCount == 0
                    ? 0m
                    : Quantize((decimal)edge.TaskKeys.Count / maximumTaskCount);
                decimal combined = Quantize((configuration.Weights.CoChange.Commit * commit) + (configuration.Weights.CoChange.Task * task));
                normalized[edge] = new CoChangeComponents(commit, task, combined);
            }
        }

        return normalized;
    }

    private static Dictionary<CoChangePairAccumulator, int> RankBaseEdges(
        IReadOnlyDictionary<CoChangePairAccumulator, CoChangeComponents> components)
    {
        Dictionary<CoChangePairAccumulator, int> ranks = [];
        foreach (IGrouping<CoChangeCohort, CoChangePairAccumulator> group in components.Keys.GroupBy(static edge => edge.Cohort))
        {
            List<CoChangePairAccumulator> edges = [.. group];
            edges.Sort((left, right) => CompareRankedAccumulators(left, right, components));
            for (int index = 0; index < edges.Count; index++)
            {
                ranks[edges[index]] = index + 1;
            }
        }

        return ranks;
    }

    private List<CoChangeCluster> BuildClusters(IReadOnlyList<CoChangePair> baseEdges)
    {
        decimal? threshold = configuration.Thresholds.CoChangeSignificance;
        if (threshold is null)
        {
            return [];
        }

        List<CoChangeCluster> clusters = [];
        foreach (IGrouping<CoChangeCohort, CoChangePair> group in baseEdges
                     .Where(pair => pair.CombinedCoChange >= threshold.Value)
                     .GroupBy(static edge => edge.Cohort))
        {
            clusters.AddRange(BuildCohortClusters(group.Key, [.. group]));
        }

        clusters.Sort(CompareClusters);
        return clusters;
    }

    private static IEnumerable<CoChangeCluster> BuildCohortClusters(CoChangeCohort cohort, IReadOnlyList<CoChangePair> edges)
    {
        Dictionary<CoChangeVertex, List<CoChangePair>> adjacency = BuildAdjacency(edges);
        HashSet<CoChangeVertex> visited = [];
        List<CoChangeVertex> startVertices = [.. adjacency.Keys];
        startVertices.Sort(CompareVertices);
        foreach (CoChangeVertex start in startVertices)
        {
            if (!visited.Add(start))
            {
                continue;
            }

            List<CoChangeVertex> members = CollectComponent(start, adjacency, visited);
            if (members.Count < 2)
            {
                continue;
            }

            yield return BuildCluster(cohort, members, edges);
        }
    }

    private static Dictionary<CoChangeVertex, List<CoChangePair>> BuildAdjacency(IReadOnlyList<CoChangePair> edges)
    {
        Dictionary<CoChangeVertex, List<CoChangePair>> adjacency = [];
        foreach (CoChangePair edge in edges)
        {
            AddAdjacent(adjacency, edge.First, edge);
            AddAdjacent(adjacency, edge.Second, edge);
        }

        return adjacency;
    }

    private static List<CoChangeVertex> CollectComponent(
        CoChangeVertex start,
        Dictionary<CoChangeVertex, List<CoChangePair>> adjacency,
        HashSet<CoChangeVertex> visited)
    {
        List<CoChangeVertex> members = [];
        Stack<CoChangeVertex> pending = new();
        pending.Push(start);
        while (pending.Count > 0)
        {
            CoChangeVertex current = pending.Pop();
            members.Add(current);
            foreach (CoChangePair edge in adjacency[current])
            {
                CoChangeVertex neighbor = ReferenceEquals(edge.First, current) ? edge.Second : edge.First;
                if (visited.Add(neighbor))
                {
                    pending.Push(neighbor);
                }
            }
        }

        members.Sort(CompareVertices);
        return members;
    }

    private static CoChangeCluster BuildCluster(CoChangeCohort cohort, List<CoChangeVertex> members, IReadOnlyList<CoChangePair> edges)
    {
        HashSet<CoChangeVertex> memberSet = [.. members];
        List<CoChangePair> clusterEdges = edges
            .Where(edge => memberSet.Contains(edge.First) && memberSet.Contains(edge.Second))
            .OrderBy(static edge => edge, Comparer<CoChangePair>.Create(ComparePairs))
            .ToList();
        decimal maximum = clusterEdges.Max(static edge => edge.CombinedCoChange!.Value);
        decimal aggregate = Quantize(clusterEdges.Sum(static edge => edge.CombinedCoChange!.Value));
        return new CoChangeCluster(cohort, members, clusterEdges, maximum, aggregate);
    }

    private static void AddAdjacent(
        Dictionary<CoChangeVertex, List<CoChangePair>> adjacency,
        CoChangeVertex vertex,
        CoChangePair edge)
    {
        if (!adjacency.TryGetValue(vertex, out List<CoChangePair>? incident))
        {
            incident = [];
            adjacency[vertex] = incident;
        }

        incident.Add(edge);
    }

    private static int CompareVertices(CoChangeVertex left, CoChangeVertex right)
        => HistoryScalarValueComparer.Compare(left.CanonicalPath, right.CanonicalPath);

    private static int ComparePairs(CoChangePair left, CoChangePair right)
    {
        int byFirst = CompareVertices(left.First, right.First);
        return byFirst != 0 ? byFirst : CompareVertices(left.Second, right.Second);
    }

    private static int CompareClusters(CoChangeCluster left, CoChangeCluster right)
    {
        int byCohort = left.Cohort.CompareTo(right.Cohort);
        if (byCohort != 0)
        {
            return byCohort;
        }

        int byMaximum = right.Maximum.CompareTo(left.Maximum);
        if (byMaximum != 0)
        {
            return byMaximum;
        }

        int byAggregate = right.Aggregate.CompareTo(left.Aggregate);
        return byAggregate != 0 ? byAggregate : CompareVertices(left.Members[0], right.Members[0]);
    }

    private static decimal Quantize(decimal value) => decimal.Round(value, 9, MidpointRounding.ToEven);

    private static int CompareRankedAccumulators(
        CoChangePairAccumulator left,
        CoChangePairAccumulator right,
        IReadOnlyDictionary<CoChangePairAccumulator, CoChangeComponents> components)
    {
        CoChangeComponents leftComponents = components[left];
        CoChangeComponents rightComponents = components[right];
        int byCombined = rightComponents.Combined.CompareTo(leftComponents.Combined);
        if (byCombined != 0)
        {
            return byCombined;
        }

        int byCommit = rightComponents.Commit.CompareTo(leftComponents.Commit);
        if (byCommit != 0)
        {
            return byCommit;
        }

        int byTask = rightComponents.Task.CompareTo(leftComponents.Task);
        if (byTask != 0)
        {
            return byTask;
        }

        int byFirst = CompareVertices(left.First, right.First);
        return byFirst != 0 ? byFirst : CompareVertices(left.Second, right.Second);
    }

    private readonly record struct CoChangePairKey(string FirstPath, string SecondPath);

    private sealed class CoChangePairAccumulator(CoChangeVertex first, CoChangeVertex second)
    {
        public CoChangeVertex First { get; } = first;

        public CoChangeVertex Second { get; } = second;

        public CoChangeCohort Cohort { get; } = CoChangeCohort.Of(first.Category, second.Category);

        public List<string> CommitIds { get; } = [];

        public List<TaskKey> TaskKeys { get; } = [];
    }

    private readonly record struct CoChangeComponents(decimal Commit, decimal Task, decimal Combined);
}
