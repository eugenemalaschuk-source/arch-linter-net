namespace ArchLinterNet.Core.History.Git;

// A single tree entry. `NameBytes` stays raw because strict UTF-8 decoding is a canonical ingestion
// rule with a fail-closed diagnostic, not something to do silently while reading an object.
internal sealed class GitTreeEntry(string mode, byte[] nameBytes, GitObjectId id)
{
    public string Mode { get; } = mode;

    public byte[] NameBytes { get; } = nameBytes;

    public GitObjectId Id { get; } = id;

    public bool IsTree => Mode == "40000";

    // Gitlinks (commit entries) and symlinks are never blob-to-blob line evidence.
    public bool IsBlob => Mode is "100644" or "100755" or "120000";
}
