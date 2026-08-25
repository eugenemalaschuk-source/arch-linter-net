using System.Numerics;
using ArchLinterNet.Core.History.Tasks;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.History;

[TestFixture]
public sealed class TaskKeyTests
{
    [Test]
    public void EqualityOperatorsMatchStructuralEquals()
    {
        TaskKey first = new("issue", new BigInteger(1));
        TaskKey second = new("issue", new BigInteger(1));
        TaskKey different = new("issue", new BigInteger(2));

        Assert.Multiple(() =>
        {
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first == different, Is.False);
            Assert.That(first != different, Is.True);
        });
    }

    [Test]
    public void ComparisonOperatorsOrderByNamespaceThenId()
    {
        TaskKey lower = new("issue", new BigInteger(1));
        TaskKey lowerCopy = new("issue", new BigInteger(1));
        TaskKey higher = new("issue", new BigInteger(2));
        TaskKey higherCopy = new("issue", new BigInteger(2));

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
}
