using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalDiagnosticsFederationReferenceScenarioTests : ExternalDiagnosticsFederationReferenceScenarioTestSupport
{
    [Test]
    public void CurrentContext_ComposesTrustSelectionFindingBaselineOutputsTestingAndApplicability()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            "external.scan",
            severity: new Dictionary<string, string>
            {
                ["error"] = "strict",
                ["warning"] = "audit",
            });
        string sarif = Sarif(
            Results(
                Result("SEC100", "error", "src/App/One.cs", "source finding", fingerprint: "{\"stable\":\"source-42\"}"),
                Result("PUBLICAPI001", "warning", "src/App/Two.cs", "fallback compatibility finding", partialFingerprint: "{\"partial\":\"ignored-for-selection\"}")));
        string path = Repository.AddUtf8File("evidence/current.sarif", sarif);
        SarifEvidenceReadResult read = Read(
            requirement,
            "evidence/current.sarif",
            new SarifEvidenceProducerContext("external.scan", "repo", "revision", "scope"));
        SarifExternalDiagnosticSelectionResult selection = Select(read);
        ImportedExternalDiagnosticProjection imported = ArchitectureImportedDiagnosticProjector.Project(selection);
        IReadOnlyList<ArchitectureBaselineCandidate> baselines =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(selection);
        ArchitectureBaselineCandidate strictBaseline = baselines.Single(candidate =>
            candidate.ContractGroup == "strict_external");
        var baselinePolicy = new ArchitectureContractDocument
        {
            ExternalEvidence = [requirement],
        };
        var baselineGenerator = new ArchitectureBaselineGenerator();
        ArchitectureBaselineDocument baseline = baselineGenerator.Generate(baselinePolicy, [strictBaseline]);
        string baselinePath = Repository.GetPath("evidence/baseline.yml");
        File.WriteAllText(baselinePath, baselineGenerator.Serialize(baseline));
        ArchitectureBaselineDocument loadedBaseline = new ArchitectureBaselineLoadingService().LoadFromPath(baselinePath);
        ArchitectureBaselineLoadingService.MergeAndValidate(baselinePolicy, loadedBaseline);
        ArchitectureBaselineComparisonResult baselineComparison = ArchitectureBaselineComparer.Compare(
            baselinePolicy, loadedBaseline, [strictBaseline], "strict");
        ArchitectureFinding strict = imported.Findings.Single(finding => finding.Mode == "strict");
        ArchitectureFinding audit = imported.Findings.Single(finding => finding.Mode == "audit");
        var strictDetail = (ImportedExternalDiagnostic)strict.Details;
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected,
            IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(
                [requirement], [read], selection);
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, conformancePassed: true)!;
        Dictionary<string, object?> json = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(strict);
        string human = ArchitectureDiagnosticFormatter.FormatFindingsForHumans(imported.Findings);
        ArchitectureFindingReadEnvelope rehydrated = ArchitectureFindingJsonReader.Read(
            JsonSerializer.Serialize(json), strict: true);
        using JsonDocument output = JsonDocument.Parse(
            ArchitectureSarifFormatter.FormatFindingsAsSarif(imported.Findings, "9.9.9"));
        JsonElement sarifResult = output.RootElement.GetProperty("runs")[0]
            .GetProperty("results").EnumerateArray()
            .Single(result => result.GetProperty("properties").GetProperty("arch_linter_net")
                .GetProperty("canonical_identity").GetString() == strict.CanonicalIdentity);
        var testing = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = imported,
        });

        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sarif)));
        ExternalDiagnosticsOutputParityAssertions.AssertOutputParity(
            strictDetail, strict, json, human, sarifResult, testing, expectedHash);
        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(read.LogicalId, Is.EqualTo("external.scan"));
            Assert.That(read.ToolName, Is.EqualTo("Synthetic.Scanner"));
            Assert.That(read.RunId, Is.EqualTo("assessment-42"));
            Assert.That(read.ArtifactPath, Is.EqualTo("evidence/current.sarif"));
            Assert.That(read.ArtifactSha256, Is.EqualTo(expectedHash));
            Assert.That(read.Context, Is.EqualTo(new SarifEvidenceResolvedContext(
                "external.scan", "repo", "revision", "scope")));
            Assert.That(selection.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(strictDetail.LogicalEvidenceId, Is.EqualTo("external.scan"));
            Assert.That(strictDetail.EvidenceProvenances.Single().ToolName, Is.EqualTo("Synthetic.Scanner"));
            Assert.That(strictDetail.EvidenceProvenances.Single().RunId, Is.EqualTo("assessment-42"));
            Assert.That(strictDetail.EvidenceProvenances.Single().Context!.Repository, Is.EqualTo("repo"));
            Assert.That(strictDetail.EvidenceProvenances.Single().Context!.Revision, Is.EqualTo("revision"));
            Assert.That(strictDetail.EvidenceProvenances.Single().Context!.Scope, Is.EqualTo("scope"));
            Assert.That(strictDetail.Fingerprint.Origin, Is.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Source));
            Assert.That(strictDetail.Fingerprint.Value, Is.EqualTo("source-42"));
            Assert.That(((ImportedExternalDiagnostic)audit.Details).Fingerprint.Origin,
                Is.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Deterministic));
            Assert.That(((ImportedExternalDiagnostic)audit.Details).SourceDiagnostic.RuleId,
                Is.EqualTo("PUBLICAPI001"));
            Assert.That(((ImportedExternalDiagnostic)audit.Details).Fingerprint.Value,
                Does.Match("^sha256:[0-9a-f]{64}$"));
            Assert.That(baselines.Select(candidate => candidate.ContractGroup),
                Is.EquivalentTo(["audit_external", "strict_external"]));
            Assert.That(baselines.Select(candidate => candidate.Identity),
                Is.EquivalentTo(imported.Findings.Select(finding => finding.Identity)));
            Assert.That(baselineComparison.New, Is.Empty);
            Assert.That(baselineComparison.Frozen.Single().Identity, Is.EqualTo(strictBaseline.Identity));
            Assert.That(strict.SourceLocation, Is.EqualTo(new ArchitectureFindingSourceLocation(
                "src/App/One.cs", 7, 3)));
            Assert.That(rehydrated.Kind, Is.EqualTo("imported_external_diagnostic"));
            Assert.That(rehydrated.CanonicalIdentity, Is.EqualTo(strict.CanonicalIdentity));
            Assert.That(records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(records.Single().Reasons, Is.Empty);
            Assert.That(records.Single().Provenance,
                Is.EqualTo(new ArchitectureApplicabilityProvenance("external_diagnostics", "external.scan", "external.scan")));
            Assert.That(completion.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(path, Does.EndWith("evidence" + Path.DirectorySeparatorChar + "current.sarif"));
        });
    }

    [Test]
    public void TrustedZeroResult_IsEvaluableAndDistinctFromMissingEvidence()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        Repository.AddUtf8File("evidence/zero.sarif", Sarif("[]"));
        SarifEvidenceReadResult valid = Read(requirement, "evidence/zero.sarif");
        SarifExternalDiagnosticSelectionResult selection = Select(valid);
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(selection);
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected,
            IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project([requirement], [valid], selection);
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, conformancePassed: true)!;
        SarifEvidenceReadResult missing = new SarifEvidenceReader().Read(
            requirement, Repository.Root, artifact: null,
            new SarifEvidenceAssessmentContext("repo", "revision"));
        (_, IReadOnlyList<ArchitectureApplicabilityRecord> missingRecords) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project([requirement], [missing]);
        ArchitectureAssessmentCompletionEvidence missingCompletion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, missingRecords, conformancePassed: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(valid.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(valid.ResultCount, Is.EqualTo(0));
            Assert.That(selection.Diagnostics, Is.Empty);
            Assert.That(projection.Findings, Is.Empty);
            Assert.That(records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(records.Single().Reasons, Is.Empty);
            Assert.That(records.Single().Provenance,
                Is.EqualTo(new ArchitectureApplicabilityProvenance("external_diagnostics", "external.scan", "external.scan")));
            Assert.That(completion.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Pass));
            Assert.That(missing.Status, Is.EqualTo(SarifEvidenceTrustStatus.MissingRequiredInput));
            Assert.That(missingRecords.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(missingCompletion.State, Is.EqualTo(ArchitectureAssessmentCompletionState.Unassessable));
        });
    }

    [TestCase("missing", SarifEvidenceTrustStatus.MissingRequiredInput, "missing_required_input")]
    [TestCase("malformed", SarifEvidenceTrustStatus.MalformedInput, "malformed_external_input")]
    [TestCase("failed", SarifEvidenceTrustStatus.FailedExecution, "malformed_external_input")]
    [TestCase("incomplete", SarifEvidenceTrustStatus.IncompleteExecution, "malformed_external_input")]
    [TestCase("wrong-key", SarifEvidenceTrustStatus.WrongLogicalId, "wrong_external_evidence_identity")]
    [TestCase("wrong-repository", SarifEvidenceTrustStatus.WrongRepository, "wrong_external_repository")]
    [TestCase("wrong-revision", SarifEvidenceTrustStatus.WrongRevision, "wrong_external_revision")]
    [TestCase("wrong-scope", SarifEvidenceTrustStatus.WrongScope, "wrong_external_scope")]
    [TestCase("missing-repository", SarifEvidenceTrustStatus.MissingRepository, "malformed_external_input")]
    [TestCase("missing-revision", SarifEvidenceTrustStatus.MissingRevision, "malformed_external_input")]
    [TestCase("missing-scope", SarifEvidenceTrustStatus.MissingScope, "malformed_external_input")]
    public void RequiredInvalidOrStaleEvidence_IsUnassessableAndCannotBeSelected(
        string scenario,
        SarifEvidenceTrustStatus expectedStatus,
        string expectedReasonCode)
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        SarifEvidenceArtifactReference? artifact = null;
        SarifEvidenceProducerContext? producer = null;
        string? repository = "repo";
        string? revision = "revision";
        string invocation = "true";
        bool includeInvocations = true;
        string content = string.Empty;
        bool malformed = false;

        switch (scenario)
        {
            case "malformed":
                content = "{not SARIF";
                malformed = true;
                break;
            case "failed":
                invocation = "false";
                content = Sarif("[]", invocation: invocation);
                break;
            case "incomplete":
                includeInvocations = false;
                content = Sarif("[]", includeInvocations: includeInvocations);
                break;
            case "wrong-key":
                artifact = new SarifEvidenceArtifactReference("evidence/input.sarif", "other.scan");
                break;
            case "wrong-repository":
                repository = "other-repo";
                break;
            case "wrong-revision":
                revision = "other-revision";
                break;
            case "wrong-scope":
                producer = new SarifEvidenceProducerContext("external.scan", "repo", "revision", "other-scope");
                break;
            case "missing-repository":
                repository = null;
                break;
            case "missing-revision":
                revision = null;
                break;
            case "missing-scope":
                break;
            case "missing":
                break;
        }

        if (scenario != "missing")
        {
            if (!malformed)
            {
                content = Sarif(
                    Results(Result("SEC100", "error", "src/App/One.cs", "not trusted")),
                    repository,
                    revision,
                    invocation,
                    includeInvocations);
            }

            Repository.AddUtf8File("evidence/input.sarif", content);
            artifact ??= new SarifEvidenceArtifactReference("evidence/input.sarif", "external.scan", producer);
            if (producer is not null)
            {
                artifact = new SarifEvidenceArtifactReference(artifact.Path, artifact.LogicalId, producer);
            }
        }

        SarifEvidenceReadResult read = new SarifEvidenceReader().Read(
            requirement,
            Repository.Root,
            artifact,
            new SarifEvidenceAssessmentContext("repo", "revision", "scope"));
        (_, IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project([requirement], [read]);
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult());

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(expectedStatus), read.Detail);
            Assert.That(read.SourceDiagnostics, Is.Empty);
            Assert.That(() => new SarifExternalDiagnosticSelector().Select(
                [new SarifExternalDiagnosticSelectionInput(read)]), Throws.ArgumentException);
            Assert.That(projection.Findings, Is.Empty);
            Assert.That(records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(records.Single().Reasons.Select(reason => reason.Code), Is.EqualTo([expectedReasonCode]));
            Assert.That(records.Single().Reasons.Single().Provenance, Is.EqualTo(records.Single().Provenance));
        });
    }

    [Test]
    public void EquivalentResults_DeduplicateIndependentlyOfArtifactAndResultOrderWithStableProvenance()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        string repeated = Result("SEC100", "error", "src/App/One.cs", "equivalent", fingerprint: "{\"stable\":\"same\"}");
        string fallback = Result("SEC100", "error", "src/App/Two.cs", "distinct location");
        string firstPath = "evidence/first.sarif";
        string secondPath = "evidence/second.sarif";
        Repository.AddUtf8File(firstPath, Sarif(Results(repeated, repeated, fallback), marker: "first"));
        Repository.AddUtf8File(secondPath, Sarif(Results(fallback, repeated), marker: "second"));
        SarifEvidenceReadResult first = Read(requirement, firstPath);
        SarifEvidenceReadResult second = Read(requirement, secondPath);
        SarifExternalDiagnosticSelectionResult forward = new SarifExternalDiagnosticSelector().Select(
            [new SarifExternalDiagnosticSelectionInput(first), new SarifExternalDiagnosticSelectionInput(second)]);
        SarifExternalDiagnosticSelectionResult reverse = new SarifExternalDiagnosticSelector().Select(
            [new SarifExternalDiagnosticSelectionInput(second), new SarifExternalDiagnosticSelectionInput(first)]);
        SarifSelectedExternalDiagnostic forwardRepeated = forward.Diagnostics.Single(diagnostic =>
            diagnostic.SourceDiagnostic.PrimaryLocation!.Path == "src/App/One.cs");
        SarifSelectedExternalDiagnostic reverseRepeated = reverse.Diagnostics.Single(diagnostic =>
            diagnostic.SourceDiagnostic.PrimaryLocation!.Path == "src/App/One.cs");
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(forward);
        IReadOnlyList<ArchitectureBaselineCandidate> baselines =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(forward);

        Assert.Multiple(() =>
        {
            Assert.That(first.ArtifactSha256, Is.Not.EqualTo(second.ArtifactSha256));
            Assert.That(forward.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity),
                Is.EqualTo(reverse.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity)));
            Assert.That(forwardRepeated.CanonicalIdentity, Is.EqualTo(reverseRepeated.CanonicalIdentity));
            Assert.That(forwardRepeated.Fingerprint.Origin, Is.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Source));
            Assert.That(forwardRepeated.EvidenceProvenances, Has.Count.EqualTo(2));
            Assert.That(forwardRepeated.EvidenceProvenances.Select(provenance => provenance.ArtifactPath),
                Is.EquivalentTo(new[] { firstPath, secondPath }));
            Assert.That(forwardRepeated.EvidenceProvenances.Select(provenance => provenance.ArtifactPath),
                Is.EqualTo(reverseRepeated.EvidenceProvenances.Select(provenance => provenance.ArtifactPath)));
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.PrimaryLocation!.Path),
                Is.EquivalentTo(["src/App/One.cs", "src/App/Two.cs"]));
            Assert.That(forward.Diagnostics.Single(diagnostic =>
                diagnostic.SourceDiagnostic.PrimaryLocation!.Path == "src/App/Two.cs").Fingerprint.Origin,
                Is.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Deterministic));
            Assert.That(baselines.Select(candidate => candidate.Identity),
                Is.EqualTo(projection.Findings.Select(finding => finding.Identity)));
        });
    }

    [Test]
    public void DistinctLogicalEvidenceContexts_RemainDistinctWhenSourceResultsMatch()
    {
        ArchitectureExternalEvidenceRequirement firstRequirement = Requirement("external.scan");
        ArchitectureExternalEvidenceRequirement secondRequirement = Requirement("external.other");
        string content = Sarif(Results(Result("SEC100", "error", "src/App/One.cs", "same", fingerprint: "{\"stable\":\"same\"}")));
        Repository.AddUtf8File("evidence/first.sarif", content);
        Repository.AddUtf8File("evidence/second.sarif", content);
        SarifEvidenceReadResult first = Read(firstRequirement, "evidence/first.sarif");
        SarifEvidenceReadResult second = Read(secondRequirement, "evidence/second.sarif");
        SarifExternalDiagnosticSelectionResult selection = new SarifExternalDiagnosticSelector().Select(
            [new SarifExternalDiagnosticSelectionInput(first), new SarifExternalDiagnosticSelectionInput(second)]);
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(selection);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(projection.Findings.Select(finding => ((ImportedExternalDiagnostic)finding.Details).LogicalEvidenceId),
                Is.EquivalentTo(["external.scan", "external.other"]));
            Assert.That(projection.Findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void WindowsStyleSourcePath_MatchesProjectAndNormalizedPathFilters()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        requirement.DiagnosticFilter!.Projects = ["App"];
        requirement.DiagnosticFilter.PathPrefixes = ["src/App"];
        Repository.AddUtf8File("evidence/windows-path.sarif", Sarif(
            Results(Result("SEC100", "error", "src\\\\App\\\\One.cs", "windows path"))));

        SarifEvidenceReadResult read = Read(requirement, "evidence/windows-path.sarif");
        SarifExternalDiagnosticSelectionResult selection = Select(read);

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(selection.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(selection.Diagnostics.Single().SourceDiagnostic.PrimaryLocation!.Path,
                Is.EqualTo("src/App/One.cs"));
        });
    }

    [Test]
    public void NativeAndImportedFindings_CoexistInDeterministicNormalAndTestingOutputs()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        Repository.AddUtf8File("evidence/input.sarif", Sarif(
            Results(Result("SEC100", "error", "src/App/One.cs", "imported", fingerprint: "{\"stable\":\"imported\"}"))));
        ImportedExternalDiagnosticProjection imported = ArchitectureImportedDiagnosticProjector.Project(
            Select(Read(requirement, "evidence/input.sarif")));
        ArchitectureViolation nativeViolation = new(
            "dependency", "native.contract", "App.Core", "Forbidden.Namespace", ["Forbidden.Namespace"]);
        ArchitectureFinding native = ArchitectureFindingMapper.FromViolation(nativeViolation, "strict");
        ArchitectureFinding importedFinding = imported.Findings.Single();
        var testing = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: [nativeViolation],
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = imported,
        });
        string sarif = ArchitectureSarifFormatter.FormatFindingsAsSarif(
            ArchitectureFindingMapper.Order([native, importedFinding]), "9.9.9");
        using JsonDocument output = JsonDocument.Parse(sarif);

        Assert.Multiple(() =>
        {
            Assert.That(native.CanonicalIdentity, Is.Not.EqualTo(importedFinding.CanonicalIdentity));
            Assert.That(testing.Findings, Has.Count.EqualTo(2));
            Assert.That(testing.Findings.Select(finding => finding.Kind),
                Is.EquivalentTo(["dependency", "imported_external_diagnostic"]));
            Assert.That(testing.Findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(output.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().Count(),
                Is.EqualTo(2));
            Assert.That(output.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray()
                .All(result => result.GetProperty("properties").TryGetProperty("arch_linter_net", out _)), Is.True);
        });
    }


}
