namespace ArchLinterNet.Core.History.Git;

// The raw inflated payload without the `<type> <size>\0` framing. Canonical parsing always starts
// from these bytes so no presentation API can transcode or normalize metadata first.
internal sealed class GitRawObject(GitObjectKind kind, byte[] payload)
{
    public GitObjectKind Kind { get; } = kind;

    public byte[] Payload { get; } = payload;
}
