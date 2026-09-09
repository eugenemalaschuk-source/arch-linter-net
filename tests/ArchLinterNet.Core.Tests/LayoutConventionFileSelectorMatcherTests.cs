using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionFileSelectorMatcherTests
{
    [Test]
    public void AnyCandidatePathMatchesFileSelector_MatchesNormalizedSourcePath()
    {
        ArchitectureLayoutFileMatcher matcher = CreateMatcher();

        bool matches = LayoutConventionFileSelectorMatcher.AnyCandidatePathMatchesFileSelector(
            matcher,
            ["src/Services/OrderService.cs"]);

        Assert.That(matches, Is.True);
    }

    [Test]
    public void AnyCandidatePathMatchesFileSelector_RequiresAllCriteriaOnTheSamePath()
    {
        ArchitectureLayoutFileMatcher matcher = CreateMatcher();

        bool matches = LayoutConventionFileSelectorMatcher.AnyCandidatePathMatchesFileSelector(
            matcher,
            [
                "src/Services/InvoiceService.cs",
                "src/Domain/OrderService.cs",
            ]);

        Assert.That(matches, Is.False);
    }

    private static ArchitectureLayoutFileMatcher CreateMatcher() => new()
    {
        FolderSegment = "Services",
        FileNamePrefix = "Order",
        FileNameSuffix = "Service",
    };
}
