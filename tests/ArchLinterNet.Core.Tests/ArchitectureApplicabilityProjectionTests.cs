using System.Text.Json;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureApplicabilityProjectionTests
{
    [Test]
    public void Project_SummaryUsesExpectedMembershipAndValidatedStates()
    {
        ArchitectureApplicabilityExpectedEntry[] expected =
        [
            Entry("required-evaluable", ArchitectureApplicabilityMembership.Required),
            Entry("required-unassessable", ArchitectureApplicabilityMembership.Required),
            Entry("optional", ArchitectureApplicabilityMembership.Optional),
            Entry("not-applicable", ArchitectureApplicabilityMembership.NotApplicable),
        ];
        ArchitectureApplicabilityRecord[] records =
        [
            Record("required-evaluable", ArchitectureApplicabilityRecordState.Evaluable),
            Record("required-unassessable", ArchitectureApplicabilityRecordState.Unassessable,
                ArchitectureApplicabilityReasonCodes.StaleDeclaration),
            Record("optional", ArchitectureApplicabilityRecordState.NotApplicable),
            Record("not-applicable", ArchitectureApplicabilityRecordState.NotApplicable),
        ];

        ArchitectureAssessmentCompletionEvidence completion =
            ArchitectureApplicabilityEvaluator.Evaluate(expected, records, conformancePassed: true)!;
        ArchitectureApplicabilityProjection projection =
            ArchitectureApplicabilityProjector.Project(completion, "strict")!;

        Assert.Multiple(() =>
        {
            Assert.That(projection.Summary.RequiredCount, Is.EqualTo(2));
            Assert.That(projection.Summary.RequiredEvaluableCount, Is.EqualTo(1));
            Assert.That(projection.Summary.RequiredUnassessableCount, Is.EqualTo(1));
            Assert.That(projection.Summary.EvaluableCount, Is.EqualTo(1));
            Assert.That(projection.Summary.UnassessableCount, Is.EqualTo(1));
            Assert.That(projection.Summary.OptionalCount, Is.EqualTo(1));
            Assert.That(projection.Summary.NotApplicableCount, Is.EqualTo(2));
            Assert.That(projection.Findings, Has.Count.EqualTo(1));
            Assert.That(projection.Findings.Single().Details, Is.TypeOf<ArchitectureApplicabilityDiagnostic>());
        });
    }

    [TestCase(ArchitectureApplicabilityReasonCodes.MissingRequiredInput)]
    [TestCase(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput)]
    [TestCase(ArchitectureApplicabilityReasonCodes.UnmappedSubject)]
    [TestCase(ArchitectureApplicabilityReasonCodes.AmbiguousSubject)]
    [TestCase(ArchitectureApplicabilityReasonCodes.StaleDeclaration)]
    public void Project_EmitsKnownUnassessableReason(string reasonCode)
    {
        ArchitectureApplicabilityReason reason = new(reasonCode, "topology", "control", "policy");
        ArchitectureAssessmentCompletionEvidence completion = new(
            ArchitectureAssessmentCompletionState.Unassessable,
            [new ArchitectureApplicabilityAssessment(
                Entry("control", ArchitectureApplicabilityMembership.Required, "topology"),
                Record("control", ArchitectureApplicabilityRecordState.Unassessable, reasonCode, "topology"),
                Array.Empty<ArchitectureApplicabilityReason>())],
            [reason]);

        ArchitectureFinding finding =
            ArchitectureApplicabilityProjector.ToFindings(completion, "audit").Single();
        ArchitectureApplicabilityDiagnostic details =
            (ArchitectureApplicabilityDiagnostic)finding.Details;

        Assert.Multiple(() =>
        {
            Assert.That(details.ReasonCode, Is.EqualTo(reasonCode));
            Assert.That(details.ControlIdentity, Is.EqualTo("control"));
            Assert.That(details.Family, Is.EqualTo("topology"));
            Assert.That(details.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(details.ValidatedState, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(finding.Mode, Is.EqualTo("audit"));
            Assert.That(finding.Severity, Is.EqualTo("warning"));
        });
    }

    [Test]
    public void Project_DeduplicatesReasonsByCanonicalEvidenceAndKeepsIntegrityReasons()
    {
        ArchitectureApplicabilityExpectedEntry expected = Entry(
            "required", ArchitectureApplicabilityMembership.Required);
        ArchitectureApplicabilityRecord duplicateRecordA = Record(
            "orphan", ArchitectureApplicabilityRecordState.Evaluable);
        ArchitectureApplicabilityRecord duplicateRecordB = Record(
            "orphan", ArchitectureApplicabilityRecordState.Evaluable);
        ArchitectureAssessmentCompletionEvidence completion =
            ArchitectureApplicabilityEvaluator.Evaluate(
                [expected],
                [Record("required", ArchitectureApplicabilityRecordState.NotApplicable), duplicateRecordA, duplicateRecordB],
                conformancePassed: true)!;

        ArchitectureApplicabilityProjection projection = ArchitectureApplicabilityProjector.Project(completion)!;

        Assert.Multiple(() =>
        {
            Assert.That(completion.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(completion.Controls.Select(control => control.ControlIdentity).ToArray(), Is.EqualTo(["orphan", "orphan", "required"]));
            Assert.That(projection.Findings.Select(finding =>
                ((ArchitectureApplicabilityDiagnostic)finding.Details).ReasonCode),
                Is.EqualTo([
                    ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity,
                    ArchitectureApplicabilityReasonCodes.IncompatibleApplicabilityRecord,
                ]));
            Assert.That(projection.Findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(),
                Is.EqualTo(projection.Findings.Count));
        });
    }

    [Test]
    public void Finding_UsesStructuredControlFamilyPolicyAndReasonIdentityAndDetails()
    {
        ArchitectureApplicabilityReason reason = new(
            ArchitectureApplicabilityReasonCodes.AmbiguousSubject,
            "exposure",
            "control/a",
            "policy-v08");
        ArchitectureApplicabilityDiagnostic diagnostic = new(
            "control/a", "exposure", ArchitectureApplicabilityMembership.Required,
            ArchitectureApplicabilityRecordState.Unassessable,
            ArchitectureApplicabilityRecordState.Unassessable, reason);
        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(diagnostic, "strict");
        Dictionary<string, object?> json = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("applicability"));
            Assert.That(finding.Identity!.ContractFamily, Is.EqualTo("exposure"));
            Assert.That(finding.Identity.SourceType, Is.EqualTo("control/a"));
            Assert.That(finding.Identity.SourceMember, Is.EqualTo("exposure"));
            Assert.That(finding.Identity.TargetMember, Is.EqualTo(ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
            Assert.That(finding.Identity.Configuration, Is.EqualTo("policy-v08"));
            Assert.That(finding.CanonicalIdentity, Does.Not.Contain("display"));
            Assert.That(json["message_code"], Is.EqualTo("applicability"));
            Assert.That(((Dictionary<string, object?>)json["details"]!)["reason_code"],
                Is.EqualTo(ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
        });
    }

    [Test]
    public void CacheRoundTrip_PreservesCanonicalApplicabilityProjectionEvidence()
    {
        ArchitectureApplicabilityExpectedEntry[] expected =
        [Entry("a", ArchitectureApplicabilityMembership.Required), Entry("b", ArchitectureApplicabilityMembership.Optional)];
        ArchitectureApplicabilityRecord[] records =
        [
            Record("a", ArchitectureApplicabilityRecordState.Unassessable, ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput),
            Record("b", ArchitectureApplicabilityRecordState.NotApplicable),
        ];
        ArchitectureAssessmentCompletionEvidence completion =
            ArchitectureApplicabilityEvaluator.Evaluate(expected, records, conformancePassed: true)!;
        ValidationOutcome original = new(
            Passed: false,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>(),
            CoverageFindings: Array.Empty<ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
            PolicyConsistencyConfig: "off",
            CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
            ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
            ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            ApplicabilityExpectedEntries = expected,
            ApplicabilityRecords = records,
            AssessmentCompletionEvidence = completion,
            ApplicabilityProjection = ArchitectureApplicabilityProjector.Project(completion, "strict"),
        };

        AnalysisCacheOutcomeV1 cache = AnalysisCacheOutcomeMapper.ToCacheOutcome(original);
        string json = JsonSerializer.Serialize(cache, AnalysisCacheJson.Options);
        AnalysisCacheOutcomeV1 rehydrated = JsonSerializer.Deserialize<AnalysisCacheOutcomeV1>(
            json, AnalysisCacheJson.Options)!;
        ValidationOutcome reconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
            rehydrated, "/repo", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            ArchitectureSourceExpansionInventory.Empty, "strict");

        ArchitectureApplicabilityProjection actual = reconstructed.ApplicabilityProjection!;
        Assert.Multiple(() =>
        {
            Assert.That(
                CanonicalJson(reconstructed.ApplicabilityExpectedEntries),
                Is.EqualTo(CanonicalJson(original.ApplicabilityExpectedEntries)));
            Assert.That(
                CanonicalJson(reconstructed.ApplicabilityRecords),
                Is.EqualTo(CanonicalJson(original.ApplicabilityRecords)));
            Assert.That(
                CanonicalJson(reconstructed.AssessmentCompletionEvidence),
                Is.EqualTo(CanonicalJson(original.AssessmentCompletionEvidence)));
            Assert.That(actual.Summary, Is.EqualTo(original.ApplicabilityProjection!.Summary));
            Assert.That(actual.Findings.Select(finding => finding.CanonicalIdentity),
                Is.EqualTo(original.ApplicabilityProjection!.Findings.Select(finding => finding.CanonicalIdentity)));
            Assert.That(
                actual.Findings.Select(finding => CanonicalJson(ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding))),
                Is.EqualTo(original.ApplicabilityProjection!.Findings.Select(finding =>
                    CanonicalJson(ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding)))));
        });
    }

    private static string CanonicalJson<T>(T value) =>
        JsonSerializer.Serialize(value);

    private static ArchitectureApplicabilityExpectedEntry Entry(
        string identity,
        ArchitectureApplicabilityMembership membership,
        string family = "family") =>
        new(identity, family, membership,
            new ArchitectureApplicabilityProvenance(family, identity, "policy"));

    private static ArchitectureApplicabilityRecord Record(
        string identity,
        ArchitectureApplicabilityRecordState state,
        string? reasonCode = null,
        string family = "family")
    {
        ArchitectureApplicabilityReason[] reasons = reasonCode is null
            ? Array.Empty<ArchitectureApplicabilityReason>()
            : [new ArchitectureApplicabilityReason(reasonCode, family, identity, "policy")];
        return new ArchitectureApplicabilityRecord(
            identity, family, state, reasons,
            new ArchitectureApplicabilityProvenance(family, identity, "policy"));
    }
}
