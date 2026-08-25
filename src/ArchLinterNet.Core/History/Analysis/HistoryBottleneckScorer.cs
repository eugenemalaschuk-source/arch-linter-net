using System.Numerics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

// Consumes only settled canonical evidence. In particular, it neither reconstructs file lifetime
// identity nor lets Gtheta or edge-normalized values feed a file-level bottleneck score.
internal sealed class HistoryBottleneckScorer
{
    private const decimal Scale = 1_000_000_000m;
    private static readonly BigInteger _integerScale = new(1_000_000_000);

    public static HistoryBottleneckAnalysis Score(HistoryIngestionResult result, HistoryAnalysisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Score(result.Commits, result.CoChangeGraph, configuration);
    }

    public static HistoryBottleneckAnalysis Score(
        IReadOnlyList<CommitEvidence> commits,
        CoChangeGraph coChangeGraph,
        HistoryAnalysisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(coChangeGraph);
        ArgumentNullException.ThrowIfNull(configuration);

        IReadOnlyDictionary<string, CommitEvidence> commitsById = commits.ToDictionary(
            static evidence => evidence.Commit.Id.Hex,
            StringComparer.Ordinal);
        IReadOnlyDictionary<CoChangeVertex, GraphEvidence> graphEvidence = BuildGraphEvidence(coChangeGraph);
        List<Candidate> candidates = [];
        foreach (CoChangeVertex vertex in coChangeGraph.Vertices)
        {
            candidates.Add(CreateCandidate(vertex, commitsById, graphEvidence[vertex]));
        }

        BottleneckWeights weights = Weights(configuration.Weights.Bottleneck);
        List<HistoryBottleneckCategoryGroup> groups = [];
        foreach (IGrouping<HistoryPathCategory, Candidate> cohort in candidates.GroupBy(static candidate => candidate.Category)
                     .OrderBy(static group => group.Key))
        {
            List<HistoryBottleneckFinding> findings = ScoreCohort(cohort.ToArray(), weights, coChangeGraph);
            findings.Sort(CompareFindings);
            groups.Add(new HistoryBottleneckCategoryGroup(cohort.Key, findings));
        }

        return new HistoryBottleneckAnalysis(groups);
    }

    private static IReadOnlyDictionary<CoChangeVertex, GraphEvidence> BuildGraphEvidence(CoChangeGraph graph)
    {
        Dictionary<CoChangeVertex, GraphEvidenceAccumulator> accumulators = graph.Vertices.ToDictionary(
            static vertex => vertex,
            static _ => new GraphEvidenceAccumulator());
        foreach (CoChangePair edge in graph.BaseEdges)
        {
            AddEdge(accumulators[edge.First], edge, edge.Second);
            AddEdge(accumulators[edge.Second], edge, edge.First);
        }

        return accumulators.ToDictionary(
            static entry => entry.Key,
            static entry => new GraphEvidence(entry.Value.Neighbors.Count, entry.Value.IncidentCommitDegree, entry.Value.IncidentTaskDegree));
    }

    private static void AddEdge(GraphEvidenceAccumulator accumulator, CoChangePair edge, CoChangeVertex neighbor)
    {
        accumulator.Neighbors.Add(neighbor);
        accumulator.IncidentCommitDegree += edge.CommitCoChange;
        accumulator.IncidentTaskDegree += edge.TaskCoChange;
    }

    private static Candidate CreateCandidate(
        CoChangeVertex vertex,
        IReadOnlyDictionary<string, CommitEvidence> commits,
        GraphEvidence graphEvidence)
    {
        List<CommitEvidence> fileCommits = vertex.File.Events
            .Select(fileEvent => commits.TryGetValue(fileEvent.CommitId, out CommitEvidence? evidence)
                ? evidence
                : throw new InvalidOperationException($"Canonical file event references unknown commit '{fileEvent.CommitId}'."))
            .OrderBy(static evidence => evidence, Comparer<CommitEvidence>.Create(CommitEvidence.CompareCanonical))
            .ToList();
        TaskKey[] taskKeys = fileCommits.SelectMany(static evidence => evidence.TaskKeys).Distinct().Order().ToArray();
        string[] authors = fileCommits.Select(static evidence => evidence.CanonicalAuthor).Distinct(StringComparer.Ordinal).ToArray();
        Array.Sort(authors, HistoryScalarValueComparer.Compare);
        List<BottleneckTaskPair> pairs = BuildIndependentPairs(taskKeys, fileCommits);
        HashSet<TaskKey> independentKeys = [];
        foreach (BottleneckTaskPair pair in pairs)
        {
            independentKeys.Add(pair.First);
            independentKeys.Add(pair.Second);
        }

        decimal temporal = pairs.Count == 0 ? 0m : pairs.Max(static pair => pair.TemporalProximity);
        return new Candidate(
            vertex,
            new BottleneckRawEvidence(
                independentKeys.Count,
                authors.Length,
                temporal,
                graphEvidence.DistinctNeighborDegree,
                graphEvidence.IncidentCommitDegree,
                graphEvidence.IncidentTaskDegree,
                taskKeys,
                pairs,
                authors));
    }

