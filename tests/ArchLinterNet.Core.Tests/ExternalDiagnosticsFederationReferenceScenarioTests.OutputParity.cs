using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ExternalDiagnosticsFederationReferenceScenarioTests
{
    private static void AssertOutputParity(
        ImportedExternalDiagnostic detail,
        ArchitectureFinding finding,
        Dictionary<string, object?> json,
        string human,
        JsonElement sarifResult,
        ArchitectureValidationResult testing,
        string expectedHash)
    {
        var jsonProvenance = (Dictionary<string, object?>)((object[])json["evidence_provenance"]!)[0];
        var jsonSource = (Dictionary<string, object?>)json["source_diagnostic"]!;
        var jsonLocation = (Dictionary<string, object?>)jsonSource["location"]!;
        var jsonFingerprint = (Dictionary<string, object?>)jsonSource["fingerprint"]!;
        JsonElement normalized = sarifResult.GetProperty("properties").GetProperty("arch_linter_net");
        JsonElement sarifProvenance = normalized.GetProperty("evidence_provenance")[0];
        var testingDetail = (ImportedExternalDiagnostic)testing.ImportedDiagnosticFindings
            .Single(importedFinding => importedFinding.CanonicalIdentity == finding.CanonicalIdentity).Details;
        SarifEvidenceProvenance testingProvenance = testingDetail.EvidenceProvenances.Single();

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("[external.scan] [imported_external_diagnostic] Synthetic.Scanner/SEC100"));
            Assert.That(human, Does.Contain("at src/App/One.cs:7:3"));
            Assert.That(human, Does.Contain("fingerprint=source:stable:source-42"));
            Assert.That(human, Does.Contain("logical=external.scan, tool=Synthetic.Scanner, version=1.0, run=assessment-42"));
            Assert.That(human, Does.Contain("repository=repo, revision=revision, scope=scope"));
            Assert.That(human, Does.Contain("artifact=evidence/current.sarif"));
            Assert.That(human, Does.Contain("sha256=" + expectedHash));
            Assert.That(human, Does.Contain("canonical_identity=" + finding.CanonicalIdentity));
            Assert.That(json["logical_evidence_id"], Is.EqualTo("external.scan"));
            Assert.That(json["governance_mode"], Is.EqualTo("strict"));
            Assert.That(jsonSource["tool"], Is.EqualTo("Synthetic.Scanner"));
            Assert.That(jsonSource["rule_id"], Is.EqualTo("SEC100"));
            Assert.That(jsonLocation["path"], Is.EqualTo("src/App/One.cs"));
            Assert.That(jsonLocation["start_line"], Is.EqualTo(7));
            Assert.That(jsonLocation["start_column"], Is.EqualTo(3));
            Assert.That(jsonFingerprint["origin"], Is.EqualTo("source"));
            Assert.That(jsonFingerprint["value"], Is.EqualTo("source-42"));
            Assert.That(jsonProvenance, Is.EqualTo(new Dictionary<string, object?>
            {
                ["logical_evidence_id"] = "external.scan",
                ["tool"] = "Synthetic.Scanner",
                ["tool_version"] = "1.0",
                ["run_id"] = "assessment-42",
                ["artifact_path"] = "evidence/current.sarif",
                ["artifact_sha256"] = expectedHash,
                ["result_count"] = 2,
                ["repository"] = "repo",
                ["revision"] = "revision",
                ["scope"] = "scope",
            }));
            Assert.That(normalized.GetProperty("logical_evidence_id").GetString(), Is.EqualTo("external.scan"));
            Assert.That(normalized.GetProperty("source_diagnostic").GetProperty("tool").GetString(),
                Is.EqualTo("Synthetic.Scanner"));
            Assert.That(normalized.GetProperty("source_diagnostic").GetProperty("rule_id").GetString(), Is.EqualTo("SEC100"));
            Assert.That(normalized.GetProperty("source_diagnostic").GetProperty("fingerprint").GetProperty("value").GetString(),
                Is.EqualTo("source-42"));
            Assert.That(sarifProvenance.GetProperty("logical_evidence_id").GetString(), Is.EqualTo("external.scan"));
            Assert.That(sarifProvenance.GetProperty("tool").GetString(), Is.EqualTo("Synthetic.Scanner"));
            Assert.That(sarifProvenance.GetProperty("tool_version").GetString(), Is.EqualTo("1.0"));
            Assert.That(sarifProvenance.GetProperty("run_id").GetString(), Is.EqualTo("assessment-42"));
            Assert.That(sarifProvenance.GetProperty("artifact_path").GetString(), Is.EqualTo("evidence/current.sarif"));
            Assert.That(sarifProvenance.GetProperty("repository").GetString(), Is.EqualTo("repo"));
            Assert.That(sarifProvenance.GetProperty("revision").GetString(), Is.EqualTo("revision"));
            Assert.That(sarifProvenance.GetProperty("scope").GetString(), Is.EqualTo("scope"));
            Assert.That(sarifProvenance.GetProperty("artifact_sha256").GetString(), Is.EqualTo(expectedHash));
            Assert.That(sarifResult.GetProperty("locations")[0].GetProperty("physicalLocation")
                .GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("src/App/One.cs"));
            Assert.That(testing.ImportedDiagnosticFindings, Has.Count.EqualTo(2));
            Assert.That(testing.Findings, Has.Count.EqualTo(2));
            Assert.That(testing.Passed, Is.False);
            Assert.That(testingDetail.LogicalEvidenceId, Is.EqualTo("external.scan"));
            Assert.That(testingDetail.SourceDiagnostic.RuleId, Is.EqualTo("SEC100"));
            Assert.That(testingDetail.SourceDiagnostic.PrimaryLocation!.Path, Is.EqualTo("src/App/One.cs"));
            Assert.That(testingDetail.Fingerprint.Value, Is.EqualTo("source-42"));
            Assert.That(testingProvenance.LogicalId, Is.EqualTo("external.scan"));
            Assert.That(testingProvenance.ToolName, Is.EqualTo("Synthetic.Scanner"));
            Assert.That(testingProvenance.ToolVersion, Is.EqualTo("1.0"));
            Assert.That(testingProvenance.RunId, Is.EqualTo("assessment-42"));
            Assert.That(testingProvenance.ArtifactPath, Is.EqualTo("evidence/current.sarif"));
            Assert.That(testingProvenance.ArtifactSha256, Is.EqualTo(expectedHash));
            Assert.That(testingProvenance.Context!.Repository, Is.EqualTo("repo"));
            Assert.That(testingProvenance.Context!.Revision, Is.EqualTo("revision"));
            Assert.That(testingProvenance.Context!.Scope, Is.EqualTo("scope"));
            Assert.That(testingProvenance, Is.EqualTo(detail.EvidenceProvenances.Single()));
        });
    }
}
