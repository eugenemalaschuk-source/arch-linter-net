using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class IgnoreMatcherTests
{
    [Test]
    public void IsIgnored_ExactMatch_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("A", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_NoMatch_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("X", "Y", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_WildcardPattern_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.*", ForbiddenReference = "B" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("MyApp.Services.Foo", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_WildcardPattern_NoMatch_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.*", ForbiddenReference = "B" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("Other.Services.Foo", "B", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_DoubleStarPattern_CrossSegmentMatch_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "*", ForbiddenReference = "MyApp.**.Models.*" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("Any.Source", "MyApp.Domain.Models.Foo", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_SingleCharPattern_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.?ervice*", ForbiddenReference = "B" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("MyApp.Services.Foo", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_EmptyPattern_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "", ForbiddenReference = "" }
        };

        bool result = ArchitectureIgnoreMatcher.IsIgnored("A", "B", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_BothPatternsMustMatch()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        Assert.That(ArchitectureIgnoreMatcher.IsIgnored("A", "B", ignored), Is.True);
        Assert.That(ArchitectureIgnoreMatcher.IsIgnored("A", "C", ignored), Is.False);
        Assert.That(ArchitectureIgnoreMatcher.IsIgnored("X", "B", ignored), Is.False);
    }
}
