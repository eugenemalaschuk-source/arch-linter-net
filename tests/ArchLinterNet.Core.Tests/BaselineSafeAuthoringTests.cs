using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Covers the safety and reviewability guarantees of baseline authoring: ambiguity detection, reason
/// mapping, comment preservation, and preserved issue metadata.
/// </summary>
[TestFixture]
public sealed class BaselineSafeAuthoringTests
{
    private static readonly string[] _sharedLifecycleNames =
        ["new", "matched", "resolved", "stale", "changed", "ambiguous", "configuration-error"];

    private static readonly int[] _twoUnanchorableCommentLines = [3, 5];

    private static readonly int[] _oneUnanchorableCommentLine = [2];

    [Test]
    public void Compare_LegacyEntryMatchingSeveralCandidates_IsAmbiguousNotFrozen()
    {
        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            CreatePolicy("legacy-rule"),
            CreateLegacyBaseline("legacy-rule", "Src.Type", "Ref.Type", reason: "legacy debt"),
            // Two distinct occurrences project onto the same legacy pair: one entry would suppress both.
            [
                new ArchitectureBaselineCandidate("strict", "legacy-rule", "Src.Type", "Ref.Type"),
                new ArchitectureBaselineCandidate("strict", "legacy-rule", "Src.Type", "Ref.Type"),
            ],
            mode: "all");