    private static List<BottleneckTaskPair> BuildIndependentPairs(IReadOnlyList<TaskKey> taskKeys, IReadOnlyList<CommitEvidence> fileCommits)
    {
        List<BottleneckTaskPair> pairs = [];
        for (int first = 0; first < taskKeys.Count; first++)
        {
            for (int second = first + 1; second < taskKeys.Count; second++)
            {
                TaskKey firstKey = taskKeys[first];
                TaskKey secondKey = taskKeys[second];
                List<CommitEvidence> firstExclusive = fileCommits.Where(commit => Contains(commit, firstKey) && !Contains(commit, secondKey)).ToList();
                List<CommitEvidence> secondExclusive = fileCommits.Where(commit => Contains(commit, secondKey) && !Contains(commit, firstKey)).ToList();
                if (firstExclusive.Count != 0 && secondExclusive.Count != 0)
                {
                    pairs.Add(CreatePair(firstKey, secondKey, firstExclusive, secondExclusive));
                }
            }
        }

        return pairs;
    }

    private static bool Contains(CommitEvidence evidence, TaskKey key) => evidence.TaskKeys.Contains(key);

    private static BottleneckTaskPair CreatePair(
        TaskKey first,
        TaskKey second,
        IReadOnlyList<CommitEvidence> firstExclusive,
        IReadOnlyList<CommitEvidence> secondExclusive)
    {
        BottleneckTaskInterval firstInterval = Interval(firstExclusive);
        BottleneckTaskInterval secondInterval = Interval(secondExclusive);
        (BottleneckTaskInterval earlier, BottleneckTaskInterval later) = CompareIntervals(firstInterval, secondInterval) <= 0
            ? (firstInterval, secondInterval)
            : (secondInterval, firstInterval);
        BigInteger gap = later.StartEpochSecond - earlier.EndEpochSecond;
        BigInteger days = gap <= BigInteger.Zero ? BigInteger.Zero : (gap + 86_399) / 86_400;
        return new BottleneckTaskPair(
            first,
            second,
            firstExclusive.Select(static evidence => evidence.Commit.Id.Hex).ToArray(),
            secondExclusive.Select(static evidence => evidence.Commit.Id.Hex).ToArray(),
            Provenance(firstExclusive, first),
            Provenance(secondExclusive, second),
            firstInterval,
            secondInterval,
            gap,
            days,
            QuantizedRatio(BigInteger.One, BigInteger.One + days));
    }

    private static int CompareIntervals(BottleneckTaskInterval left, BottleneckTaskInterval right)
    {
        int byStart = left.StartEpochSecond.CompareTo(right.StartEpochSecond);
        return byStart != 0 ? byStart : left.EndEpochSecond.CompareTo(right.EndEpochSecond);
    }

    private static BottleneckTaskInterval Interval(IReadOnlyList<CommitEvidence> commits)
    {
        BigInteger start = commits.Min(static evidence => evidence.Commit.CommitterEpochSecond);
        BigInteger end = commits.Max(static evidence => evidence.Commit.CommitterEpochSecond);
        return new BottleneckTaskInterval(start, end);
    }

    private static IReadOnlyList<BottleneckTaskProvenance> Provenance(IReadOnlyList<CommitEvidence> commits, TaskKey key)
        => commits.SelectMany(evidence => evidence.TaskKeyMatches
                .Where(match => match.Key.Equals(key))
                .Select(match => new BottleneckTaskProvenance(evidence.Commit.Id.Hex, match)))
            .ToArray();

