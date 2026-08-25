using System.Buffers.Binary;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Reporting;
using ArchLinterNet.Core.History.Tasks;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryCoChangeGraphTests
{
    private static readonly string[] _issue1Jira1TaskKeys = ["issue#1", "jira#1"];
    private static readonly string[] _srcXCsCanonicalPaths = ["src/X.cs"];
    private static readonly string[] _abcCanonicalPaths = ["A.cs", "B.cs", "C.cs"];
    private static readonly string[] _aToBAToCEdgePairs = ["A.cs:B.cs", "A.cs:C.cs"];
    private static readonly string[] _rankedEdgeDescriptions =
    [
        "B.cs:C.cs:0.700000000:1",
        "A.cs:B.cs:0.600000000:2",
        "A.cs:C.cs:0.590000000:3",
    ];
    private static readonly string[] _aToBBToCEdgePairs = ["A.cs:B.cs", "B.cs:C.cs"];

    [Test]
    public void DefaultLeadingZeroTaskSpellingsProduceOneTaskCoChange()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Commit("change A #001");
        repository.Write("B.cs", "two\n");
        string last = repository.Commit("change B #1");

        CoChangePair pair = HistoryIngestionFixture.Succeed(repository, first, last).CoChangeGraph.Pairs.Single();

        Assert.Multiple(() =>
        {
            Assert.That(pair.CommitCoChange, Is.Zero);
            Assert.That(pair.TaskCoChange, Is.EqualTo(1));
            Assert.That(pair.TaskKeys.Single().ToString(), Is.EqualTo("issue#1"));
            Assert.That(pair.IsBaseEdge, Is.False);
            Assert.That(pair.CommitComponent, Is.Null);
        });
    }

    [Test]
    public void DistinctTaskNamespacesRemainDistinctWhileCommitEvidenceCreatesTopology()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Write("B.cs", "two\n");
        repository.Commit("change #001 and JIRA-001");
        repository.Write("A.cs", "three\n");
        repository.Write("B.cs", "three\n");
        string last = repository.Commit("change #1 and JIRA-1");
        var configuration = new HistoryAnalysisConfiguration
        {
            Extractors = [new HistoryTaskExtractorConfiguration
            {
                Id = "jira",
                Namespace = "jira",
                Pattern = new HistoryTaskExtractorPattern { Prefix = "JIRA-" },
            }],
        };

        CoChangePair pair = HistoryIngestionFixture.Succeed(repository, first, last, configuration).CoChangeGraph.BaseEdges.Single();

        Assert.Multiple(() =>
        {
            Assert.That(pair.CommitCoChange, Is.EqualTo(2));
            Assert.That(pair.TaskCoChange, Is.EqualTo(2));
            Assert.That(pair.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(_issue1Jira1TaskKeys));
            Assert.That(pair.CommitComponent, Is.EqualTo(1m));
            Assert.That(pair.TaskComponent, Is.EqualTo(1m));
            Assert.That(pair.CombinedCoChange, Is.EqualTo(1m));
        });
    }

    [Test]
    public void SameCommitCoChangeWithoutTasksCreatesABaseEdgeWithZeroTaskComponent()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Write("B.cs", "two\n");
        string last = repository.Commit("co-change without a task");

        CoChangePair pair = HistoryIngestionFixture.Succeed(repository, first, last).CoChangeGraph.BaseEdges.Single();

        Assert.Multiple(() =>
        {
            Assert.That(pair.CommitCoChange, Is.EqualTo(1));
            Assert.That(pair.TaskCoChange, Is.Zero);
            Assert.That(pair.CommitComponent, Is.EqualTo(1m));
            Assert.That(pair.TaskComponent, Is.Zero);
            Assert.That(pair.CombinedCoChange, Is.EqualTo(0.750000000m));
        });
    }

    [Test]
    public void SamePathDeleteAndReaddStaysOneGraphVertex()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Remove("src/X.cs");
        repository.Commit("delete");
        repository.Write("src/X.cs", "unrelated\n");
        string last = repository.Commit("readd");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, last);

        Assert.Multiple(() =>
        {
            Assert.That(result.CoChangeGraph.Vertices.Select(static vertex => vertex.CanonicalPath), Is.EqualTo(_srcXCsCanonicalPaths));
            Assert.That(result.CoChangeGraph.Pairs, Is.Empty);
            Assert.That(result.CoChangeGraph.BaseEdges, Is.Empty);
        });
    }

    [Test]
    public void AmbiguousRenamePathsStaySeparateGraphVerticesWithComponentProvenance()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string baseCommit = repository.Commit("base");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Git("checkout", "-q", "-b", "side", baseCommit);
        repository.Move("A.cs", "C.cs");
        repository.Commit("A to C");
        repository.Git("checkout", "-q", "main");
        repository.Git("merge", "-q", "-s", "ours", "-m", "merge", "side");
        string merged = repository.Head();

        CoChangeGraph graph = HistoryIngestionFixture.Succeed(repository, baseCommit, merged).CoChangeGraph;

        Assert.Multiple(() =>
        {
            Assert.That(graph.Vertices.Select(static vertex => vertex.CanonicalPath), Is.EqualTo(_abcCanonicalPaths));
            Assert.That(graph.Vertices.All(static vertex => vertex.RenameComponents.Single().StatusText == "ambiguous_dag"), Is.True);
            Assert.That(graph.BaseEdges.Select(DescribePair), Is.EqualTo(_aToBAToCEdgePairs));
        });
    }

    [Test]
    public void ComponentsAndClustersAreCohortLocalInputOrderIndependentAndThresholdOnly()
    {
        HistoryAnalysisConfiguration lowThreshold = CoChangeConfiguration(0.600000000m);
        (IReadOnlyList<LogicalFile> files, IReadOnlyList<CommitEvidence> commits) = SyntheticEvidence(
            ("A.cs", "B.cs", 60),
            ("B.cs", "C.cs", 70),
            ("A.cs", "C.cs", 59));
        CoChangeGraph forward = new CoChangeGraphBuilder(lowThreshold).Build(files, commits, []);
        CoChangeGraph reverse = new CoChangeGraphBuilder(lowThreshold).Build(
            files.Reverse().ToArray(),
            commits.Reverse().ToArray(),
            []);
        CoChangeCluster cluster = forward.Clusters.Single();

        Assert.Multiple(() =>
        {
            Assert.That(forward.BaseEdges.Select(DescribeRankedEdge), Is.EqualTo(_rankedEdgeDescriptions));
            Assert.That(cluster.Members.Select(static member => member.CanonicalPath), Is.EqualTo(_abcCanonicalPaths));
            Assert.That(cluster.Edges.Select(DescribePair), Is.EqualTo(_aToBBToCEdgePairs));
            Assert.That(cluster.Maximum, Is.EqualTo(0.700000000m));
            Assert.That(cluster.Aggregate, Is.EqualTo(1.300000000m));
            Assert.That(reverse.Pairs.Select(DescribeEdge), Is.EqualTo(forward.Pairs.Select(DescribeEdge)));
            Assert.That(reverse.Clusters.Select(DescribeCluster), Is.EqualTo(forward.Clusters.Select(DescribeCluster)));
        });

        CoChangeGraph highThreshold = new CoChangeGraphBuilder(CoChangeConfiguration(0.800000000m)).Build(files, commits, []);
        CoChangeGraph noThreshold = new CoChangeGraphBuilder(CoChangeConfiguration(null)).Build(files, commits, []);
        Assert.Multiple(() =>
        {
            Assert.That(highThreshold.Clusters, Is.Empty);
            Assert.That(noThreshold.Clusters, Is.Empty);
            Assert.That(highThreshold.BaseEdges.Select(DescribeEdge), Is.EqualTo(forward.BaseEdges.Select(DescribeEdge)));
        });
    }

    [Test]
    public void EdgeNormalizationIsLocalToTheUnorderedEndpointCategoryCohort()
    {
        var configuration = new HistoryAnalysisConfiguration
        {
            Paths = new HistoryPathConfiguration
            {
                Production = ["src/**"],
                Tests = ["tests/**"],
            },
            Weights = new HistoryAnalysisWeightProfiles
            {
                CoChange = new HistoryCoChangeWeightProfile { Commit = 1m, Task = 0m },
            },
        };
        (IReadOnlyList<LogicalFile> files, IReadOnlyList<CommitEvidence> commits) = SyntheticEvidence(
            ("src/A.cs", "src/B.cs", 1),
            ("src/A.cs", "tests/T.cs", 2));

        CoChangeGraph graph = new CoChangeGraphBuilder(configuration).Build(files, commits, []);
        CoChangePair productionEdge = graph.BaseEdges.Single(static edge => edge.Cohort == CoChangeCohort.Of(HistoryPathCategory.Production, HistoryPathCategory.Production));
        CoChangePair crossCategoryEdge = graph.BaseEdges.Single(static edge => edge.Cohort == CoChangeCohort.Of(HistoryPathCategory.Production, HistoryPathCategory.Tests));

        Assert.Multiple(() =>
        {
            Assert.That(productionEdge.CommitComponent, Is.EqualTo(1m));
            Assert.That(crossCategoryEdge.CommitComponent, Is.EqualTo(1m));
            Assert.That(productionEdge.CombinedCoChange, Is.EqualTo(1m));
            Assert.That(crossCategoryEdge.CombinedCoChange, Is.EqualTo(1m));
        });
    }

    [Test]
    public void JsonExposesGraphWeightsCountsComponentsCohortsAndProvenanceLinks()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\n");
        repository.Write("B.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("A.cs", "two\n");
        repository.Write("B.cs", "two\n");
        string last = repository.Commit("change #42");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, last));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"coChangeGraph\""));
            Assert.That(json, Does.Contain("\"commitCoChange\": 1"));
            Assert.That(json, Does.Contain("\"combinedCoChange\": 1.000000000"));
            Assert.That(json, Does.Contain("\"firstCategory\": \"unknown\""));
            Assert.That(json, Does.Contain("\"namespace\": \"issue\""));
        });
    }

    private static HistoryAnalysisConfiguration CoChangeConfiguration(decimal? threshold) => new()
    {
        Weights = new HistoryAnalysisWeightProfiles
        {
            CoChange = new HistoryCoChangeWeightProfile { Commit = 0.70m, Task = 0.30m },
        },
        Thresholds = new HistoryAnalysisThresholds { CoChangeSignificance = threshold },
    };

    private static (IReadOnlyList<LogicalFile> Files, IReadOnlyList<CommitEvidence> Commits) SyntheticEvidence(
        params (string First, string Second, int Count)[] associations)
    {
        Dictionary<string, List<FileEvent>> eventsByPath = new(StringComparer.Ordinal);
        List<CommitEvidence> commits = [];
        int sequence = 1;
        foreach ((string first, string second, int count) in associations)
        {
            for (int index = 0; index < count; index++)
            {
                string commitId = CommitId(sequence++);
                AddEvent(eventsByPath, first, commitId);
                AddEvent(eventsByPath, second, commitId);
                commits.Add(Commit(commitId));
            }
        }

        IReadOnlyList<LogicalFile> files = eventsByPath
            .Select(static entry => new LogicalFile(entry.Key, [], entry.Value))
            .ToArray();
        return (files, commits);
    }

    private static void AddEvent(Dictionary<string, List<FileEvent>> eventsByPath, string path, string commitId)
    {
        if (!eventsByPath.TryGetValue(path, out List<FileEvent>? events))
        {
            events = [];
            eventsByPath[path] = events;
        }

        events.Add(new FileEvent(commitId, FileEventKind.Modify, LineCountStatus.Text, 0, 0, null, null));
    }

    private static CommitEvidence Commit(string id, IReadOnlyList<TaskKey>? taskKeys = null)
    {
        GitObjectId commitId = ParseId(id);
        byte[] header = Encoding.UTF8.GetBytes("Fixture <fixture@example.com> 0 +0000");
        GitIdentityHeader identity = GitIdentityHeader.Parse("author", header, id);
        var commit = new GitCommit(commitId, default, [], identity, identity, [], []);
        return new CommitEvidence(commit, "fixture@example.com", [], taskKeys ?? []);
    }

    private static string CommitId(int sequence)
    {
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteInt32BigEndian(bytes, sequence);
        return GitObjectId.FromBytes(bytes).Hex;
    }

    private static GitObjectId ParseId(string id)
    {
        Assert.That(GitObjectId.TryParseHex(id, 20, out GitObjectId objectId), Is.True);
        return objectId;
    }

    private static string DescribePair(CoChangePair pair) => $"{pair.First.CanonicalPath}:{pair.Second.CanonicalPath}";

    private static string DescribeEdge(CoChangePair pair)
        => $"{DescribePair(pair)}:{pair.CombinedCoChange?.ToString("F9", System.Globalization.CultureInfo.InvariantCulture)}";

    private static string DescribeRankedEdge(CoChangePair pair) => $"{DescribeEdge(pair)}:{pair.CohortRank}";

    private static string DescribeCluster(CoChangeCluster cluster)
        => $"{string.Join(',', cluster.Members.Select(static member => member.CanonicalPath))}:{cluster.Maximum:F9}:{cluster.Aggregate:F9}";
}
