using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Evidence;

// `Commits(from,to) = Reachable(to) \ Reachable(from)` plus the strict-ancestor test the rename
// lineage rule needs. Reachability is defined over parent edges only, so the analyzed set never
// depends on traversal order or first-parent enumeration.
internal sealed class CommitGraph(GitCommitReader commits)
{
    private readonly Dictionary<GitObjectId, HashSet<GitObjectId>> _reachableCache = [];

    public IReadOnlyList<GitCommit> Range(GitObjectId from, GitObjectId to)
    {
        HashSet<GitObjectId> excluded = Reachable(from);
        List<GitCommit> range = [];
        foreach (GitObjectId id in Reachable(to).Where(id => !excluded.Contains(id)))
        {
            range.Add(commits.Read(id));
        }

        range.Sort(GitCommit.CompareCanonical);
        return range;
    }

    public bool IsStrictAncestor(GitObjectId ancestor, GitObjectId descendant)
        => !ancestor.Equals(descendant) && Reachable(descendant).Contains(ancestor);

    public HashSet<GitObjectId> Reachable(GitObjectId start)
    {
        if (_reachableCache.TryGetValue(start, out HashSet<GitObjectId>? cached))
        {
            return cached;
        }

        HashSet<GitObjectId> visited = [];
        Stack<GitObjectId> pending = new();
        pending.Push(start);
        while (pending.Count > 0)
        {
            GitObjectId current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (GitObjectId parent in commits.Read(current).Parents.Where(parent => !visited.Contains(parent)))
            {
                pending.Push(parent);
            }
        }

        _reachableCache[start] = visited;
        return visited;
    }
}
