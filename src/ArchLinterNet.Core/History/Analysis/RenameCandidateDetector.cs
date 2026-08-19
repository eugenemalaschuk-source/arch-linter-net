using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// Detects local exact-rename candidates inside one non-merge commit delta. Similarity inference, copy
// inference, rename-with-edit, and any ambient Git rename threshold deliberately cannot create a
// candidate: only an identical blob object ID with exactly one source and one destination qualifies.
internal static class RenameCandidateDetector
{
    public static IReadOnlyList<RenameCandidate> Detect(GitCommit commit, IReadOnlyList<GitTreeChange> changes)
    {
        Dictionary<GitObjectId, List<GitTreeChange>> deletesByBlob = [];
        Dictionary<GitObjectId, List<GitTreeChange>> addsByBlob = [];
        foreach (GitTreeChange change in changes)
        {
            if (change.Kind == GitTreeChangeKind.Delete && change.OldIsBlob)
            {
                Bucket(deletesByBlob, change.OldId).Add(change);
            }
            else if (change.Kind == GitTreeChangeKind.Add && change.NewIsBlob)
            {
                Bucket(addsByBlob, change.NewId).Add(change);
            }
        }

        List<RenameCandidate> candidates = [];
        foreach ((GitObjectId blobId, List<GitTreeChange> deletes) in deletesByBlob)
        {
            // A same-commit split or join is a competing relation, so no candidate is created at all.
            if (deletes.Count != 1 || !addsByBlob.TryGetValue(blobId, out List<GitTreeChange>? adds) || adds.Count != 1)
            {
                continue;
            }

            candidates.Add(new RenameCandidate(commit, deletes[0].Path, adds[0].Path, blobId));
        }

        candidates.Sort(RenameCandidate.CompareCanonical);
        return candidates;
    }

    private static List<GitTreeChange> Bucket(Dictionary<GitObjectId, List<GitTreeChange>> map, GitObjectId key)
    {
        if (!map.TryGetValue(key, out List<GitTreeChange>? bucket))
        {
            bucket = [];
            map[key] = bucket;
        }

        return bucket;
    }
}
