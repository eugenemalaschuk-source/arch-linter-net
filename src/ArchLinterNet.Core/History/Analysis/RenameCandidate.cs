using ArchLinterNet.Core.History.Canonical;
using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// A local exact-rename candidate: a one-to-one delete/add relation inside one non-merge commit whose
// preimage and postimage share a blob object ID with no competing source or destination. Every
// candidate is mandatory canonical provenance, including the ones that end up ambiguous.
internal sealed class RenameCandidate(GitCommit commit, string sourcePath, string destinationPath, GitObjectId blobId)
{
    public GitCommit Commit { get; } = commit;

    public string SourcePath { get; } = sourcePath;

    public string DestinationPath { get; } = destinationPath;

    public GitObjectId BlobId { get; } = blobId;

    public int ComponentIndex { get; set; } = -1;

    public bool Accepted { get; set; }

    public static int CompareCanonical(RenameCandidate left, RenameCandidate right)
    {
        int byCommit = GitCommit.CompareCanonical(left.Commit, right.Commit);
        if (byCommit != 0)
        {
            return byCommit;
        }

        int bySource = HistoryScalarValueComparer.Compare(left.SourcePath, right.SourcePath);
        if (bySource != 0)
        {
            return bySource;
        }

        int byDestination = HistoryScalarValueComparer.Compare(left.DestinationPath, right.DestinationPath);
        return byDestination != 0 ? byDestination : string.CompareOrdinal(left.BlobId.Hex, right.BlobId.Hex);
    }
}
