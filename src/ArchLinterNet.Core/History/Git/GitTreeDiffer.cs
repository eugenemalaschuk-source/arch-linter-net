namespace ArchLinterNet.Core.History.Git;

// Recursive parent-tree to commit-tree delta. Subtree pairs with identical object IDs are skipped
// outright, so the cost tracks the size of the change rather than the size of the tree. A root
// commit is diffed against the empty tree by passing an empty parent tree ID.
internal sealed class GitTreeDiffer(GitTreeReader trees)
{
    public IReadOnlyList<GitTreeChange> Diff(GitObjectId parentTree, GitObjectId commitTree, string commitId)
    {
        List<GitTreeChange> changes = [];
        DiffInto(parentTree, commitTree, string.Empty, commitId, changes);
        return changes;
    }

    private void DiffInto(GitObjectId oldTree, GitObjectId newTree, string prefix, string commitId, List<GitTreeChange> changes)
    {
        if (oldTree.Equals(newTree))
        {
            return;
        }

        Dictionary<string, GitTreeEntry> oldEntries = Index(oldTree, prefix, commitId);
        Dictionary<string, GitTreeEntry> newEntries = Index(newTree, prefix, commitId);
        foreach ((string name, GitTreeEntry oldEntry) in oldEntries)
        {
            string path = Join(prefix, name);
            if (!newEntries.TryGetValue(name, out GitTreeEntry? newEntry))
            {
                Remove(oldEntry, path, commitId, changes);
                continue;
            }

            if (oldEntry.IsTree && newEntry.IsTree)
            {
                DiffInto(oldEntry.Id, newEntry.Id, path, commitId, changes);
                continue;
            }

            if (oldEntry.IsTree || newEntry.IsTree)
            {
                // A path that changes between a tree and a leaf is a delete plus an add, never a
                // modification of one entry.
                Remove(oldEntry, path, commitId, changes);
                Add(newEntry, path, commitId, changes);
                continue;
            }

            if (!oldEntry.Id.Equals(newEntry.Id) || oldEntry.Mode != newEntry.Mode)
            {
                changes.Add(new GitTreeChange(GitTreeChangeKind.Modify, path, oldEntry.Id, oldEntry.Mode, newEntry.Id, newEntry.Mode));
            }
        }

        foreach ((string name, GitTreeEntry newEntry) in newEntries)
        {
            if (!oldEntries.ContainsKey(name))
            {
                Add(newEntry, Join(prefix, name), commitId, changes);
            }
        }
    }

    private void Add(GitTreeEntry entry, string path, string commitId, List<GitTreeChange> changes)
    {
        if (entry.IsTree)
        {
            DiffInto(default, entry.Id, path, commitId, changes);
            return;
        }

        changes.Add(new GitTreeChange(GitTreeChangeKind.Add, path, default, null, entry.Id, entry.Mode));
    }

    private void Remove(GitTreeEntry entry, string path, string commitId, List<GitTreeChange> changes)
    {
        if (entry.IsTree)
        {
            DiffInto(entry.Id, default, path, commitId, changes);
            return;
        }

        changes.Add(new GitTreeChange(GitTreeChangeKind.Delete, path, entry.Id, entry.Mode, default, null));
    }

    private Dictionary<string, GitTreeEntry> Index(GitObjectId treeId, string prefix, string commitId)
    {
        Dictionary<string, GitTreeEntry> entries = new(StringComparer.Ordinal);
        foreach (GitTreeEntry entry in trees.Read(treeId))
        {
            entries[GitPathDecoder.DecodeSegment(entry.NameBytes, prefix, commitId)] = entry;
        }

        return entries;
    }

    private static string Join(string prefix, string name) => prefix.Length == 0 ? name : $"{prefix}/{name}";
}
