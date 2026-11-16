using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class IgnoreMatcherTests
{
    [Test]
    public void IsIgnored_ExactMatch_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("A", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_NoMatch_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("X", "Y", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_WildcardPattern_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.*", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("MyApp.Services.Foo", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_WildcardPattern_NoMatch_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.*", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("Other.Services.Foo", "B", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_DoubleStarPattern_CrossSegmentMatch_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "*", ForbiddenReference = "MyApp.**.Models.*" }
        };

        bool result = IsIgnored("Any.Source", "MyApp.Domain.Models.Foo", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_SingleCharPattern_ReturnsTrue()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "MyApp.?ervice*", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("MyApp.Services.Foo", "B", ignored);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsIgnored_EmptyPattern_ReturnsFalse()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "", ForbiddenReference = "" }
        };

        bool result = IsIgnored("A", "B", ignored);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsIgnored_BothPatternsMustMatch()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" }
        };

        Assert.That(IsIgnored("A", "B", ignored), Is.True);
        Assert.That(IsIgnored("A", "C", ignored), Is.False);
        Assert.That(IsIgnored("X", "B", ignored), Is.False);
    }

    [Test]
    public void UsageTracker_StartsEmpty()
    {
        var tracker = new ArchitectureIgnoreUsageTracker();

        Assert.That(tracker.MatchedIndexes, Is.Empty);
    }

    [Test]
    public void UsageTracker_MarkMatched_TracksIndex()
    {
        var tracker = new ArchitectureIgnoreUsageTracker();

        tracker.MarkMatched(0);
        tracker.MarkMatched(2);

        Assert.That(tracker.IsMatched(0), Is.True);
        Assert.That(tracker.IsMatched(1), Is.False);
        Assert.That(tracker.IsMatched(2), Is.True);
        Assert.That(tracker.MatchedIndexes.Count, Is.EqualTo(2));
    }

    [Test]
    public void UsageTracker_MarkDuplicate_IsIdempotent()
    {
        var tracker = new ArchitectureIgnoreUsageTracker();

        tracker.MarkMatched(1);
        tracker.MarkMatched(1);

        Assert.That(tracker.MatchedIndexes.Count, Is.EqualTo(1));
    }

    [Test]
    public void IsIgnored_WithTracker_MarksMatchedIndex()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" },
            new() { SourceType = "C", ForbiddenReference = "D" }
        };
        var tracker = new ArchitectureIgnoreUsageTracker();

        bool result = IsIgnored("A", "B", ignored, tracker);

        Assert.That(result, Is.True);
        Assert.That(tracker.IsMatched(0), Is.True);
        Assert.That(tracker.IsMatched(1), Is.False);
    }

    [Test]
    public void IsIgnored_WithTracker_UntouchedEntriesNotMatched()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "A", ForbiddenReference = "B" },
            new() { SourceType = "C", ForbiddenReference = "D" }
        };
        var tracker = new ArchitectureIgnoreUsageTracker();

        bool result = IsIgnored("X", "Y", ignored, tracker);

        Assert.That(result, Is.False);
        Assert.That(tracker.MatchedIndexes, Is.Empty);
    }

    [Test]
    public void IsIgnored_WithTracker_NoShortCircuit_AllMatchingEntriesTracked()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "*", ForbiddenReference = "B" },
            new() { SourceType = "A", ForbiddenReference = "B" }
        };
        var tracker = new ArchitectureIgnoreUsageTracker();

        IsIgnored("A", "B", ignored, tracker);

        Assert.That(tracker.IsMatched(0), Is.True, "Broad wildcard entry should be tracked");
        Assert.That(tracker.IsMatched(1), Is.True, "Specific overlapping entry should also be tracked");
    }

    [Test]
    public void IsIgnored_WithoutTracker_ShortCircuits()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "*", ForbiddenReference = "B" }
        };

        bool result = IsIgnored("A", "B", ignored);

        Assert.That(result, Is.True);
    }

    private static bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker = null)
    {
        var index = ArchitectureIgnoreMatcherIndex.Build(ignoredViolations);
        return index.IsIgnored(sourceType, forbiddenReference, usageTracker, null, null, out _);
    }
}
