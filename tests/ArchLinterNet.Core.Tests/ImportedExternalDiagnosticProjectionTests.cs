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
            Assert.That(candidates.Single().ContractGroup, Is.EqualTo("strict_external"));
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
        ValidationOutcome strictThenAuditOutcome = strictOutcome.WithImportedDiagnostics(audit);
        ValidationOutcome strictThenEmptyOutcome = strictOutcome.WithImportedDiagnostics(ImportedExternalDiagnosticProjection.Empty);
        ValidationOutcome nativeFailureWithAuditOutcome = Outcome(passed: false).WithImportedDiagnostics(audit);
        ValidationOutcome publiclyFailedOutcome = PassingOutcome() with { Passed = false };
        ValidationOutcome publiclyFailedThenEmptyOutcome = publiclyFailedOutcome.WithImportedDiagnostics(
            ImportedExternalDiagnosticProjection.Empty);
        var (deconstructedPassed, _, _, _, _, _, _, _, _, _, _, _) = strictOutcome;
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
            Assert.That(deconstructedPassed, Is.False);
            Assert.That(auditOutcome.Passed, Is.True);
            Assert.That(strictThenAuditOutcome.Passed, Is.True);
            Assert.That(strictThenAuditOutcome.ImportedDiagnosticFindings, Is.EqualTo(audit.Findings));
            Assert.That(strictThenEmptyOutcome.Passed, Is.True);
            Assert.That(strictThenEmptyOutcome.ImportedDiagnosticFindings, Is.Empty);
            Assert.That(nativeFailureWithAuditOutcome.Passed, Is.False);
            Assert.That(publiclyFailedOutcome.NativePassed, Is.False);
            Assert.That(publiclyFailedThenEmptyOutcome.Passed, Is.False);
            Assert.That(strictResult.Passed, Is.False);
            Assert.That(auditResult.Passed, Is.True);
        });
    }

    [Test]
    public void ImportedBaseline_GenerateSerializeLoadAndCompareKeepsExactFindingKnownWithoutNativeIgnore()
    {
        SarifSelectedExternalDiagnostic selected = Selected(
            "external-diagnostic:v2:baseline-round-trip",
            SarifExternalDiagnosticGovernanceMode.Strict,
            "baseline-hash",
            "baseline-run",
            "baseline source message",
            "src/App/Baseline.cs");
        ArchitectureContractDocument policy = new()
        {
            ExternalEvidence =
            [
                new ArchitectureExternalEvidenceRequirement
                {
                    Id = "external.scan",
                    Format = "sarif",
                    Required = true,
                    Tool = "Example Analyzer",
                    Run = "baseline-run",
                },
            ],
        };
        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(
                new SarifExternalDiagnosticSelectionResult([selected]));
        var generator = new ArchitectureBaselineGenerator();
        ArchitectureBaselineDocument generated = generator.Generate(policy, candidates);
        string path = Path.Combine(Path.GetTempPath(), "arch-linter-net-imported-baseline-" + Guid.NewGuid() + ".yml");

        try
        {
            File.WriteAllText(path, generator.Serialize(generated));
            ArchitectureBaselineDocument loaded = new ArchitectureBaselineLoadingService().LoadFromPath(path);
            ArchitectureBaselineLoadingService.MergeAndValidate(policy, loaded);
            ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
                policy,
                loaded,
                candidates,
                "strict");

            Assert.Multiple(() =>
            {
                Assert.That(candidates.Single().ContractGroup, Is.EqualTo("strict_external"));
                Assert.That(loaded.Baseline.StrictExternal.Single().Id, Is.EqualTo("external.scan"));
                Assert.That(policy.Contracts.StrictExternal, Is.Empty);
                Assert.That(comparison.New, Is.Empty);
                Assert.That(comparison.Frozen, Has.Count.EqualTo(1));
                Assert.That(comparison.Frozen.Single().Identity, Is.EqualTo(candidates.Single().Identity));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ImportedBaseline_CaseDistinctLogicalEvidenceIdsRoundTripIndependently()
    {
        SarifSelectedExternalDiagnostic lowerCase = Selected(
            "external-diagnostic:v2:case-lower",
            SarifExternalDiagnosticGovernanceMode.Strict,
            "case-lower-hash",
            "case-run",
            "lower-case source message",
            "src/App/Lower.cs",
            logicalEvidenceId: "scan");
        SarifSelectedExternalDiagnostic upperCase = Selected(
            "external-diagnostic:v2:case-upper",
            SarifExternalDiagnosticGovernanceMode.Strict,
            "case-upper-hash",
            "case-run",
            "upper-case source message",
            "src/App/Upper.cs",
            logicalEvidenceId: "Scan");
        ArchitectureContractDocument policy = new()
        {
            ExternalEvidence =
            [
                ExternalEvidenceRequirement("scan"),
                ExternalEvidenceRequirement("Scan"),
            ],
        };
        IReadOnlyList<ArchitectureBaselineCandidate> candidates =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(
                new SarifExternalDiagnosticSelectionResult([lowerCase, upperCase]));
        var generator = new ArchitectureBaselineGenerator();
        ArchitectureBaselineDocument generated = generator.Generate(policy, candidates);
        string path = Path.Combine(Path.GetTempPath(), "arch-linter-net-imported-baseline-casing-" + Guid.NewGuid() + ".yml");

        try
        {
            File.WriteAllText(path, generator.Serialize(generated));
            ArchitectureBaselineDocument loaded = new ArchitectureBaselineLoadingService().LoadFromPath(path);
            ArchitectureBaselineLoadingService.MergeAndValidate(policy, loaded);
            ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
                policy,
                loaded,
                candidates,
                "strict");

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Baseline.StrictExternal.Select(entry => entry.Id),
                    Is.EquivalentTo(["scan", "Scan"]));
                Assert.That(comparison.New, Is.Empty);
                Assert.That(comparison.Frozen, Has.Count.EqualTo(2));
                Assert.That(comparison.Frozen.Select(entry => entry.ContractId),
                    Is.EquivalentTo(["scan", "Scan"]));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void BaselineProjection_PrecomputesOneIdentitySortKeyPerHighCardinalityCandidate()
    {
        const int CandidateCount = 2_048;
        ArchitectureBaselineCandidate[] candidates = Enumerable.Range(0, CandidateCount)
            .Select(index => new ArchitectureBaselineCandidate(
                "strict_external",
                "external.scan",
                $"source-{CandidateCount - index:D4}",
                $"external-diagnostic:v2:{index:D4}",
                new ArchitectureViolationIdentity(
                    ArchitectureViolationIdentity.CurrentVersion,
                    "external_diagnostic",
                    "external_diagnostic",
                    "external.scan",
                    SourceAssembly: null,
                    SourceType: $"source-{CandidateCount - index:D4}",
                    SourceMember: null,
                    TargetAssembly: null,
                    TargetType: null,
                    TargetMember: $"external-diagnostic:v2:{index:D4}",
                    Occurrence: index)))
            .ToArray();
        int serializationCount = 0;

        IReadOnlyList<ArchitectureBaselineCandidate> ordered =
            ArchitectureImportedDiagnosticBaselineProjector.SortCandidates(
                candidates,
                identity =>
                {
                    serializationCount++;
                    return ArchitectureViolationIdentityJson.Serialize(identity);
                });

        Assert.Multiple(() =>
        {
            Assert.That(serializationCount, Is.EqualTo(CandidateCount));
            Assert.That(ordered, Has.Count.EqualTo(CandidateCount));
            Assert.That(ordered[0].SourceType, Is.EqualTo("source-0001"));
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

    private static ValidationOutcome PassingOutcome() => Outcome(passed: true);

    private static ValidationOutcome Outcome(bool passed) => new(
        Passed: passed,
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
        string path,
        string logicalEvidenceId = "external.scan")
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
            logicalEvidenceId,
            "artifacts/analysis.sarif",
            artifactHash,
            "Example Analyzer",
            "1.2.3",
            runId,
            1,
            new SarifEvidenceResolvedContext(logicalEvidenceId, "repo", "revision", "scope"));
        return new SarifSelectedExternalDiagnostic(
            canonicalIdentity,
            source,
            mode,
            new SarifExternalDiagnosticFingerprint(SarifExternalDiagnosticFingerprintOrigin.Source, "source-fingerprint", "primary"),
            [provenance]);
    }

    private static ArchitectureExternalEvidenceRequirement ExternalEvidenceRequirement(string id) => new()
    {
        Id = id,
        Format = "sarif",
        Required = true,
        Tool = "Example Analyzer",
        Run = "case-run",
    };
}
