using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureContractExecutionContextTests
{
    [Test]
    public void IsIgnored_NonIgnoredPair_AddsBaselineCandidate()
    {
        var baselineCandidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: true, contractGroup: "group", baselineCandidates: baselineCandidates);

        bool ignored = context.IsIgnored("Source.Type", "Forbidden.Reference");

        Assert.That(ignored, Is.False);
        Assert.That(baselineCandidates, Has.Count.EqualTo(1));
        Assert.That(baselineCandidates[0], Is.EqualTo(
            new ArchitectureBaselineCandidate("group", "contract-id", "Source.Type", "Forbidden.Reference")));
    }

    [Test]
    public void IsIgnored_MatchedIgnore_DoesNotAddCandidate_AndCollectUnmatchedReportsOnlyStaleIgnore()
    {
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "Source.Type", ForbiddenReference = "Forbidden.Reference", Reason = "matched" },
            new() { SourceType = "Other.Type", ForbiddenReference = "Other.Reference", Reason = "stale" }
        };
        var baselineCandidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "group", baselineCandidates: baselineCandidates);

        bool ignored = context.IsIgnored("Source.Type", "Forbidden.Reference");

        Assert.That(ignored, Is.True);
        Assert.That(baselineCandidates, Is.Empty);

        var unmatched = new List<ArchitectureUnmatchedIgnoredViolation>();
        context.CollectUnmatchedIgnores(unmatched);

        Assert.That(unmatched, Has.Count.EqualTo(1));
        Assert.That(unmatched[0].SourceType, Is.EqualTo("Other.Type"));
        Assert.That(unmatched[0].ForbiddenReference, Is.EqualTo("Other.Reference"));
    }
}
