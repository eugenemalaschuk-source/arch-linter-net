using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

// Per-commit canonical metadata evidence. Merge commits stay here as range metadata even though they
// contribute no file-derived evidence, so the excluded merge count is visible rather than implied.
internal sealed class CommitEvidence(
    GitCommit commit,
    string canonicalAuthor,
    IReadOnlyList<TaskKeyMatch> taskKeyMatches,
    IReadOnlyList<TaskKey> taskKeys)
{
    public GitCommit Commit { get; } = commit;

    public string CanonicalAuthor { get; } = canonicalAuthor;

    public IReadOnlyList<TaskKeyMatch> TaskKeyMatches { get; } = taskKeyMatches;

    public IReadOnlyList<TaskKey> TaskKeys { get; } = taskKeys;
}
