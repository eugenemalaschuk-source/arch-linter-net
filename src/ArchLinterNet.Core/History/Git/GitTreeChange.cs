namespace ArchLinterNet.Core.History.Git;

internal enum GitTreeChangeKind
{
    Add,
    Delete,
    Modify,
}

// One raw parent-tree to commit-tree delta entry, before any rename lineage or logical-file identity
// is applied. Modes are retained so gitlink and tree entries can be classified as non-line evidence
// rather than being guessed at from content.
internal sealed class GitTreeChange(GitTreeChangeKind kind, string path, GitObjectId oldId, string? oldMode, GitObjectId newId, string? newMode)
{
    public GitTreeChangeKind Kind { get; } = kind;

    public string Path { get; } = path;

    public GitObjectId OldId { get; } = oldId;

    public string? OldMode { get; } = oldMode;

    public GitObjectId NewId { get; } = newId;

    public string? NewMode { get; } = newMode;

    public bool OldIsBlob => IsBlobMode(OldMode);

    public bool NewIsBlob => IsBlobMode(NewMode);

    private static bool IsBlobMode(string? mode) => mode is "100644" or "100755" or "120000";
}
