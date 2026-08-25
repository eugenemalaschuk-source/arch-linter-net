using ArchLinterNet.Core.History.Git;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class GitObjectIdTests
{
    [Test]
    public void ComparisonOperatorsOrderByHexDigest()
    {
        GitObjectId lower = Parse("00000000000000000000000000000000000000");
        GitObjectId lowerCopy = Parse("00000000000000000000000000000000000000");
        GitObjectId higher = Parse("ffffffffffffffffffffffffffffffffffffffff");
        GitObjectId higherCopy = Parse("ffffffffffffffffffffffffffffffffffffffff");

        Assert.Multiple(() =>
        {
            Assert.That(lower < higher, Is.True);
            Assert.That(higher < lower, Is.False);
            Assert.That(lower <= lowerCopy, Is.True);
            Assert.That(higher > lower, Is.True);
            Assert.That(lower > higher, Is.False);
            Assert.That(higher >= higherCopy, Is.True);
        });
    }

    private static GitObjectId Parse(string hex)
    {
        Assert.That(GitObjectId.TryParseHex(hex, hex.Length / 2, out GitObjectId id), Is.True);
        return id;
    }
}
