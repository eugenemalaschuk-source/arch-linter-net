using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderTrustBindingTests
{
    [TestCase("repository", SarifEvidenceTrustStatus.WrongRepository)]
    [TestCase("revision", SarifEvidenceTrustStatus.WrongRevision)]
    [TestCase("scope", SarifEvidenceTrustStatus.WrongScope)]
    public void Read_WrongRequiredBinding_IsRejectedWithDimensionSpecificStatus(
        string dimension,
        SarifEvidenceTrustStatus expectedStatus)
    {
        string json = BuildSarif();
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);
        var requirement = Requirement();
        var producer = new SarifEvidenceProducerContext(
            logicalId: "external.scan",
            repository: "repository",
            revision: "revision",
            scope: "scope");
        var expected = new SarifEvidenceAssessmentContext(
            repository: dimension == "repository" ? "other" : "repository",
            revision: dimension == "revision" ? "other" : "revision",
            scope: dimension == "scope" ? "other" : "scope");

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            requirement,
            "/repo",
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan", producer),
            expected,
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [TestCase("repository", SarifEvidenceTrustStatus.MissingRepository)]
    [TestCase("revision", SarifEvidenceTrustStatus.MissingRevision)]
    [TestCase("scope", SarifEvidenceTrustStatus.MissingScope)]
    public void Read_MissingRequiredBinding_IsRejectedWithoutInference(
        string dimension,
        SarifEvidenceTrustStatus expectedStatus)
    {
        string json = BuildSarif(
            repository: dimension == "repository" ? null : "repository",
            revision: dimension == "revision" ? null : "revision");
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);
        var expected = new SarifEvidenceAssessmentContext(
            repository: "repository",
            revision: "revision",
            scope: dimension == "scope" ? "scope" : null);

        // Scope never comes from standard SARIF; it must be supplied explicitly by the producer.
        SarifEvidenceProducerContext? producer = dimension == "scope"
            ? new SarifEvidenceProducerContext("external.scan", "repository", "revision", null)
            : new SarifEvidenceProducerContext("external.scan", null, null, null);
        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan", producer),
            expected,
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_WrongLogicalId_IsRejectedEvenWhenArtifactOtherwiseMatches()
    {
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", BuildSarif(), DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference("scan.sarif", "other.scan"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.WrongLogicalId));
            Assert.That(result.ReasonCode, Is.EqualTo("wrong_external_evidence_identity"));
        });
    }

    [Test]
    public void Read_ConflictingSarifAndProducerContext_FailsClosed()
    {
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", BuildSarif(), DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference(
                "scan.sarif",
                "external.scan",
                new SarifEvidenceProducerContext("external.scan", "other-repository", "revision", null)),
            new SarifEvidenceAssessmentContext("repository", "revision"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.ConflictingContext));
    }

    [TestCase("scan.sarif", SarifEvidenceTrustStatus.MissingRequiredInput)]
    [TestCase("../scan.sarif", SarifEvidenceTrustStatus.UnsafePath)]
    [TestCase("/tmp/scan.sarif", SarifEvidenceTrustStatus.UnsafePath)]
    [TestCase("C:\\tmp\\scan.sarif", SarifEvidenceTrustStatus.UnsafePath)]
    public void Read_MissingOrUnsafeArtifact_IsRejectedBeforeRead(
        string path,
        SarifEvidenceTrustStatus expectedStatus)
    {
        var fileSystem = new FakeArchitectureFileSystem();
        if (path == "scan.sarif")
        {
            // Intentionally do not add the file.
        }

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference(path, "external.scan"));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_ArtifactRunsAndResultsRespectIndependentBounds()
    {
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile(
            "/repo/too-large.sarif",
            BuildSarif(results: "[{\"ruleId\":\"A\"},{\"ruleId\":\"B\"}]"),
            DateTime.UtcNow);
        fileSystem.AddFile(
            "/repo/too-many-runs.sarif",
            BuildSarifWithTwoRuns(),
            DateTime.UtcNow);

        var reader = new SarifEvidenceReader(fileSystem);
        SarifEvidenceReadResult results = reader.Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("too-large.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"), new SarifEvidenceLimits(4096, 4, 1));
        SarifEvidenceReadResult runs = reader.Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("too-many-runs.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"), new SarifEvidenceLimits(4096, 1, 10));

        Assert.Multiple(() =>
        {
            Assert.That(results.Status, Is.EqualTo(SarifEvidenceTrustStatus.TooManyResults));
            Assert.That(runs.Status, Is.EqualTo(SarifEvidenceTrustStatus.TooManyRuns));
            Assert.That(results.ArtifactSha256, Is.Not.Null);
            Assert.That(runs.ArtifactSha256, Is.Not.Null);
        });
    }

    [Test]
    public void Read_ArtifactByteLimitHashesConsumedBytesAndDoesNotParsePartialJson()
    {
        string json = BuildSarif();
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"),
            new SarifEvidenceLimits(10, 4, 10));

        string consumed = json[..11];
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.ArtifactTooLarge));
            Assert.That(result.ArtifactSha256, Is.EqualTo(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(consumed)))));
            Assert.That(result.ResultCount, Is.Null);
        });
    }

    private static ArchitectureExternalEvidenceRequirement Requirement() => new()
    {
        Id = "external.scan",
        Format = "sarif",
        Required = true,
        Tool = "Acme.Scanner",
        ToolVersion = "7.2",
        Run = "assessment-42",
        RequireRepository = true,
        RequireRevision = true,
        RequireScope = true,
    };

    private static string BuildSarif(string? repository = "repository", string? revision = "revision", string results = "[]")
    {
        string repositoryProperty = repository is null ? string.Empty : $"\"repositoryUri\":\"{repository}\"";
        string revisionProperty = revision is null ? string.Empty : $"\"revisionId\":\"{revision}\"";
        string separator = repository is not null && revision is not null ? "," : string.Empty;
        string provenance = repository is null && revision is null
            ? "[]"
            : $"[{{{repositoryProperty}{separator}{revisionProperty}}}]";
        return $"{{\"version\":\"2.1.0\",\"runs\":[{{\"tool\":{{\"driver\":{{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"}}}},\"automationDetails\":{{\"id\":\"assessment-42\"}},\"invocations\":[{{\"executionSuccessful\":true}}],\"versionControlProvenance\":{provenance},\"results\":{results}}}]}}";
    }

    private static string BuildSarifWithTwoRuns() =>
        "{\"version\":\"2.1.0\",\"runs\":["
        + BuildSarif()["{\"version\":\"2.1.0\",\"runs\":[".Length..^2]
        + ","
        + BuildSarif()["{\"version\":\"2.1.0\",\"runs\":[".Length..^2]
        + "]}";

}
