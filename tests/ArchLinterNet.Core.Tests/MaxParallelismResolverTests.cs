using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class MaxParallelismResolverTests
{
    [Test]
    public void Resolve_NullRequest_ReturnsBoundedDefault()
    {
        int resolved = MaxParallelismResolver.Resolve(null);

        Assert.That(resolved, Is.EqualTo(Math.Max(1, Math.Min(Environment.ProcessorCount, 4))));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(8)]
    [TestCase(64)]
    public void Resolve_PositiveOverride_IsHonoredExactly(int requested)
    {
        Assert.That(MaxParallelismResolver.Resolve(requested), Is.EqualTo(requested));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Resolve_NonPositiveOverride_ThrowsArgumentException(int requested)
    {
        Assert.Throws<ArgumentException>(() => MaxParallelismResolver.Resolve(requested));
    }
}
