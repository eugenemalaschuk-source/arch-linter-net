using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryRangeAndFileEventTests
{
    [Test]
    public void AnEmptyRangeSucceedsWithExplicitEmptyEvidence()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, first);

        Assert.That(result.Commits, Is.Empty);
        Assert.That(result.LogicalFiles, Is.Empty);
        Assert.That(result.ExcludedMergeCount, Is.Zero);
    }

    [Test]
    public void SideBranchCommitsBelongToTheRangeAndMergesStayMetadataOnly()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string baseCommit = repository.Commit("base");
        repository.Write("a.txt", "one\nmain\n");
        repository.Commit("main change");
        repository.Git("checkout", "-q", "-b", "side", baseCommit);
        repository.Write("b.txt", "side\n");
        repository.Commit("side change");
        repository.Git("checkout", "-q", "main");
        repository.Git("-c", "user.name=Fixture Author", "-c", "user.email=Fixture@Example.COM", "merge", "-q", "--no-ff", "-m", "merge side", "side");
        string merged = repository.Head();

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, baseCommit, merged);

        Assert.That(result.Commits.Count, Is.EqualTo(3));
        Assert.That(result.ExcludedMergeCount, Is.EqualTo(1));
        Assert.That(result.LogicalFiles.Select(static file => file.CanonicalPath), Is.EqualTo(new[] { "a.txt", "b.txt" }));
        Assert.That(HistoryIngestionFixture.File(result, "b.txt").CommitCount, Is.EqualTo(1));
    }

    [Test]
    public void ARootCommitIsDiffedAgainstTheEmptyTree()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\ntwo\n");
        string root = repository.Commit("root");
        repository.Git("checkout", "-q", "--orphan", "detached");
        repository.Git("rm", "-rf", "-q", ".");
        repository.Write("c.txt", "x\n");
        string orphan = repository.Commit("orphan root");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, root, orphan);

        LogicalFile file = HistoryIngestionFixture.File(result, "c.txt");
        Assert.That(file.Events.Single().KindText, Is.EqualTo("add"));
        Assert.That(file.Additions, Is.EqualTo(1));
        Assert.That(file.Deletions, Is.Zero);
    }

    [Test]
    public void CommitOrderUsesTheExactEpochThenTheCanonicalCommitId()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "two\n");
        repository.Commit("second");
        repository.Write("a.txt", "three\n");
        string third = repository.Commit("third");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, third);

        IReadOnlyList<CommitEvidence> commits = result.Commits;
        Assert.That(commits.Count, Is.EqualTo(2));
        Assert.That(commits[0].Commit.CommitterEpochSecond, Is.LessThan(commits[1].Commit.CommitterEpochSecond));
    }

    [Test]
    public void SamePathDeleteAndRecreateStaysOneBaselineIdentity()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/X.cs", "a\nb\n");
        string first = repository.Commit("add X");
        repository.Write("src/X.cs", "a\nb\nc\n");
        repository.Commit("modify X");
        repository.Remove("src/X.cs");
        repository.Commit("delete X");
        repository.Write("src/X.cs", "unrelated\n");
        string last = repository.Commit("recreate X");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, last);

        LogicalFile file = HistoryIngestionFixture.File(result, "src/X.cs");
        Assert.That(result.LogicalFiles.Count, Is.EqualTo(1));
        Assert.That(file.CommitCount, Is.EqualTo(3));
        Assert.That(file.Events.Select(static fileEvent => fileEvent.KindText), Is.EqualTo(new[] { "modify", "delete", "add" }));
    }

    [Test]
    public void GitlinkAndNulBlobEventsAreBinaryOrUnavailable()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.WriteBytes("binary.bin", [0x01, 0x00, 0x02]);
        string second = repository.Commit("add binary");

        LogicalFile file = HistoryIngestionFixture.File(HistoryIngestionFixture.Succeed(repository, first, second), "binary.bin");

        Assert.That(file.Events.Single().LineCountStatusText, Is.EqualTo("binary_or_unavailable"));
        Assert.That(file.Churn, Is.Zero);
    }

    [Test]
    public void AMissingRequiredBlobFailsClosed()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second");
        string blobId = repository.Git("rev-parse", $"{second}:a.txt").Trim();
        repository.DeleteLooseObject(blobId);

        HistoryDiagnostic diagnostic = HistoryIngestionFixture.Fail(repository, first, second);

        Assert.That(diagnostic.KindText, Is.EqualTo("object_missing"));
        Assert.That(diagnostic.ObjectId, Is.EqualTo(blobId));
    }
}
