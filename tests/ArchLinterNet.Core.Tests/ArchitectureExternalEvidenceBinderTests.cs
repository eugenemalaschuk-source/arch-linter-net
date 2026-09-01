using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureExternalEvidenceBinderTests
{
    private SarifEvidenceTestRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new SarifEvidenceTestRepository();

    [TearDown]
    public void TearDown() => _repository.Dispose();

    [Test]
    public void Evaluate_NoRequirementsAndNoArtifacts_ReturnsEmpty()
    {
        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            Array.Empty<ArchitectureExternalEvidenceRequirement>(),
            _repository.Root,
            Array.Empty<SarifEvidenceArtifactReference>());

        Assert.Multiple(() =>
        {
            Assert.That(result.ImportedDiagnostics.Findings, Is.Empty);
            Assert.That(result.ApplicabilityExpectedEntries, Is.Empty);
            Assert.That(result.ApplicabilityRecords, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_OneRequiredArtifactWithFindings_ProjectsImportedFindings()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));

        Assert.Multiple(() =>
        {
            Assert.That(result.ImportedDiagnostics.Findings, Has.Count.EqualTo(1));
            Assert.That(result.ApplicabilityExpectedEntries, Has.Count.EqualTo(1));
            Assert.That(result.ApplicabilityRecords.Single().State,
                Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Evaluate_OneRequiredArtifactWithZeroFindings_IsEvaluableWithNoImportedFindings()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/zero.sarif", Sarif());

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/zero.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));

        Assert.Multiple(() =>
        {
            Assert.That(result.ImportedDiagnostics.Findings, Is.Empty);
            Assert.That(result.ApplicabilityRecords.Single().State,
                Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Evaluate_TwoIndependentRequiredEvidences_AreOrderIndependent()
    {
        ArchitectureExternalEvidenceRequirement first = Requirement("external.first");
        ArchitectureExternalEvidenceRequirement second = Requirement("external.second");
        _repository.AddUtf8File("evidence/first.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "first"), tool: "Synthetic.Scanner"));
        _repository.AddUtf8File("evidence/second.sarif", Sarif(
            Result("SEC100", "error", "src/App/Two.cs", "second"), tool: "Synthetic.Scanner"));
        SarifEvidenceArtifactReference firstArtifact = new("evidence/first.sarif", "external.first");
        SarifEvidenceArtifactReference secondArtifact = new("evidence/second.sarif", "external.second");
        SarifEvidenceAssessmentContext context = new("repo", "revision", "scope");

        ArchitectureExternalEvidenceBindingResult forward = ArchitectureExternalEvidenceBinder.Evaluate(
            [first, second], _repository.Root, [firstArtifact, secondArtifact], context);
        ArchitectureExternalEvidenceBindingResult reverse = ArchitectureExternalEvidenceBinder.Evaluate(
            [second, first], _repository.Root, [secondArtifact, firstArtifact], context);

        Assert.Multiple(() =>
        {
            Assert.That(forward.ImportedDiagnostics.Findings.Select(finding => finding.CanonicalIdentity),
                Is.EquivalentTo(reverse.ImportedDiagnostics.Findings.Select(finding => finding.CanonicalIdentity)));
            Assert.That(forward.ApplicabilityRecords.Select(record => record.ControlIdentity),
                Is.EquivalentTo(["external.first", "external.second"]));
        });
    }

    [Test]
    public void Evaluate_OptionalEvidenceAbsent_IsNotApplicable()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.optional", required: false);

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement], _repository.Root, Array.Empty<SarifEvidenceArtifactReference>());

        Assert.That(result.ApplicabilityRecords.Single().State,
            Is.EqualTo(ArchitectureApplicabilityRecordState.NotApplicable));
    }

    [Test]
    public void Evaluate_RequiredEvidenceMissing_IsUnassessable()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement], _repository.Root, Array.Empty<SarifEvidenceArtifactReference>());

        Assert.Multiple(() =>
        {
            Assert.That(result.ApplicabilityRecords.Single().State,
                Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(result.ImportedDiagnostics.Findings, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_WrongRevision_IsUnassessableAndNotSelected()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan", requireRevision: true);
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding"), revision: "old-revision"));

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "current-revision", "scope"));

        Assert.Multiple(() =>
        {
            Assert.That(result.ApplicabilityRecords.Single().State,
                Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(result.ImportedDiagnostics.Findings, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_RequiredBindingMetadataMissing_IsUnassessable()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan", requireScope: true);
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));

        ArchitectureExternalEvidenceBindingResult result = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", scope: null));

        Assert.That(result.ApplicabilityRecords.Single().State,
            Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
    }

    [Test]
    public void Evaluate_UnknownSuppliedBindingId_Throws()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));

        Assert.That(() => ArchitectureExternalEvidenceBinder.Evaluate(
                [requirement],
                _repository.Root,
                [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.unknown")]),
            Throws.ArgumentException);
    }

    [Test]
    public void Evaluate_DuplicateSuppliedBindingId_Throws()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/first.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));
        _repository.AddUtf8File("evidence/second.sarif", Sarif(
            Result("SEC100", "error", "src/App/Two.cs", "finding")));

        Assert.That(() => ArchitectureExternalEvidenceBinder.Evaluate(
                [requirement],
                _repository.Root,
                [
                    new SarifEvidenceArtifactReference("evidence/first.sarif", "external.scan"),
                    new SarifEvidenceArtifactReference("evidence/second.sarif", "external.scan"),
                ]),
            Throws.ArgumentException);
    }

    [Test]
    public void ValidateBindingIds_UnknownSuppliedId_Throws_WithoutTouchingDisk()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");

        // No SARIF file exists at this path anywhere — proves the failure comes from pure id
        // matching, never from an attempted read.
        Assert.That(() => ArchitectureExternalEvidenceBinder.ValidateBindingIds(
                [requirement],
                [new SarifEvidenceArtifactReference("evidence/does-not-exist.sarif", "external.unknown")]),
            Throws.ArgumentException);
    }

    [Test]
    public void ValidateBindingIds_DuplicateSuppliedId_Throws()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");

        Assert.That(() => ArchitectureExternalEvidenceBinder.ValidateBindingIds(
                [requirement],
                [
                    new SarifEvidenceArtifactReference("evidence/first.sarif", "external.scan"),
                    new SarifEvidenceArtifactReference("evidence/second.sarif", "external.scan"),
                ]),
            Throws.ArgumentException);
    }

    [Test]
    public void ValidateBindingIds_KnownAndDistinctIds_DoesNotThrow()
    {
        ArchitectureExternalEvidenceRequirement[] requirements =
        [
            Requirement("external.first"),
            Requirement("external.second"),
        ];

        Assert.That(() => ArchitectureExternalEvidenceBinder.ValidateBindingIds(
                requirements,
                [
                    new SarifEvidenceArtifactReference("evidence/first.sarif", "external.first"),
                    new SarifEvidenceArtifactReference("evidence/second.sarif", "external.second"),
                ]),
            Throws.Nothing);
    }

    [Test]
    public void Attach_EmptyBinding_ReturnsOutcomeUnchanged()
    {
        ValidationOutcome outcome = PassingOutcome();

        ValidationOutcome result = ArchitectureExternalEvidenceBinder.Attach(
            outcome, ArchitectureExternalEvidenceBindingResult.Empty, "strict");

        Assert.That(result, Is.SameAs(outcome));
    }

    [Test]
    public void Attach_UnassessableRequiredEvidence_MakesOutcomeUnassessableAndFails()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement], _repository.Root, Array.Empty<SarifEvidenceArtifactReference>());
        ValidationOutcome outcome = PassingOutcome();

        ValidationOutcome result = ArchitectureExternalEvidenceBinder.Attach(outcome, binding, "strict");

        Assert.Multiple(() =>
        {
            Assert.That(result.AssessmentCompletionEvidence!.State,
                Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
            Assert.That(result.Passed, Is.False);
            Assert.That(result.NativePassed, Is.False);
        });
    }

    [Test]
    public void Attach_ValidZeroResultEvidence_KeepsOutcomePassing()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/zero.sarif", Sarif());
        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/zero.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));
        ValidationOutcome outcome = PassingOutcome();

        ValidationOutcome result = ArchitectureExternalEvidenceBinder.Attach(outcome, binding, "strict");

        Assert.Multiple(() =>
        {
            Assert.That(result.AssessmentCompletionEvidence!.State,
                Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(result.Passed, Is.True);
        });
    }

    [Test]
    public void Attach_BlockingStrictImportedFinding_FailsOutcome()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            "external.scan", severity: new Dictionary<string, string> { ["error"] = "strict" });
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "error", "src/App/One.cs", "finding")));
        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));
        ValidationOutcome outcome = PassingOutcome();

        ValidationOutcome result = ArchitectureExternalEvidenceBinder.Attach(outcome, binding, "strict");

        Assert.Multiple(() =>
        {
            Assert.That(result.ImportedDiagnostics.HasBlockingFindings, Is.True);
            Assert.That(result.Passed, Is.False);
        });
    }

    [Test]
    public void Attach_NonBlockingAuditImportedFinding_KeepsOutcomePassing()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            "external.scan", severity: new Dictionary<string, string> { ["warning"] = "audit" });
        _repository.AddUtf8File("evidence/current.sarif", Sarif(
            Result("SEC100", "warning", "src/App/One.cs", "finding")));
        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/current.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));
        ValidationOutcome outcome = PassingOutcome();

        ValidationOutcome result = ArchitectureExternalEvidenceBinder.Attach(outcome, binding, "audit");

        Assert.Multiple(() =>
        {
            Assert.That(result.ImportedDiagnostics.Findings, Has.Count.EqualTo(1));
            Assert.That(result.ImportedDiagnostics.HasBlockingFindings, Is.False);
            Assert.That(result.Passed, Is.True);
        });
    }

    [Test]
    public void Attach_DoesNotDuplicateApplicabilityWhenCalledOnceEach_AcrossTwoOutcomes()
    {
        // Guards against the cache-population ordering bug this design explicitly avoids: Attach
        // must never be applied twice to the same outcome's accumulating collections.
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        _repository.AddUtf8File("evidence/zero.sarif", Sarif());
        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            [requirement],
            _repository.Root,
            [new SarifEvidenceArtifactReference("evidence/zero.sarif", "external.scan")],
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));
        ValidationOutcome strictOutcome = PassingOutcome();
        ValidationOutcome auditOutcome = PassingOutcome();

        ValidationOutcome strictResult = ArchitectureExternalEvidenceBinder.Attach(strictOutcome, binding, "strict");
        ValidationOutcome auditResult = ArchitectureExternalEvidenceBinder.Attach(auditOutcome, binding, "audit");

        Assert.Multiple(() =>
        {
            Assert.That(strictResult.ApplicabilityRecords, Has.Count.EqualTo(1));
            Assert.That(auditResult.ApplicabilityRecords, Has.Count.EqualTo(1));
        });
    }

    private static ValidationOutcome PassingOutcome() => new(
        Passed: true,
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
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());

    private static ArchitectureExternalEvidenceRequirement Requirement(
        string id,
        bool required = true,
        bool requireRevision = false,
        bool requireScope = false,
        Dictionary<string, string>? severity = null) => new()
        {
            Id = id,
            Format = "sarif",
            Required = required,
            Tool = "Synthetic.Scanner",
            ToolVersion = "1.0",
            Run = "assessment-42",
            RequireRevision = requireRevision,
            RequireScope = requireScope,
            DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
            {
                Severity = severity ?? new Dictionary<string, string> { ["error"] = "strict" },
            },
        };

    private static string Result(string ruleId, string level, string path, string message) =>
        "{\"ruleId\":\"" + ruleId + "\",\"message\":{\"text\":\"" + message + "\"},\"level\":\"" + level
        + "\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\""
        + path + "\"},\"region\":{\"startLine\":7,\"startColumn\":3}}}]}";

    private static string Sarif(
        string? result = null,
        string? revision = "revision",
        string tool = "Synthetic.Scanner") =>
        "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"" + tool + "\",\"version\":\"1.0\","
        + "\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[\"security\"]}}]}},"
        + "\"automationDetails\":{\"id\":\"assessment-42\"},\"invocations\":[{\"executionSuccessful\":true}],"
        + "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"" + revision + "\"}],"
        + "\"results\":[" + (result ?? string.Empty) + "]}]}";
}
