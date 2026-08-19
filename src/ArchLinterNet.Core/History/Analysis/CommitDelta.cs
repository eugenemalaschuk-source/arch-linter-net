using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// One non-merge commit paired with its parent-tree to commit-tree delta. Merge commits never get a
// delta: they stay range metadata only, which avoids first-parent versus combined-diff ambiguity.
internal sealed class CommitDelta(GitCommit commit, IReadOnlyList<GitTreeChange> changes)
{
    public GitCommit Commit { get; } = commit;

    public IReadOnlyList<GitTreeChange> Changes { get; } = changes;
}
