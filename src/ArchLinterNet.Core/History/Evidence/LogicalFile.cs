namespace ArchLinterNet.Core.History.Evidence;

// A logical file: one baseline path identity, or several unioned by an accepted exact-rename lineage.
// `CommitCount` counts distinct canonical file-evidence commits, not raw delta entries.
internal sealed class LogicalFile(string canonicalPath, IReadOnlyList<string> aliases, IReadOnlyList<FileEvent> events)
{
    public string CanonicalPath { get; } = canonicalPath;

    public IReadOnlyList<string> Aliases { get; } = aliases;

    public IReadOnlyList<FileEvent> Events { get; } = events;

    public int CommitCount => Events.Count;

    public long Additions => Events.Sum(static fileEvent => fileEvent.Additions);

    public long Deletions => Events.Sum(static fileEvent => fileEvent.Deletions);

    public long Churn => Additions + Deletions;
}
