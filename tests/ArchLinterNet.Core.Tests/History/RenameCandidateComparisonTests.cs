using ArchLinterNet.Core.History.Analysis;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.History.Git;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

// Canonical candidate ordering compares commit, then source path, then destination path, then blob
// ID. Real detector output can never tie past source path (two candidates in one commit always have
// distinct delete paths), so the destination/blob tie-break is only reachable through a direct test.
[TestFixture]
public sealed class RenameCandidateComparisonTests
{
    [Test]
    public void OrdersBySourcePathWhenCommitsAreEqual()
    {
        GitCommit commit = SyntheticCommit("aaaa");
        RenameCandidate first = new(commit, "a.cs", "z.cs", Blob("11"));
        RenameCandidate second = new(commit, "b.cs", "y.cs", Blob("22"));

        Assert.That(RenameCandidate.CompareCanonical(first, second), Is.LessThan(0));
        Assert.That(RenameCandidate.CompareCanonical(second, first), Is.GreaterThan(0));
    }

    [Test]
    public void OrdersByDestinationPathWhenCommitAndSourceAreEqual()
    {
        GitCommit commit = SyntheticCommit("bbbb");
        RenameCandidate first = new(commit, "a.cs", "m.cs", Blob("11"));
        RenameCandidate second = new(commit, "a.cs", "n.cs", Blob("22"));

        Assert.That(RenameCandidate.CompareCanonical(first, second), Is.LessThan(0));
    }

    [Test]
    public void OrdersByBlobIdAsTheFinalTieBreaker()
    {
        GitCommit commit = SyntheticCommit("cccc");
        RenameCandidate first = new(commit, "a.cs", "m.cs", Blob("11"));
        RenameCandidate second = new(commit, "a.cs", "m.cs", Blob("22"));

        Assert.That(RenameCandidate.CompareCanonical(first, second), Is.LessThan(0));
        Assert.That(RenameCandidate.CompareCanonical(first, first), Is.Zero);
    }

    private static GitObjectId Blob(string hexPrefix)
        => GitObjectId.TryParseHex(hexPrefix.PadRight(40, '0'), 20, out GitObjectId id) ? id : default;

    private static GitCommit SyntheticCommit(string idHexPrefix)
    {
        GitObjectId id = Blob(idHexPrefix);
        return new GitCommit(id, default, [], StubIdentity(), StubIdentity(), [], []);
    }

    private static GitIdentityHeader StubIdentity()
        => GitIdentityHeader.Parse("author", "A <a@example.com> 1700000000 +0000"u8.ToArray(), "stub");
}
