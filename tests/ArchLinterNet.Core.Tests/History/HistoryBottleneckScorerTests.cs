using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Reporting;
using ArchLinterNet.Core.History.Tasks;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryBottleneckScorerTests
{
    [Test]
    public void LeadingZeroSpellingsShareOneTaskWhilePairExclusiveEvidenceEstablishesIndependence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        repository.Commit("first #001");
        repository.Write("X.cs", "three\n");
        repository.Commit("shared #1 #2");
        repository.Write("X.cs", "four\n");
        string last = repository.Commit("second #2");

        HistoryBottleneckFinding finding = HistoryIngestionFixture.Succeed(repository, first, last)
            .BottleneckAnalysis.Findings.Single();
        BottleneckTaskPair pair = finding.RawEvidence.IndependentTaskPairs.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(new[] { "issue#1", "issue#2" }));
            Assert.That(finding.RawEvidence.IndependentTaskSpread, Is.EqualTo(2));
            Assert.That(pair.First.ToString(), Is.EqualTo("issue#1"));
            Assert.That(pair.Second.ToString(), Is.EqualTo("issue#2"));
            Assert.That(pair.FirstExclusiveCommitIds, Has.Count.EqualTo(1));
            Assert.That(pair.SecondExclusiveCommitIds, Has.Count.EqualTo(1));
            Assert.That(pair.DaysBetween, Is.EqualTo(BigInteger.One));
            Assert.That(pair.TemporalProximity, Is.EqualTo(0.500000000m));
        });
    }

    [Test]
    public void OneMultiReferenceCommitDoesNotEstablishIndependence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        string last = repository.Commit("shared #101 #102");

        HistoryBottleneckFinding finding = HistoryIngestionFixture.Succeed(repository, first, last)
            .BottleneckAnalysis.Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.RawEvidence.TaskKeys.Select(static key => key.ToString()), Is.EqualTo(new[] { "issue#101", "issue#102" }));
            Assert.That(finding.RawEvidence.IndependentTaskSpread, Is.Zero);
            Assert.That(finding.RawEvidence.IndependentTemporalProximity, Is.Zero);
            Assert.That(finding.RawEvidence.IndependentTaskPairs, Is.Empty);
        });
    }

    [Test]
    public void HugeEpochsAndTimezoneTokensUseExactIntegerEndpoints()
    {
        TaskKey one = new("issue", BigInteger.One);
        TaskKey two = new("issue", new BigInteger(2));
        string firstId = Id(1);
        string secondId = Id(2);
        var files = new[]
        {
            new LogicalFile("X.cs", [], [Event(firstId), Event(secondId)]),
        };
        var commits = new[]
        {
            Commit(firstId, "1000000000000000000000000000000", "+1414", one, "#001"),
            Commit(secondId, "1000000000000000000000000000000", "-1200", two, "#2"),
        };

        HistoryBottleneckFinding finding = Score(files, commits).Findings.Single();
        BottleneckTaskPair pair = finding.RawEvidence.IndependentTaskPairs.Single();

        Assert.Multiple(() =>
        {
            Assert.That(pair.FirstInterval.StartEpochSecond, Is.EqualTo(BigInteger.Parse("1000000000000000000000000000000")));
            Assert.That(pair.SecondInterval.StartEpochSecond, Is.EqualTo(BigInteger.Parse("1000000000000000000000000000000")));
            Assert.That(pair.GapSeconds, Is.EqualTo(BigInteger.Zero));
            Assert.That(pair.DaysBetween, Is.EqualTo(BigInteger.Zero));
            Assert.That(pair.TemporalProximity, Is.EqualTo(1.000000000m));
        });
    }

    [Test]
    public void NinetyThousandSecondGapRoundsUpToTwoDays()
    {
        TaskKey one = new("issue", BigInteger.One);
        TaskKey two = new("issue", new BigInteger(2));
        string firstId = Id(1);
        string secondId = Id(2);
        var files = new[] { new LogicalFile("X.cs", [], [Event(firstId), Event(secondId)]) };
        var commits = new[] { Commit(firstId, "0", "+0000", one, "#1"), Commit(secondId, "90000", "+0000", two, "#2") };

        BottleneckTaskPair pair = Score(files, commits).Findings.Single().RawEvidence.IndependentTaskPairs.Single();

        Assert.Multiple(() =>
        {
            Assert.That(pair.DaysBetween, Is.EqualTo(new BigInteger(2)));
            Assert.That(pair.TemporalProximity, Is.EqualTo(0.333333333m));
        });
    }

    [Test]
    public void ThresholdCannotChangeG0DerivedCentralityOrBottleneckScore()
    {
        string a1 = Id(1);
        string a2 = Id(2);
        string b1 = Id(3);
        string b2 = Id(4);
        string c1 = Id(5);
        string c2 = Id(6);
        var files = new[]
        {
            new LogicalFile("A.cs", [], [Event(a1), Event(a2), Event(b1), Event(c1)]),
            new LogicalFile("B.cs", [], [Event(a1), Event(a2), Event(b1), Event(b2)]),
            new LogicalFile("C.cs", [], [Event(c1), Event(c2), Event(b1), Event(b2)]),
        };
        var commits = new[]
        {
            Commit(a1, "1", "+0000"), Commit(a2, "2", "+0000"), Commit(b1, "3", "+0000"),
            Commit(b2, "4", "+0000"), Commit(c1, "5", "+0000"), Commit(c2, "6", "+0000"),
        };
        HistoryBottleneckAnalysis low = Score(files, commits, threshold: 0m);
        HistoryBottleneckAnalysis high = Score(files, commits, threshold: 1m);

        Assert.That(
            high.Findings.Select(DescribeFinding),
            Is.EqualTo(low.Findings.Select(DescribeFinding)));
    }

    [Test]
    public void JsonExposesBottleneckPairsIntervalsProvenanceAndWeights()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("X.cs", "one\n");
        string first = repository.Commit("base");
        repository.Write("X.cs", "two\n");
        repository.Commit("first #1");
        repository.Write("X.cs", "three\n");
        string last = repository.Commit("second #2");

        string json = HistoryIngestionJsonWriter.Write(HistoryIngestionFixture.Succeed(repository, first, last));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"bottleneckGroups\""));
            Assert.That(json, Does.Contain("\"independentTaskPairs\""));
            Assert.That(json, Does.Contain("\"firstInterval\""));
            Assert.That(json, Does.Contain("\"gapSeconds\""));
            Assert.That(json, Does.Contain("\"firstProvenance\""));
            Assert.That(json, Does.Contain("\"pathnameReuseMayConflateGenerations\": true"));
        });
    }

    private static HistoryBottleneckAnalysis Score(
        IReadOnlyList<LogicalFile> files,
        IReadOnlyList<CommitEvidence> commits,
        decimal? threshold = null)
    {
        var configuration = new HistoryAnalysisConfiguration
        {
            Thresholds = new HistoryAnalysisThresholds { CoChangeSignificance = threshold },
        };
        CoChangeGraph graph = new CoChangeGraphBuilder(configuration).Build(files, commits, []);
        var result = new HistoryIngestionResult("sha1", "from", "to", "from", "to", commits, 0, [], [], files, graph, new HistoryBottleneckAnalysis([]));
        return new HistoryBottleneckScorer().Score(result, configuration);
    }

    private static CommitEvidence Commit(string id, string epoch, string timezone, params object[] taskParts)
    {
        var tasks = new List<TaskKey>();
        var matches = new List<TaskKeyMatch>();
        for (int index = 0; index < taskParts.Length; index += 2)
        {
            TaskKey key = (TaskKey)taskParts[index];
            string spelling = (string)taskParts[index + 1];
            tasks.Add(key);
            matches.Add(new TaskKeyMatch("issue", key, index, index + spelling.Length, spelling));
        }

        byte[] header = Encoding.UTF8.GetBytes($"Fixture <fixture@example.com> {epoch} {timezone}");
        GitIdentityHeader identity = GitIdentityHeader.Parse("author", header, id);
        var commit = new GitCommit(ParseId(id), default, [], identity, identity, [], []);
        return new CommitEvidence(commit, "fixture@example.com", matches, tasks);
    }

    private static FileEvent Event(string id) => new(id, FileEventKind.Modify, LineCountStatus.Text, 0, 0, null, null);

    private static string Id(int sequence)
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

    private static string DescribeFinding(HistoryBottleneckFinding finding)
        => $"{finding.CanonicalPath}:{finding.Components.Degree:F9}:{finding.Components.Centrality:F9}:{finding.Score:F9}";
}
