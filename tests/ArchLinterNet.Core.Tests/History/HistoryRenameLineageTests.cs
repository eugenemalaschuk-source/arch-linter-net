using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class HistoryRenameLineageTests
{
    private static readonly string[] _oldCsAlias = { "src/Old.cs" };
    private static readonly string[] _aThenBAliases = { "A.cs", "B.cs" };
    private static readonly string[] _bCsAliasOnly = { "B.cs" };
    private static readonly string[] _abcCanonicalPaths = { "A.cs", "B.cs", "C.cs" };

    [Test]
    public void APureExactRenameIsOneTouchWithZeroChurn()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("src/Old.cs", Lines(100));
        string first = repository.Commit("add");
        repository.Move("src/Old.cs", "tests/New.cs");
        string second = repository.Commit("move");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        LogicalFile file = HistoryIngestionFixture.File(result, "tests/New.cs");
        Assert.That(result.LogicalFiles.Count, Is.EqualTo(1));
        Assert.That(file.Aliases, Is.EqualTo(_oldCsAlias));
        Assert.That(file.CommitCount, Is.EqualTo(1));
        Assert.That(file.Churn, Is.Zero);
        Assert.That(file.Events.Single().LineCountStatusText, Is.EqualTo("exact_rename"));
        Assert.That(result.RenameCandidates.Single().Accepted, Is.True);
    }

    [Test]
    public void ALinearChainCanonicalizesToItsTerminalDestination()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string first = repository.Commit("add A");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Move("B.cs", "C.cs");
        string last = repository.Commit("B to C");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, last);

        LogicalFile file = HistoryIngestionFixture.File(result, "C.cs");
        Assert.That(result.LogicalFiles.Count, Is.EqualTo(1));
        Assert.That(file.Aliases, Is.EqualTo(_aThenBAliases));
        Assert.That(result.RenameComponents.Single().StatusText, Is.EqualTo("accepted"));
    }

    [Test]
    public void AnAliasCycleCanonicalizesBackToItsOriginalPath()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string first = repository.Commit("add A");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Move("B.cs", "A.cs");
        string last = repository.Commit("B to A");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, last);

        LogicalFile file = HistoryIngestionFixture.File(result, "A.cs");
        Assert.That(file.Aliases, Is.EqualTo(_bCsAliasOnly));
        Assert.That(result.RenameComponents.Single().StatusText, Is.EqualTo("accepted"));
    }

    [Test]
    public void AParallelForkIsAmbiguousAndKeepsOrdinaryEvents()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string baseCommit = repository.Commit("add A");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Git("checkout", "-q", "-b", "side", baseCommit);
        repository.Move("A.cs", "C.cs");
        string sideTip = repository.Commit("A to C");
        repository.Git("checkout", "-q", "main");

        // The `ours` strategy only exists to make both tips reachable from `to`; merge commits are
        // metadata-only for file evidence, so the merge tree never enters the assertion.
        repository.Git("merge", "-q", "-s", "ours", "-m", "merge", "side");
        string merged = repository.Head();

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, baseCommit, merged);

        Assert.That(result.RenameComponents.Single().StatusText, Is.EqualTo("ambiguous_dag"));
        Assert.That(result.RenameCandidates.All(static candidate => !candidate.Accepted), Is.True);
        Assert.That(
            result.LogicalFiles.Select(static file => file.CanonicalPath),
            Is.EqualTo(_abcCanonicalPaths));
        Assert.That(HistoryIngestionFixture.File(result, "B.cs").Events.Single().KindText, Is.EqualTo("add"));
        Assert.That(HistoryIngestionFixture.File(result, "A.cs").Events.All(static fileEvent => fileEvent.KindText == "delete"), Is.True);
    }

    [Test]
    public void APathDeleteAndRecreateBreaksTheLineage()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string first = repository.Commit("add A");
        repository.Move("A.cs", "B.cs");
        repository.Commit("A to B");
        repository.Remove("B.cs");
        repository.Commit("delete B");
        repository.Write("B.cs", "content\n");
        repository.Commit("recreate B");
        repository.Move("B.cs", "C.cs");
        string last = repository.Commit("B to C");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, last);

        Assert.That(result.RenameComponents.Single().StatusText, Is.EqualTo("ambiguous_dag"));
        Assert.That(
            result.LogicalFiles.Select(static file => file.CanonicalPath),
            Is.EqualTo(_abcCanonicalPaths));
    }

    [Test]
    public void ASameCommitSplitCreatesNoCandidate()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "content\n");
        string first = repository.Commit("add A");
        repository.Remove("A.cs");
        repository.Write("B.cs", "content\n");
        repository.Write("C.cs", "content\n");
        string second = repository.Commit("split A");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        Assert.That(result.RenameCandidates, Is.Empty);
        Assert.That(
            result.LogicalFiles.Select(static file => file.CanonicalPath),
            Is.EqualTo(_abcCanonicalPaths));
    }

    [Test]
    public void AModifiedMoveIsNotAnExactCandidate()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("A.cs", "one\ntwo\n");
        string first = repository.Commit("add A");
        repository.Remove("A.cs");
        repository.Write("B.cs", "one\ntwo\nthree\n");
        string second = repository.Commit("move with edit");

        HistoryIngestionResult result = HistoryIngestionFixture.Succeed(repository, first, second);

        Assert.That(result.RenameCandidates, Is.Empty);
        Assert.That(HistoryIngestionFixture.File(result, "A.cs").Deletions, Is.EqualTo(2));
        Assert.That(HistoryIngestionFixture.File(result, "B.cs").Additions, Is.EqualTo(3));
    }

    private static string Lines(int count) => string.Concat(Enumerable.Range(1, count).Select(static line => $"line {line}\n"));
}
