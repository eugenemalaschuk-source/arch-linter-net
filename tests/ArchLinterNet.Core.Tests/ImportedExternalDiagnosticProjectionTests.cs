using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ImportedExternalDiagnosticProjectionTests
{
    [Test]
    public void Project_ProjectsStrictAndAuditDiagnosticsWithSelectedIdentityAndProvenance()
    {
        SarifSelectedExternalDiagnostic strict = Selected(
            canonicalIdentity: "external-diagnostic:v2:strict",
            mode: SarifExternalDiagnosticGovernanceMode.Strict,
            artifactHash: "hash-one",
            runId: "run-one",
            message: "strict source message",
            path: "src/App/Strict.cs");
        SarifSelectedExternalDiagnostic audit = Selected(
            canonicalIdentity: "external-diagnostic:v2:audit",
            mode: SarifExternalDiagnosticGovernanceMode.Audit,
            artifactHash: "hash-two",
            runId: "run-two",
            message: "audit source message",
            path: "src/App/Audit.cs");

        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult([audit, strict]));
        IReadOnlyList<ArchitectureFinding> findings = projection.Findings;

        ArchitectureFinding strictFinding = findings.Single(finding => finding.Mode == "strict");
        ArchitectureFinding auditFinding = findings.Single(finding => finding.Mode == "audit");
        var strictDetail = (ImportedExternalDiagnostic)strictFinding.Details;
        Assert.Multiple(() =>
        {
            Assert.That(strictFinding.Kind, Is.EqualTo("imported_external_diagnostic"));
            Assert.That(strictFinding.Severity, Is.EqualTo("error"));
            Assert.That(auditFinding.Severity, Is.EqualTo("warning"));
            Assert.That(strictDetail.LogicalEvidenceId, Is.EqualTo("external.scan"));
            Assert.That(strictDetail.SourceDiagnostic.Message, Is.EqualTo("strict source message"));
            Assert.That(strictDetail.EvidenceProvenances.Single().ArtifactSha256, Is.EqualTo("hash-one"));
            Assert.That(strictFinding.SourceLocation, Is.EqualTo(new ArchitectureFindingSourceLocation("src/App/Strict.cs", 10, 4)));
            Assert.That(projection.HasBlockingFindings, Is.True);
        });
    }

    [Test]
    public void Project_ExcludesTransientRunAndArtifactProvenanceFromPersistentIdentity()
    {
        SarifSelectedExternalDiagnostic first = Selected(
            canonicalIdentity: "external-diagnostic:v2:stable",
            mode: SarifExternalDiagnosticGovernanceMode.Strict,
            artifactHash: "first-hash",
            runId: "first-run",
            message: "first display text",
            path: "src/App/Shared.cs");
        SarifSelectedExternalDiagnostic repeated = Selected(
            canonicalIdentity: "external-diagnostic:v2:stable",
            mode: SarifExternalDiagnosticGovernanceMode.Strict,
            artifactHash: "second-hash",
            runId: "second-run",
            message: "changed display text",
            path: "src/App/Shared.cs");

        ArchitectureFinding firstFinding = ArchitectureImportedDiagnosticProjector.ToFinding(first);
        ArchitectureFinding repeatedFinding = ArchitectureImportedDiagnosticProjector.ToFinding(repeated);
        IReadOnlyList<ArchitectureBaselineCandidate> candidates = ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(
            new SarifExternalDiagnosticSelectionResult([first]));

        Assert.Multiple(() =>
        {
            Assert.That(firstFinding.CanonicalIdentity, Is.EqualTo(repeatedFinding.CanonicalIdentity));
            Assert.That(((ImportedExternalDiagnostic)repeatedFinding.Details).EvidenceProvenances.Single().RunId,
                Is.EqualTo("second-run"));
            Assert.That(candidates.Single().Identity, Is.EqualTo(firstFinding.Identity));
            Assert.That(candidates.Single().ContractGroup, Is.EqualTo("strict"));
            Assert.That(candidates.Single().ForbiddenReference, Is.EqualTo("external-diagnostic:v2:stable"));
        });
    }

    [Test]
    public void Project_DistinguishesSelectedLocationIdentitiesAndRetainsOutputParity()
    {
        SarifSelectedExternalDiagnostic first = Selected(
            canonicalIdentity: "external-diagnostic:v2:location-one",
            mode: SarifExternalDiagnosticGovernanceMode.Strict,
            artifactHash: "hash-one",
            runId: "run-one",
            message: "source finding",
            path: "src/App/One.cs");
        SarifSelectedExternalDiagnostic second = Selected(
            canonicalIdentity: "external-diagnostic:v2:location-two",
            mode: SarifExternalDiagnosticGovernanceMode.Strict,
            artifactHash: "hash-two",
            runId: "run-two",
            message: "source finding",
            path: "src/App/Two.cs");
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult([first, second]));
        IReadOnlyList<ArchitectureFinding> findings = projection.Findings;

        Dictionary<string, object?> json = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(findings[0]);
        ArchitectureFindingReadEnvelope rehydrated = ArchitectureFindingJsonReader.Read(
            JsonSerializer.Serialize(json), strict: true);
        string human = ArchitectureDiagnosticFormatter.FormatFindingsForHumans(findings);
        string sarif = ArchitectureSarifFormatter.FormatFindingsAsSarif(findings, "1.2.3");
        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement normalized = result.GetProperty("properties").GetProperty("arch_linter_net");

        var testingResult = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = projection,
        });

        Assert.Multiple(() =>
        {
            Assert.That(findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(human, Does.Contain("sha256=hash-one"));
            Assert.That(human, Does.Contain($"canonical_identity={findings[0].CanonicalIdentity}"));
            Assert.That(json["logical_evidence_id"], Is.EqualTo("external.scan"));
            Assert.That(rehydrated.SchemaVersion, Is.EqualTo(ArchitectureFinding.CurrentSchemaVersion));
            Assert.That(rehydrated.Kind, Is.EqualTo("imported_external_diagnostic"));
            Assert.That(result.GetProperty("locations")[0].GetProperty("physicalLocation")
                .GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("src/App/One.cs"));
            Assert.That(normalized.GetProperty("evidence_provenance")[0]
                .GetProperty("artifact_sha256").GetString(), Is.EqualTo("hash-one"));
            Assert.That(testingResult.Findings.Count(finding => finding.Kind == "imported_external_diagnostic"), Is.EqualTo(2));
            Assert.That(testingResult.Passed, Is.False);
            Assert.That(() => testingResult.ShouldPass(), Throws.InvalidOperationException
                .With.Message.Contains("Imported external diagnostics:")
                .And.Message.Contains("imported_external_diagnostic"));
        });
    }

    [Test]
    public void WithImportedDiagnostics_DerivesOutcomeAndAdapterPassStateFromGovernanceMode()
    {
        ImportedExternalDiagnosticProjection strict = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult(
            [
                Selected(
                    "external-diagnostic:v2:strict",
                    SarifExternalDiagnosticGovernanceMode.Strict,
                    "strict-hash",
                    "strict-run",
                    "strict source message",
                    "src/App/Strict.cs"),
            ]));
        ImportedExternalDiagnosticProjection audit = ArchitectureImportedDiagnosticProjector.Project(
            new SarifExternalDiagnosticSelectionResult(
            [
                Selected(
                    "external-diagnostic:v2:audit",
                    SarifExternalDiagnosticGovernanceMode.Audit,
                    "audit-hash",
                    "audit-run",
                    "audit source message",
                    "src/App/Audit.cs"),
            ]));

        ValidationOutcome strictOutcome = PassingOutcome().WithImportedDiagnostics(strict);
        ValidationOutcome auditOutcome = PassingOutcome().WithImportedDiagnostics(audit);
        var strictResult = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = strict,
        });
        var auditResult = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = audit,
        });

        Assert.Multiple(() =>
        {
            Assert.That(strictOutcome.Passed, Is.False);
            Assert.That(auditOutcome.Passed, Is.True);
            Assert.That(strictResult.Passed, Is.False);
            Assert.That(auditResult.Passed, Is.True);
        });
    }

    [Test]
    public void HumanProjection_EscapesProducerControlledTextAndRetainsTheCanonicalIdentity()
    {
        var source = new SarifEvidenceSourceDiagnostic(
            "source message\r\n::error title=forged\u001b",
            "SEC\r100",
            SarifEvidenceSourceSeverity.Error,
            new SarifEvidenceSourceLocation("src/Unsafe\n.cs", new SarifEvidenceSourceRegion(startLine: 10)),
            project: "Project\tName");
        var provenance = new SarifEvidenceProvenance(
            "external\rscan",
            "artifacts/analysis\n.sarif",
            "hash\tvalue",
            "Example\rAnalyzer",
            "1.2.3\n",
            "run\u001b1",
            1,
            new SarifEvidenceResolvedContext("external.scan", "repo\rname", "revision\nname", "scope\tname"));
        ArchitectureFinding finding = ArchitectureImportedDiagnosticProjector.ToFinding(new SarifSelectedExternalDiagnostic(
            "external-diagnostic:v2:unsafe-human-output",
            source,
            SarifExternalDiagnosticGovernanceMode.Strict,
            new SarifExternalDiagnosticFingerprint(
                SarifExternalDiagnosticFingerprintOrigin.Source,
                "fingerprint\rvalue",
                "primary\nname"),
            [provenance]));

        string human = ArchitectureDiagnosticFormatter.FormatFindingsForHumans([finding]);

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Not.Contain("\r"));
            Assert.That(human, Does.Not.Contain("\n"));
            Assert.That(human, Does.Contain("source message\\r\\n::error title=forged\\u001B"));
            Assert.That(human, Does.Contain("tool=Example\\rAnalyzer"));
            Assert.That(human, Does.Contain("canonical_identity=" + finding.CanonicalIdentity));
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

    private static SarifSelectedExternalDiagnostic Selected(
        string canonicalIdentity,
        SarifExternalDiagnosticGovernanceMode mode,
        string artifactHash,
        string runId,
        string message,
        string path)
    {
        var source = new SarifEvidenceSourceDiagnostic(
            message,
            "SEC100",
            mode == SarifExternalDiagnosticGovernanceMode.Strict
                ? SarifEvidenceSourceSeverity.Error
                : SarifEvidenceSourceSeverity.Warning,
            new SarifEvidenceSourceLocation(path, new SarifEvidenceSourceRegion(startLine: 10, startColumn: 4)),
            project: "App",
            driverRuleTags: ["security"]);
        var provenance = new SarifEvidenceProvenance(
            "external.scan",
            "artifacts/analysis.sarif",
            artifactHash,
            "Example Analyzer",
            "1.2.3",
            runId,
            1,
            new SarifEvidenceResolvedContext("external.scan", "repo", "revision", "scope"));
        return new SarifSelectedExternalDiagnostic(
            canonicalIdentity,
            source,
            mode,
            new SarifExternalDiagnosticFingerprint(SarifExternalDiagnosticFingerprintOrigin.Source, "source-fingerprint", "primary"),
            [provenance]);
    }
}
