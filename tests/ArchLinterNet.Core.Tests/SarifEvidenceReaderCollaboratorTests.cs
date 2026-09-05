using System.IO;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderCollaboratorTests
{
    private static readonly string[] _securityTags = ["security"];
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
    public void ArtifactReader_MissingArtifact_ReturnsMissingFailure()
    {
        using var repository = new SarifEvidenceTestRepository();

        SarifEvidenceArtifactReadOutcome outcome = new SarifEvidenceArtifactReader(ArchitectureFileSystem.Real).Read(
            repository.Root,
            "missing.sarif",
            4096,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsReadable, Is.False);
            Assert.That(outcome.Failure, Is.EqualTo(ArtifactReadFailure.Missing));
            Assert.That(outcome.ExceededLimit, Is.False);
            Assert.That(outcome.BytesRead, Is.EqualTo(0));
            Assert.That(outcome.Data, Is.Empty);
            Assert.That(outcome.RelativePath, Is.EqualTo("missing.sarif"));
            Assert.That(outcome.Sha256, Is.Null);
        });
    }

    [Test]
    public void ArtifactReader_UnsafeArtifactPath_ReturnsUnsafeFailure()
    {
        using var repository = new SarifEvidenceTestRepository();

        SarifEvidenceArtifactReadOutcome outcome = new SarifEvidenceArtifactReader(ArchitectureFileSystem.Real).Read(
            repository.Root,
            "../scan.sarif",
            4096,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsReadable, Is.False);
            Assert.That(outcome.Failure, Is.EqualTo(ArtifactReadFailure.Unsafe));
            Assert.That(outcome.RelativePath, Is.Null);
            Assert.That(outcome.Data, Is.Empty);
            Assert.That(outcome.BytesRead, Is.EqualTo(0));
            Assert.That(outcome.Sha256, Is.Null);
            Assert.That(outcome.ExceededLimit, Is.False);
        });
    }

    [Test]
    public void ArtifactReader_UnreadableArtifact_ReturnsUnreadableFailure()
    {
        using var repository = new SarifEvidenceTestRepository();
        var fileSystem = new ThrowingEvidenceFileSystem();

        SarifEvidenceArtifactReadOutcome outcome = new SarifEvidenceArtifactReader(fileSystem).Read(
            repository.Root,
            "scan.sarif",
            4096,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsReadable, Is.False);
            Assert.That(outcome.Failure, Is.EqualTo(ArtifactReadFailure.Unreadable));
            Assert.That(outcome.BytesRead, Is.EqualTo(0));
            Assert.That(outcome.Data, Is.Empty);
            Assert.That(outcome.RelativePath, Is.EqualTo("scan.sarif"));
            Assert.That(outcome.Sha256, Is.Null);
            Assert.That(outcome.ExceededLimit, Is.False);
        });
    }

    [Test]
    public void ArtifactReader_OverLimit_IsReadableWithExceededFlag()
    {
        using var repository = new SarifEvidenceTestRepository();
        byte[] bytes = [0x00, 0x01, 0x02, 0x03, 0x04];
        repository.AddFile("scan.sarif", bytes);

        SarifEvidenceArtifactReadOutcome outcome = new SarifEvidenceArtifactReader(ArchitectureFileSystem.Real).Read(
            repository.Root,
            "scan.sarif",
            4,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsReadable, Is.True);
            Assert.That(outcome.Failure, Is.EqualTo(ArtifactReadFailure.None));
            Assert.That(outcome.ExceededLimit, Is.True);
            Assert.That(outcome.BytesRead, Is.EqualTo(5));
            Assert.That(outcome.Data, Is.EqualTo(bytes));
            Assert.That(outcome.RelativePath, Is.EqualTo("scan.sarif"));
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

        Assert.That(SarifEvidenceDocumentReader.TryGetRuns(document.RootElement, out JsonElement runs, out _, out _), Is.True);
        SarifRunSelection selection = SarifEvidenceDocumentReader.SelectMatchingRun(runs, requirement, new SarifEvidenceLimits(4096, 4, 10), CancellationToken.None);

        Assert.That(selection.Failure, Is.Null);
        Assert.That(SarifEvidenceDocumentReader.ReadResultCount(selection.Candidate!.Value.Run, new SarifEvidenceLimits(4096, 4, 10), out _, out _), Is.EqualTo(1));
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

        ContextReadOutcome outcome = SarifEvidenceContextReader.ReadContext(document.RootElement, artifact);

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
            Assert.That(diagnostic.DriverRuleTags, Is.EqualTo(_securityTags));
            Assert.That(diagnostic.PrimaryLocation!.Path, Is.EqualTo("src/App.cs"));
            Assert.That(diagnostic.PrimaryLocation.Region!.StartLine, Is.EqualTo(4));
        });
    }

    private sealed class ThrowingEvidenceFileSystem : IArchitectureEvidenceFileSystem
    {
        public Stream OpenRepositoryLocalRegularFile(string repositoryRoot, string repositoryRelativePath)
        {
            throw new IOException("The evidence source was unavailable.");
        }
    }
}
