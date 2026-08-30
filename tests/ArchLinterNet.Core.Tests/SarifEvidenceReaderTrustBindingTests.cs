using System.Runtime.InteropServices;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class SarifEvidenceReaderTrustBindingTests
{
    private SarifEvidenceTestRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new SarifEvidenceTestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _repository.Dispose();
    }

    [TestCase("repository", SarifEvidenceTrustStatus.WrongRepository)]
    [TestCase("revision", SarifEvidenceTrustStatus.WrongRevision)]
    [TestCase("scope", SarifEvidenceTrustStatus.WrongScope)]
    public void Read_WrongRequiredBinding_IsRejectedWithDimensionSpecificStatus(
        string dimension,
        SarifEvidenceTrustStatus expectedStatus)
    {
        _repository.AddUtf8File("scan.sarif", BuildSarif());
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

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            requirement,
            _repository.Root,
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
        _repository.AddUtf8File("scan.sarif", json);
        var expected = new SarifEvidenceAssessmentContext(
            repository: "repository",
            revision: "revision",
            scope: dimension == "scope" ? "scope" : null);

        SarifEvidenceProducerContext? producer = dimension == "scope"
            ? new SarifEvidenceProducerContext("external.scan", "repository", "revision", null)
            : new SarifEvidenceProducerContext("external.scan", null, null, null);
        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan", producer),
            expected,
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_WrongLogicalId_IsRejectedEvenWhenArtifactOtherwiseMatches()
    {
        _repository.AddUtf8File("scan.sarif", BuildSarif());

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
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
        _repository.AddUtf8File("scan.sarif", BuildSarif());

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
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
    [TestCase("C:relative.sarif", SarifEvidenceTrustStatus.UnsafePath)]
    [TestCase("scan.sarif:Zone.Identifier", SarifEvidenceTrustStatus.UnsafePath)]
    public void Read_MissingOrUnsafeArtifact_IsRejectedBeforeRead(
        string path,
        SarifEvidenceTrustStatus expectedStatus)
    {
        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference(path, "external.scan"));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_ArtifactRunsAndResultsRespectIndependentBounds()
    {
        _repository.AddUtf8File(
            "too-large.sarif",
            BuildSarif(results: "[{\"ruleId\":\"A\"},{\"ruleId\":\"B\"}]"));
        _repository.AddUtf8File("too-many-runs.sarif", BuildSarifWithTwoRuns());

        var reader = new SarifEvidenceReader();
        SarifEvidenceReadResult results = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("too-large.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"), new SarifEvidenceLimits(4096, 4, 1));
        SarifEvidenceReadResult runs = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("too-many-runs.sarif", "external.scan"),
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
    public void Read_ResultBoundPrecedesDuplicatePropertyTraversal()
    {
        _repository.AddUtf8File(
            "too-many-results.sarif",
            BuildSarif(results: "[{\"properties\":{\"key\":1,\"key\":2}},{\"ruleId\":\"B\"}]"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("too-many-results.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"),
            new SarifEvidenceLimits(4096, 4, 1));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.TooManyResults));
    }

    [Test]
    public void Read_ArtifactByteLimitHashesConsumedBytesAndDoesNotParsePartialJson()
    {
        string json = BuildSarif();
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repository", "revision"), new SarifEvidenceLimits(10, 4, 10));

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

    [Test]
    public void Read_Directory_IsRejectedAsAnUnsafeArtifact()
    {
        Directory.CreateDirectory(_repository.GetPath("directory.sarif"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("directory.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsafePath));
    }

    [Test]
    public void VerifiedEvidenceFileSystem_RejectsAlternateDataStreamSyntax()
    {
        _repository.AddUtf8File("scan.sarif", BuildSarif());

        Assert.That(
            () => new ArchLinterNet.Core.IO.ArchitectureFileSystem()
                .OpenRepositoryLocalRegularFile(_repository.Root, "scan.sarif:Zone.Identifier"),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void Read_Fifo_IsRejectedWithoutBlocking()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows does not expose POSIX FIFOs in this test environment.");
        }

        string fifo = _repository.GetPath("evidence.fifo");
        Assert.That(CreateFifo(fifo, 0x1A4), Is.EqualTo(0));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("evidence.fifo", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsafePath));
    }

    [Test]
    public void Read_FinalSymlink_IsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The Windows CI account cannot be assumed to have symlink creation rights.");
        }

        using var outside = new SarifEvidenceTestRepository();
        string outsidePath = outside.AddUtf8File("outside.sarif", BuildSarif());
        File.CreateSymbolicLink(_repository.GetPath("scan.sarif"), outsidePath);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsafePath));
    }

    [Test]
    public void Read_AncestorSymlink_IsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The Windows CI account cannot be assumed to have symlink creation rights.");
        }

        using var outside = new SarifEvidenceTestRepository();
        outside.AddUtf8File("scan.sarif", BuildSarif());
        Directory.CreateSymbolicLink(_repository.GetPath("linked"), outside.Root);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("linked/scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsafePath));
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

    [LibraryImport("libc", EntryPoint = "mkfifo", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int CreateFifo(string path, uint mode);

}