        Assert.Multiple(() =>
        {
            Assert.That(result.Ambiguous, Has.Count.EqualTo(1));
            Assert.That(result.Ambiguous[0].SourceType, Is.EqualTo("Src.Type"));
            Assert.That(result.Frozen, Is.Empty);
            Assert.That(result.Resolved, Is.Empty);
        });
    }

    [Test]
    public void Compare_LegacyEntryMatchingExactlyOneCandidate_IsFrozen()
    {
        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            CreatePolicy("legacy-rule"),
            CreateLegacyBaseline("legacy-rule", "Src.Type", "Ref.Type", reason: "legacy debt"),
            [new ArchitectureBaselineCandidate("strict", "legacy-rule", "Src.Type", "Ref.Type")],
            mode: "all");

        Assert.Multiple(() =>
        {
            Assert.That(result.Frozen, Has.Count.EqualTo(1));
            Assert.That(result.Ambiguous, Is.Empty);
        });
    }

    [Test]
    public void Compare_EntryWithIssueMetadata_CarriesItOntoTheComparisonEntry()
    {
        ArchitectureBaselineDocument baseline = CreateLegacyBaseline(
            "legacy-rule", "Src.Type", "Ref.Type", reason: "legacy debt", issue: "PROJ-42");

        ArchitectureBaselineComparisonResult result = ArchitectureBaselineComparer.Compare(
            CreatePolicy("legacy-rule"),
            baseline,
            [new ArchitectureBaselineCandidate("strict", "legacy-rule", "Src.Type", "Ref.Type")],
            mode: "all");

        Assert.That(result.Frozen[0].Issue, Is.EqualTo("PROJ-42"));
    }

    [Test]
    public void Serialize_EntryWithIssueMetadata_RoundTripsThroughTheDocument()
    {
        ArchitectureBaselineGenerator generator = CreateGenerator();
        ArchitectureBaselineDocument document = generator.BuildFromEntries(
            [
                new ArchitectureBaselineComparisonEntry("strict", "legacy-rule", "Src.Type", "Ref.Type", "legacy debt")
                {
                    Issue = "PROJ-42",
                },
            ],
            version: 1);

        string yaml = generator.Serialize(document);

        Assert.Multiple(() =>
        {
            Assert.That(document.Baseline.Strict[0].IgnoredViolations[0].Issue, Is.EqualTo("PROJ-42"));
            Assert.That(yaml, Does.Contain("issue: PROJ-42"));
        });
    }

    [Test]
    public void ReasonMap_ResolvesContractThenFamilyThenDefault()
    {
        bool parsed = BaselineReasonMap.TryParse(
            ["app-boundaries=contract debt"],
            ["package_dependency=package debt"],
            "flat debt",
            out BaselineReasonMap map,
            out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(map.Resolve("app-boundaries", "strict"), Is.EqualTo("contract debt"));
            Assert.That(map.Resolve("other-rule", "strict_package_dependency"), Is.EqualTo("package debt"));
            Assert.That(map.Resolve("other-rule", "audit_package_dependency"), Is.EqualTo("package debt"));
            Assert.That(map.Resolve("other-rule", "strict_composition"), Is.EqualTo("flat debt"));
        });
    }

    [Test]
    public void ReasonMap_ContractMappingWinsOverFamilyMapping()
    {
        BaselineReasonMap.TryParse(
            ["app-boundaries=contract debt"], ["strict=family debt"], "flat debt", out BaselineReasonMap map, out _);

        Assert.That(map.Resolve("app-boundaries", "strict"), Is.EqualTo("contract debt"));
    }

    [TestCase("package_dependency", "--reason-for-family expects")]
    [TestCase("=reason text", "empty key")]
    [TestCase("package_dependency=", "empty reason text")]
    public void ReasonMap_MalformedMapping_FailsClosedWithAnExplicitMessage(string mapping, string expectedError)
    {
        bool parsed = BaselineReasonMap.TryParse(
            null, [mapping], "flat debt", out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(error, Does.Contain(expectedError));
        });
    }

    [Test]
    public void ReasonMap_DuplicateKey_FailsClosed()
    {
        bool parsed = BaselineReasonMap.TryParse(
            null, ["composition=first", "composition=second"], "flat debt", out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(error, Does.Contain("more than one mapping"));
        });
    }

    [Test]
    public void CommentInspector_LeadingCommentBlock_IsPreservableHeader()
    {
        BaselineCommentInspection inspection = BaselineCommentInspector.Inspect(
            "# Reviewed baseline\n# Tracked in #123\n\nversion: 2\nbaseline: {}\n");

        Assert.Multiple(() =>
        {
            Assert.That(inspection.CanRoundTrip, Is.True);
            Assert.That(inspection.HasHeader, Is.True);
            Assert.That(inspection.Header, Does.Contain("# Reviewed baseline"));
            Assert.That(inspection.Header, Does.Contain("# Tracked in #123"));
        });
    }

    [Test]
    public void CommentInspector_LeadingCommentBlock_IsPreservedVerbatim()
    {
        const string Header = "# Reviewed baseline\r\n\r\n# Tracked in #123\r\n\r\n";
        BaselineCommentInspection inspection = BaselineCommentInspector.Inspect(Header + "version: 2\r\n");

        Assert.That(inspection.Header, Is.EqualTo(Header));
    }

    [Test]
    public void CommentInspector_CommentAfterContent_IsReportedByLineNumber()
    {
        BaselineCommentInspection inspection = BaselineCommentInspector.Inspect(
            "# header\nversion: 2\n# a note about the entry below\nbaseline: {}\n  # and another\n");

        Assert.Multiple(() =>
        {
            Assert.That(inspection.CanRoundTrip, Is.False);
            Assert.That(inspection.UnanchorableCommentLines, Is.EqualTo(_twoUnanchorableCommentLines));
            Assert.That(inspection.Header, Is.EqualTo("# header" + Environment.NewLine));
        });
    }

    [Test]
    public void LifecycleNames_AreExactlyTheSharedVocabulary()
    {
        // Fixed by the adoption-stabilization-compatibility capability. A command inventing its own
        // status word is the compatibility break this guards against.
        Assert.That(
            BaselineEntryLifecycleNames.All,
            Is.EqualTo(_sharedLifecycleNames));
    }

    [Test]
    public void LifecycleNames_OnlyMatchedSuppressesAFinding()
    {
        BaselineEntryLifecycle[] nonSuppressing =
        [
            BaselineEntryLifecycle.New,
            BaselineEntryLifecycle.Resolved,
            BaselineEntryLifecycle.Stale,
            BaselineEntryLifecycle.Changed,
            BaselineEntryLifecycle.Ambiguous,
            BaselineEntryLifecycle.ConfigurationError,
        ];

        Assert.Multiple(() =>
        {
            Assert.That(BaselineEntryLifecycleNames.Suppresses(BaselineEntryLifecycle.Matched), Is.True);
            foreach (BaselineEntryLifecycle lifecycle in nonSuppressing)
            {
                Assert.That(BaselineEntryLifecycleNames.Suppresses(lifecycle), Is.False, lifecycle.ToString());
            }
        });
    }

    [TestCase("reason: legacy debt # reviewed by Alice", true, TestName = "trailing comment after content")]
    [TestCase("  forbidden_reference: Infra.Db   # keep until Q4", true, TestName = "trailing comment after indented content")]
    [TestCase("reason: \"contains # inside double quotes\"", false, TestName = "hash inside a double-quoted scalar")]
    [TestCase("reason: 'contains # inside single quotes'", false, TestName = "hash inside a single-quoted scalar")]
    [TestCase("reason: \"ends with escaped backslash\\\\\" # reviewed", true, TestName = "hash after a double-quote preceded by two backslashes")]
    [TestCase("source_type: MyApp.Tagged#1", false, TestName = "hash mid-token is not a comment")]
    public void CommentInspector_TrailingComments_AreDetectedButQuotedHashesAreNot(string contentLine, bool expectRefusal)
    {
        BaselineCommentInspection inspection = BaselineCommentInspector.Inspect(
            "version: 2\n" + contentLine + "\n");

        Assert.That(inspection.CanRoundTrip, Is.EqualTo(!expectRefusal));
        if (expectRefusal)
        {
            Assert.That(inspection.UnanchorableCommentLines, Is.EqualTo(_oneUnanchorableCommentLine));
        }
    }

    [Test]
    public void CommentInspector_NoComments_HasNoHeaderAndRoundTrips()
    {
        BaselineCommentInspection inspection = BaselineCommentInspector.Inspect("version: 2\nbaseline: {}\n");

        Assert.Multiple(() =>
        {
            Assert.That(inspection.CanRoundTrip, Is.True);
            Assert.That(inspection.HasHeader, Is.False);
            Assert.That(inspection.Header, Is.Empty);
        });
    }

    [Test]
    public void CommentInspector_Refusal_NamesLinesAndTheDryRunPathForward()
    {
        string refusal = BaselineCommentInspector.DescribeRefusal("baseline update", "baseline.yml", [3, 5]);

        Assert.Multiple(() =>
        {
            Assert.That(refusal, Does.Contain("baseline.yml"));
            Assert.That(refusal, Does.Contain("line(s) 3, 5"));
            Assert.That(refusal, Does.Contain("--dry-run"));
        });
    }

    private static ArchitectureContractDocument CreatePolicy(string contractId)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = contractId, Name = contractId, Source = "core" },
                },
            },
        };
    }

    private static ArchitectureBaselineDocument CreateLegacyBaseline(
        string contractId, string sourceType, string forbiddenReference, string reason, string? issue = null)
    {
        return new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = contractId,
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = sourceType,
                                ForbiddenReference = forbiddenReference,
                                Reason = reason,
                                Issue = issue,
                            },
                        },
                    },
                },
            },
        };
    }

    private static ArchitectureBaselineGenerator CreateGenerator()
    {
        return new ArchitectureBaselineGenerator();
    }
}
