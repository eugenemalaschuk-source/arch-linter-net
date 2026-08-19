using ArchLinterNet.Core.History;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

// A linked worktree (`git worktree add`) has a private $GIT_DIR whose only per-worktree state is
// HEAD; objects, repository config, and branch/tag refs live in the main repository's $GIT_DIR,
// reached through the worktree's `commondir` pointer.
[TestFixture]
public sealed class HistoryWorktreeTests
{
    [Test]
    public void ALinkedWorktreeResolvesBranchesAndObjectsFromTheCommonDirectory()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string second = repository.Commit("second #9");
        repository.Git("branch", "feature");

        string worktreePath = Path.Combine(Path.GetDirectoryName(repository.Path)!, "arch-linter-history-worktree-" + Guid.NewGuid().ToString("N"));
        try
        {
            repository.Git("worktree", "add", "-q", "--detach", worktreePath, "feature");

            HistoryIngestionResult result = HistoryIngestionFixture.Succeed(worktreePath, first, "feature");

            Assert.Multiple(() =>
            {
                Assert.That(result.ResolvedTo, Is.EqualTo(second));
                Assert.That(result.ObjectFormatName, Is.EqualTo("sha1"));
                Assert.That(result.Commits.Single().Commit.Id.Hex, Is.EqualTo(second));
            });
        }
        finally
        {
            repository.Git("worktree", "remove", "--force", worktreePath);
        }
    }

    [Test]
    public void ALinkedWorktreeReadsHeadFromItsOwnPrivateDirectory()
    {
        using GitTestRepository repository = GitTestRepository.Create();
        repository.Write("a.txt", "one\n");
        string first = repository.Commit("first");
        repository.Write("a.txt", "one\ntwo\n");
        string mainTip = repository.Commit("second");
        repository.Git("branch", "feature", first);

        string worktreePath = Path.Combine(Path.GetDirectoryName(repository.Path)!, "arch-linter-history-worktree-" + Guid.NewGuid().ToString("N"));
        try
        {
            repository.Git("worktree", "add", "-q", worktreePath, "feature");

            // The worktree's own HEAD points at `feature`, not at the main worktree's HEAD, so
            // resolving HEAD from inside the worktree must not silently fall back to the main tip.
            HistoryIngestionResult result = HistoryIngestionFixture.Succeed(worktreePath, first, "HEAD");

            Assert.That(result.Commits, Is.Empty);
            Assert.That(result.ResolvedTo, Is.EqualTo(first));
            Assert.That(result.ResolvedTo, Is.Not.EqualTo(mainTip));
        }
        finally
        {
            repository.Git("worktree", "remove", "--force", worktreePath);
        }
    }
}
