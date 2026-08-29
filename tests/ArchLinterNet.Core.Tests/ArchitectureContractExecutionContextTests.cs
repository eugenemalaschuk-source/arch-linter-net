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
            // Display text is deliberately not part of canonical identity. Call sites that have a
            // semantic target must provide it explicitly instead of inheriting forbiddenReference.
            Assert.That(candidate.Identity.TargetMember, Is.Null);
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
    public void IsIgnored_WithTargetType_PreservesSemanticTargetSeparatelyFromDisplayText()
    {
        var baselineCandidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict_method_body", baselineCandidates: baselineCandidates);

        bool ignored = context.IsIgnored(
            "Source.Type",
            "il 000A (Run): forbidden -> Target.Type.Call()",
            sourceAssembly: "Source.Assembly",
            targetAssembly: "Target.Assembly",
            targetType: "Target.Type",
            sourceMember: "Source.Type.Run",
            targetMember: "Target.Type.Call()");

        Assert.That(ignored, Is.False);
        ArchitectureViolationIdentity identity = baselineCandidates[0].Identity!;
        Assert.Multiple(() =>
        {
            Assert.That(identity.TargetType, Is.EqualTo("Target.Type"));
            Assert.That(identity.TargetMember, Is.EqualTo("Target.Type.Call()"));
            Assert.That(identity.TargetMember, Does.Not.Contain("il 000A"));
        });
    }

    [Test]
    public void IsIgnored_Version2Entry_SameNamedTypeDifferentAssembly_OnlyBaselinedAssemblySuppressed()
    {
        // A version-2 baseline entry for Host.A.Program must not suppress the same-named
        // Host.B.Program violation — this is the exact P0 adopter scenario from issue #357, and it
        // must hold at runtime `validate --baseline` time, not just in `baseline diff`/`verify`.
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = "Program",
                ForbiddenReference = "System.Object",
                Reason = "known debt",
                IdentityVersion = 2,
                ContractFamily = "strict",
                Kind = "dependency",
                SourceAssembly = "Host.A",
                TargetAssembly = "mscorlib",
                TargetMember = "System.Object",
                Occurrence = 0,
            },
        };

        var contextForA = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict", baselineCandidates: new List<ArchitectureBaselineCandidate>());
        var contextForB = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict", baselineCandidates: new List<ArchitectureBaselineCandidate>());

        bool ignoredA = contextForA.IsIgnored("Program", "System.Object", sourceAssembly: "Host.A", targetAssembly: "mscorlib", targetMember: "System.Object");
        bool ignoredB = contextForB.IsIgnored("Program", "System.Object", sourceAssembly: "Host.B", targetAssembly: "mscorlib", targetMember: "System.Object");

        Assert.Multiple(() =>
        {
            Assert.That(ignoredA, Is.True, "Host.A.Program was explicitly baselined and must be suppressed.");
            Assert.That(ignoredB, Is.False, "Host.B.Program shares source/target text but a different assembly — it must still be reported.");
        });
    }

    [Test]
    public void IsIgnored_Version2Entry_SecondOccurrence_NotSuppressedByFirstOccurrencesEntry()
    {
        // A version-2 entry baselined at occurrence 0 must not suppress a second, distinct call to
        // the same target from the same source type (occurrence 1) — "multiple forbidden calls in
        // one type" must remain visible at runtime, not just at generate/diff time.
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = "MyApp.Service",
                ForbiddenReference = "line 10: -> Console.WriteLine",
                Reason = "known debt",
                IdentityVersion = 2,
                ContractFamily = "method_body",
                Kind = "call",
                TargetMember = "System.Console.WriteLine(string)",
                Occurrence = 0,
            },
        };

        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict_method_body", baselineCandidates: new List<ArchitectureBaselineCandidate>());

        bool firstCallIgnored = context.IsIgnored(
            "MyApp.Service", "line 10: -> Console.WriteLine", targetMember: "System.Console.WriteLine(string)");
        bool secondCallIgnored = context.IsIgnored(
            "MyApp.Service", "line 20: -> Console.WriteLine", targetMember: "System.Console.WriteLine(string)");

        Assert.Multiple(() =>
        {
            Assert.That(firstCallIgnored, Is.True, "Occurrence 0 was explicitly baselined.");
            Assert.That(secondCallIgnored, Is.False, "Occurrence 1 is a distinct call and was never baselined.");
        });
    }

    [Test]
    public void IsIgnored_SuppressedFirstOccurrence_PreservesLiveOccurrenceOnFindingCandidate()
    {
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = "MyApp.Service",
                ForbiddenReference = "line 10: -> Console.WriteLine",
                Reason = "known debt",
                IdentityVersion = 2,
                ContractFamily = "method_body",
                Kind = "call",
                TargetMember = "System.Console.WriteLine(string)",
                Occurrence = 0,
            },
        };
        var findingCandidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            "contract-name",
            "contract-id",
            ignoredViolations,
            enableUnmatchedIgnoreTracking: false,
            contractGroup: "strict_method_body",
            baselineCandidates: null,
            findingIdentityCandidates: findingCandidates);

        Assert.That(context.IsIgnored(
            "MyApp.Service", "line 10: -> Console.WriteLine",
            targetMember: "System.Console.WriteLine(string)"), Is.True);
        Assert.That(context.IsIgnored(
            "MyApp.Service", "line 20: -> Console.WriteLine",
            targetMember: "System.Console.WriteLine(string)"), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(findingCandidates, Has.Count.EqualTo(1));
            Assert.That(findingCandidates[0].Identity!.Occurrence, Is.EqualTo(1));
        });
    }

    [Test]
    public void IsIgnored_Version1LegacyEntry_MatchesByGlobPairRegardlessOfStructuredIdentity()
    {
        // Entries merged from a version-1 baseline (no IdentityVersion) must keep matching by the
        // exact legacy glob pair, completely unaffected by any live structured identity — no
        // reinterpretation of existing files.
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "Src.*", ForbiddenReference = "Ref.Type", Reason = "legacy glob" },
        };

        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict", baselineCandidates: new List<ArchitectureBaselineCandidate>());

        bool ignored = context.IsIgnored("Src.AnyType", "Ref.Type", sourceAssembly: "SomeAssembly", targetAssembly: "OtherAssembly");

        Assert.That(ignored, Is.True, "Legacy glob entries must keep matching by text, ignoring assembly.");
    }

    [Test]
    public void IsIgnored_StructuredWaiver_MatchesOnlyItsCanonicalTargetFingerprint()
    {
        ArchitectureViolationIdentity identity = new(
            ArchitectureViolationIdentity.CurrentVersion,
            "strict",
            "dependency",
            "contract-id",
            "Host.A",
            "Program",
            null,
            "mscorlib",
            null,
            "System.Object",
            0);
        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = "Program",
                ForbiddenReference = "System.Object",
                Reason = "Move the dependency behind the boundary.",
                WaiverId = "ARCH-IGN-001",
                Target = new ArchitectureWaiverTarget
                {
                    Fingerprint = ArchitectureWaiverTargetFingerprint.Create(identity)
                },
                Owner = "architecture-team",
                Issue = "ARCH-231",
                Introduced = "2026-08-12",
                Expires = "2026-10-01"
            }
        };
        var context = new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", ignoredViolations,
            enableUnmatchedIgnoreTracking: true, contractGroup: "strict", baselineCandidates: new List<ArchitectureBaselineCandidate>());

        bool exactMatch = context.IsIgnored(
            "Program", "System.Object", sourceAssembly: "Host.A", targetAssembly: "mscorlib", targetMember: "System.Object");
        bool differentOccurrence = context.IsIgnored(
            "Program", "System.Object", sourceAssembly: "Host.A", targetAssembly: "mscorlib", targetMember: "System.Object");

        Assert.Multiple(() =>
        {
            Assert.That(exactMatch, Is.True);
            Assert.That(differentOccurrence, Is.False, "The second occurrence has a distinct canonical target identity.");
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
