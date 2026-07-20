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
        ArchitectureBaselineCandidate candidate = baselineCandidates[0];
        Assert.Multiple(() =>
        {
            Assert.That(candidate.ContractGroup, Is.EqualTo("group"));
            Assert.That(candidate.ContractId, Is.EqualTo("contract-id"));
            Assert.That(candidate.SourceType, Is.EqualTo("Source.Type"));
            Assert.That(candidate.ForbiddenReference, Is.EqualTo("Forbidden.Reference"));
            Assert.That(candidate.Identity, Is.Not.Null);
            Assert.That(candidate.Identity!.IdentityVersion, Is.EqualTo(2));
            Assert.That(candidate.Identity.ContractFamily, Is.EqualTo("group"));
            Assert.That(candidate.Identity.ContractId, Is.EqualTo("contract-id"));
            Assert.That(candidate.Identity.SourceType, Is.EqualTo("Source.Type"));
            Assert.That(candidate.Identity.SourceAssembly, Is.Null);
            Assert.That(candidate.Identity.TargetAssembly, Is.Null);
            // No richer targetMember was supplied by the caller, so identity falls back to the
            // full forbiddenReference text — preserving legacy (source_type, forbidden_reference)
            // discrimination for families not yet qualified with assembly/member info.
            Assert.That(candidate.Identity.TargetMember, Is.EqualTo("Forbidden.Reference"));
            Assert.That(candidate.Identity.Occurrence, Is.EqualTo(0));
        });
    }

    [Test]
    public void IsIgnored_WithAssemblyAndMember_PopulatesStructuredIdentity()
    {
        var baselineCandidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict_method_body", baselineCandidates: baselineCandidates);

        bool ignored = context.IsIgnored(
            "Source.Type", "line 1: Console.WriteLine -> System.Console.WriteLine(string)",
            sourceAssembly: "MyApp.App", targetAssembly: "System.Console",
            targetMember: "System.Console.WriteLine(string)");

        Assert.That(ignored, Is.False);
        ArchitectureViolationIdentity identity = baselineCandidates[0].Identity!;
        Assert.Multiple(() =>
        {
            Assert.That(identity.ContractFamily, Is.EqualTo("method_body"));
            Assert.That(identity.Kind, Is.EqualTo("call"));
            Assert.That(identity.SourceAssembly, Is.EqualTo("MyApp.App"));
            Assert.That(identity.TargetAssembly, Is.EqualTo("System.Console"));
            Assert.That(identity.TargetMember, Is.EqualTo("System.Console.WriteLine(string)"));
        });
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