    private static List<HistoryBottleneckFinding> ScoreCohort(
        IReadOnlyList<Candidate> candidates,
        BottleneckWeights weights,
        CoChangeGraph graph)
    {
        int maxTasks = candidates.Max(static candidate => candidate.RawEvidence.IndependentTaskSpread);
        int maxAuthors = candidates.Max(static candidate => candidate.RawEvidence.DistinctAuthorCount);
        decimal maxTemporal = candidates.Max(static candidate => candidate.RawEvidence.IndependentTemporalProximity);
        int maxDegree = candidates.Max(static candidate => candidate.RawEvidence.DistinctNeighborDegree);
        int maxIncidentCommit = candidates.Max(static candidate => candidate.RawEvidence.IncidentCommitDegree);
        int maxIncidentTask = candidates.Max(static candidate => candidate.RawEvidence.IncidentTaskDegree);
        List<HistoryBottleneckFinding> findings = [];
        foreach (Candidate candidate in candidates)
        {
            BottleneckRawEvidence raw = candidate.RawEvidence;
            decimal incidentCommit = QuantizedRatio(raw.IncidentCommitDegree, maxIncidentCommit);
            decimal incidentTask = QuantizedRatio(raw.IncidentTaskDegree, maxIncidentTask);
            decimal centrality = Quantize((graph.CommitWeight * incidentCommit) + (graph.TaskWeight * incidentTask));
            BottleneckComponents components = new(
                QuantizedRatio(raw.IndependentTaskSpread, maxTasks),
                QuantizedRatio(raw.DistinctAuthorCount, maxAuthors),
                QuantizedRatio(raw.IndependentTemporalProximity, maxTemporal),
                QuantizedRatio(raw.DistinctNeighborDegree, maxDegree),
                incidentCommit,
                incidentTask,
                centrality);
            decimal score = Quantize(
                (weights.IndependentTask * components.IndependentTask) +
                (weights.Author * components.Author) +
                (weights.Temporal * components.Temporal) +
                (weights.Degree * components.Degree) +
                (weights.Centrality * components.Centrality));
            findings.Add(new HistoryBottleneckFinding(
                candidate.Vertex.CanonicalPath,
                candidate.Vertex.File.Aliases,
                candidate.Category,
                raw,
                components,
                weights,
                score));
        }

        return findings;
    }

    private static BottleneckWeights Weights(HistoryBottleneckWeightProfile profile) => new(
        profile.IndependentTask, profile.Author, profile.Temporal, profile.Degree, profile.Centrality);

    private static decimal QuantizedRatio(int value, int maximum) => QuantizedRatio(new BigInteger(value), new BigInteger(maximum));

    private static decimal QuantizedRatio(decimal value, decimal maximum)
    {
        if (maximum == 0m)
        {
            return 0m;
        }

        return Quantize(value / maximum);
    }

    private static decimal QuantizedRatio(BigInteger value, BigInteger maximum)
    {
        if (maximum.IsZero)
        {
            return 0m;
        }

        BigInteger quotient = BigInteger.DivRem(value * _integerScale, maximum, out BigInteger remainder);
        int comparison = (remainder * 2).CompareTo(maximum);
        if (comparison > 0 || (comparison == 0 && !quotient.IsEven))
        {
            quotient++;
        }

        return (decimal)quotient / Scale;
    }

    private static decimal Quantize(decimal value) => decimal.Round(value, 9, MidpointRounding.ToEven);

    private static int CompareFindings(HistoryBottleneckFinding left, HistoryBottleneckFinding right)
    {
        int byScore = right.Score.CompareTo(left.Score);
        return byScore != 0 ? byScore : HistoryScalarValueComparer.Compare(left.CanonicalPath, right.CanonicalPath);
    }

    private sealed class Candidate(CoChangeVertex vertex, BottleneckRawEvidence rawEvidence)
    {
        public CoChangeVertex Vertex { get; } = vertex;

        public HistoryPathCategory Category => Vertex.Category;

        public BottleneckRawEvidence RawEvidence { get; } = rawEvidence;
    }

    private sealed class GraphEvidenceAccumulator
    {
        public HashSet<CoChangeVertex> Neighbors { get; } = [];

        public int IncidentCommitDegree { get; set; }

        public int IncidentTaskDegree { get; set; }
    }

    private readonly record struct GraphEvidence(int DistinctNeighborDegree, int IncidentCommitDegree, int IncidentTaskDegree);
}
