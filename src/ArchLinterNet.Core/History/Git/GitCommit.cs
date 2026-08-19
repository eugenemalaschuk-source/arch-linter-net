using System.Numerics;

namespace ArchLinterNet.Core.History.Git;

// A parsed commit object. Canonical ordering is the exact committer epoch integer first and the full
// canonical commit ID second, so two commits sharing a timestamp still have one stable order.
internal sealed class GitCommit(
    GitObjectId id,
    GitObjectId tree,
    IReadOnlyList<GitObjectId> parents,
    GitIdentityHeader author,
    GitIdentityHeader committer,
    IReadOnlyList<string> encodingHeaderHex,
    byte[] rawMessage)
{
    public GitObjectId Id { get; } = id;

    public GitObjectId Tree { get; } = tree;

    public IReadOnlyList<GitObjectId> Parents { get; } = parents;

    public GitIdentityHeader Author { get; } = author;

    public GitIdentityHeader Committer { get; } = committer;

    // Mandatory provenance: retained as lowercase hexadecimal in original header order, and never
    // acted on as a transcoding instruction.
    public IReadOnlyList<string> EncodingHeaderHex { get; } = encodingHeaderHex;

    public byte[] RawMessage { get; } = rawMessage;

    public bool IsMerge => Parents.Count >= 2;

    public BigInteger CommitterEpochSecond => Committer.EpochSecond;

    public static int CompareCanonical(GitCommit left, GitCommit right)
    {
        int byEpoch = left.CommitterEpochSecond.CompareTo(right.CommitterEpochSecond);
        return byEpoch != 0 ? byEpoch : string.CompareOrdinal(left.Id.Hex, right.Id.Hex);
    }
}
