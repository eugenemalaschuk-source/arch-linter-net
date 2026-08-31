using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ExternalDiagnosticsFederationReferenceScenarioTests
{
    [Test]
    public void PolicyAuthorizedRuleIds_ExcludeUnlistedDiagnosticsFromEveryProjection()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan", ruleIds: ["SEC100"]);
        _repository.AddUtf8File("evidence/filtered.sarif", Sarif(Results(
            Result("SEC100", "error", "src/App/One.cs", "allowed", fingerprint: "{\"stable\":\"allowed\"}"),
            Result("PUBLICAPI001", "error", "src/App/Two.cs", "excluded", fingerprint: "{\"stable\":\"excluded\"}"))));

        SarifEvidenceReadResult read = Read(requirement, "evidence/filtered.sarif");
        SarifExternalDiagnosticSelectionResult selection = Select(read);
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(selection);
        IReadOnlyList<ArchitectureBaselineCandidate> baselines =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(selection);
        ArchitectureFinding finding = projection.Findings.Single();
        string human = ArchitectureDiagnosticFormatter.FormatFindingsForHumans(projection.Findings);
        string json = JsonSerializer.Serialize(ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding));
        string sarif = ArchitectureSarifFormatter.FormatFindingsAsSarif(projection.Findings, "9.9.9");
        var testing = new ArchitectureValidationResult(new ArchitectureValidationResultParams(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>())
        {
            ImportedDiagnostics = projection,
        });

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(read.SourceDiagnostics.Select(diagnostic => diagnostic.RuleId),
                Is.EquivalentTo(["SEC100", "PUBLICAPI001"]));
            Assert.That(selection.FilterMismatches, Is.Empty);
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.RuleId),
                Is.EqualTo(["SEC100"]));
            Assert.That(projection.Findings.Select(projected =>
                    ((ImportedExternalDiagnostic)projected.Details).SourceDiagnostic.RuleId),
                Is.EqualTo(["SEC100"]));
            Assert.That(baselines.Select(candidate => candidate.Identity), Is.EqualTo(projection.Findings.Select(projected => projected.Identity)));
            Assert.That(human, Does.Contain("SEC100").And.Not.Contain("PUBLICAPI001"));
            Assert.That(json, Does.Contain("SEC100").And.Not.Contain("PUBLICAPI001"));
            Assert.That(sarif, Does.Contain("SEC100").And.Not.Contain("PUBLICAPI001"));
            Assert.That(testing.ImportedDiagnosticFindings.Select(projected =>
                    ((ImportedExternalDiagnostic)projected.Details).SourceDiagnostic.RuleId),
                Is.EqualTo(["SEC100"]));
            Assert.That(testing.Findings.Select(projected =>
                    ((ImportedExternalDiagnostic)projected.Details).SourceDiagnostic.RuleId),
                Is.EqualTo(["SEC100"]));
            Assert.That(testing.Passed, Is.False);
        });
    }
}
