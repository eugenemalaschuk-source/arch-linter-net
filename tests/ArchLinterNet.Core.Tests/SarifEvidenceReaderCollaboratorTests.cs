using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderCollaboratorTests
{
    [Test]
    public void ArtifactReader_ReportsNormalizedPathHashAndAcquiredBytes()
    {
        using var repository = new SarifEvidenceTestRepository();
        byte[] bytes = "{}"u8.ToArray();
        repository.AddFile("reports/scan.sarif", bytes);

        SarifEvidenceArtifactReadOutcome outcome = new SarifEvidenceArtifactReader(ArchitectureFileSystem.Real).Read(
            repository.Root,
            "reports\\scan.sarif",
            4096,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsReadable, Is.True);
            Assert.That(outcome.RelativePath, Is.EqualTo("reports/scan.sarif"));
            Assert.That(outcome.Data, Is.EqualTo(bytes));
            Assert.That(outcome.Sha256, Is.EqualTo(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))));
        });
    }

    [Test]
    public void DocumentReader_SelectsMatchingRunAndCountsResults()
    {
        using JsonDocument document = JsonDocument.Parse(
            "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"scanner\"}},\"automationDetails\":{\"id\":\"run-1\"},\"results\":[{}]}]}");
        var requirement = new ArchitectureExternalEvidenceRequirement
        {
            Id = "scan",
            Format = "sarif",
            Tool = "scanner",
            Run = "run-1",
        };
        var reader = new SarifEvidenceDocumentReader();

        Assert.That(reader.TryGetRuns(document.RootElement, out JsonElement runs, out _, out _), Is.True);
        SarifRunSelection selection = reader.SelectMatchingRun(runs, requirement, new SarifEvidenceLimits(4096, 4, 10), CancellationToken.None);

        Assert.That(selection.Failure, Is.Null);
        Assert.That(reader.ReadResultCount(selection.Candidate!.Value.Run, new SarifEvidenceLimits(4096, 4, 10), out _, out _), Is.EqualTo(1));
    }

    [Test]
    public void ContextReader_MergesSarifAndProducerIdentityFacts()
    {
        using JsonDocument document = JsonDocument.Parse(
            "{\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"rev\"}]} ");
        var artifact = new SarifEvidenceArtifactReference(
            "scan.sarif",
            "scan",
            new SarifEvidenceProducerContext("repo", "rev", "scope"));

        ContextReadOutcome outcome = new SarifEvidenceContextReader().ReadContext(document.RootElement, artifact);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Failure, Is.Null, outcome.Detail);
            Assert.That(outcome.Context, Is.EqualTo(new SarifEvidenceResolvedContext("scan", "repo", "rev", "scope")));
        });
    }

    [Test]
    public void SourceProjectionReader_ProjectsDiagnosticAndLocationFacts()
    {
        using JsonDocument document = JsonDocument.Parse(
            "{\"tool\":{\"driver\":{\"rules\":[{\"id\":\"RULE\",\"properties\":{\"tags\":[\"security\"]}}]}},\"artifacts\":[{\"location\":{\"uri\":\"src\\\\App.cs\"}}],\"results\":[{\"ruleIndex\":0,\"message\":{\"text\":\"bad\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"index\":0},\"region\":{\"startLine\":4}}}]}]}");
        var reader = new SarifEvidenceSourceProjectionReader();

        Assert.That(reader.TryReadSourceDiagnostics(
            document.RootElement,
            out IReadOnlyList<SarifEvidenceSourceDiagnostic> diagnostics,
            out string? detail,
            CancellationToken.None), Is.True, detail);
        SarifEvidenceSourceDiagnostic diagnostic = diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.RuleId, Is.EqualTo("RULE"));
            Assert.That(diagnostic.DriverRuleTags, Is.EqualTo(new[] { "security" }));
            Assert.That(diagnostic.PrimaryLocation!.Path, Is.EqualTo("src/App.cs"));
            Assert.That(diagnostic.PrimaryLocation.Region!.StartLine, Is.EqualTo(4));
        });
    }
}
