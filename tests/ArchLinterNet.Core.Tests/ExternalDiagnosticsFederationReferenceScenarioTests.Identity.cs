using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ExternalDiagnosticsFederationReferenceScenarioTests
{
    [Test]
    public void DistinctScopeContexts_RemainDistinctWhenSourceResultsMatch()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        string content = Sarif(Results(Result(
            "SEC100", "error", "src/App/One.cs", "same", fingerprint: "{\"stable\":\"same\"}")));
        _repository.AddUtf8File("evidence/scope-a.sarif", content);
        _repository.AddUtf8File("evidence/scope-b.sarif", content);

        SarifEvidenceReadResult scopeA = Read(
            requirement,
            "evidence/scope-a.sarif",
            new SarifEvidenceProducerContext("external.scan", "repo", "revision", "scope-a"),
            "scope-a");
        SarifEvidenceReadResult scopeB = Read(
            requirement,
            "evidence/scope-b.sarif",
            new SarifEvidenceProducerContext("external.scan", "repo", "revision", "scope-b"),
            "scope-b");
        SarifExternalDiagnosticSelectionResult selection = new SarifExternalDiagnosticSelector().Select(
            [new SarifExternalDiagnosticSelectionInput(scopeA), new SarifExternalDiagnosticSelectionInput(scopeB)]);
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(selection);

        Assert.Multiple(() =>
        {
            Assert.That(scopeA.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(scopeB.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(scopeA.Context, Is.EqualTo(new SarifEvidenceResolvedContext(
                "external.scan", "repo", "revision", "scope-a")));
            Assert.That(scopeB.Context, Is.EqualTo(new SarifEvidenceResolvedContext(
                "external.scan", "repo", "revision", "scope-b")));
            Assert.That(scopeA.ArtifactSha256, Is.EqualTo(scopeB.ArtifactSha256));
            Assert.That(selection.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.RuleId),
                Is.All.EqualTo("SEC100"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().LogicalId),
                Is.All.EqualTo("external.scan"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.Fingerprint.Value),
                Is.All.EqualTo("same"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.PrimaryLocation!.Path),
                Is.All.EqualTo("src/App/One.cs"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().Context!.Scope),
                Is.EquivalentTo(["scope-a", "scope-b"]));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().Context!.Repository),
                Is.All.EqualTo("repo"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().Context!.Revision),
                Is.All.EqualTo("revision"));
            Assert.That(projection.Findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void SourceLocation_IndependentlyIsolatesCanonicalIdentity()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement("external.scan");
        string first = Result("SEC100", "error", "src/App/One.cs", "same", fingerprint: "{\"stable\":\"same\"}");
        string second = Result("SEC100", "error", "src/App/Two.cs", "same", fingerprint: "{\"stable\":\"same\"}");
        _repository.AddUtf8File("evidence/location.sarif", Sarif(Results(first, second)));

        SarifExternalDiagnosticSelectionResult selection = Select(Read(requirement, "evidence/location.sarif"));
        ImportedExternalDiagnosticProjection projection = ArchitectureImportedDiagnosticProjector.Project(selection);
        IReadOnlyList<ArchitectureBaselineCandidate> baselines =
            ArchitectureImportedDiagnosticBaselineProjector.ToBaselineCandidates(selection);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().LogicalId),
                Is.All.EqualTo("external.scan"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.EvidenceProvenances.Single().Context),
                Is.All.EqualTo(new SarifEvidenceResolvedContext("external.scan", "repo", "revision", "scope")));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.RuleId), Is.All.EqualTo("SEC100"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.SourceSeverity),
                Is.All.EqualTo(SarifEvidenceSourceSeverity.Error));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.Project), Is.All.EqualTo("App"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.Message), Is.All.EqualTo("same"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.Fingerprint.Value), Is.All.EqualTo("same"));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.PrimaryLocation!.Path),
                Is.EquivalentTo(["src/App/One.cs", "src/App/Two.cs"]));
            Assert.That(selection.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(projection.Findings.Select(finding => finding.CanonicalIdentity).Distinct().Count(), Is.EqualTo(2));
            Assert.That(baselines.Select(candidate => candidate.Identity).Distinct().Count(), Is.EqualTo(2));
        });
    }
}
