using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderTests
{
    [Test]
    public void Read_MatchingSuccessfulZeroResultRun_IsValidAndCarriesStableProvenance()
    {
        string json = Sarif(
            tool: "Acme.Scanner",
            runId: "assessment-42",
            repository: "https://example.test/acme/repo",
            revision: "abc123",
            results: "[]");
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/reports/scan.sarif", json, DateTime.UtcNow);
        var requirement = Requirement();
        var artifact = new SarifEvidenceArtifactReference(
            "reports\\scan.sarif",
            "external.scan",
            new SarifEvidenceProducerContext("https://example.test/acme/repo", "abc123", "strict"));
        var context = new SarifEvidenceAssessmentContext(
            "https://example.test/acme/repo",
            "abc123",
            "strict");

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            requirement,
            "/repo",
            artifact,
            context,
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.LogicalId, Is.EqualTo("external.scan"));
            Assert.That(result.ToolName, Is.EqualTo("Acme.Scanner"));
            Assert.That(result.ToolVersion, Is.EqualTo("7.2"));
            Assert.That(result.RunId, Is.EqualTo("assessment-42"));
            Assert.That(result.ResultCount, Is.EqualTo(0));
            Assert.That(result.ArtifactPath, Is.EqualTo("reports/scan.sarif"));
            Assert.That(result.ArtifactSha256, Is.EqualTo(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json)))));
            Assert.That(result.Context, Is.EqualTo(new SarifEvidenceResolvedContext(
                "external.scan", "https://example.test/acme/repo", "abc123", "strict")));
        });
    }

    [Test]
    public void Read_MatchingSuccessfulNonzeroRun_PreservesResultCountAndSelectedToolFacts()
    {
        string json = Sarif(
            tool: "Acme.Scanner",
            runId: "assessment-42",
            results: "[{\"ruleId\":\"A\"},{\"ruleId\":\"B\"}]");
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision", null),
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(result.ResultCount, Is.EqualTo(2));
            Assert.That(result.Provenance.SelectedToolName, Is.EqualTo("Acme.Scanner"));
            Assert.That(result.Provenance.SelectedRunId, Is.EqualTo("assessment-42"));
        });
    }

    [TestCase("not-json", SarifEvidenceTrustStatus.MalformedInput)]
    [TestCase("{\"version\":\"2.0.0\",\"runs\":[]}", SarifEvidenceTrustStatus.UnsupportedVersion)]
    [TestCase("{\"version\":\"2.1.0\",\"runs\":{}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    public void Read_InvalidDocument_ReturnsDistinctTrustStatus(
        string json,
        SarifEvidenceTrustStatus expectedStatus)
    {
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            "/repo",
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"),
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.ArtifactSha256, Is.Not.Null);
    }

    [Test]
    public void Read_MissingExpectedAndAmbiguousRuns_AreNotZeroResultSuccess()
    {
        string missing = Sarif("Other.Scanner", "assessment-42", results: "[]");
        string ambiguous =
            $"{{\"version\":\"2.1.0\",\"runs\":[{RunObject("Acme.Scanner", "assessment-42", results: "[]")},{RunObject("Acme.Scanner", "assessment-42", results: "[]")}]}}";

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/missing.sarif", missing, DateTime.UtcNow);
        fileSystem.AddFile("/repo/ambiguous.sarif", ambiguous, DateTime.UtcNow);
        var reader = new SarifEvidenceReader(fileSystem);

        SarifEvidenceReadResult missingResult = reader.Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("missing.sarif", "external.scan"));
        SarifEvidenceReadResult ambiguousResult = reader.Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("ambiguous.sarif", "external.scan"));

        Assert.Multiple(() =>
        {
            Assert.That(missingResult.Status, Is.EqualTo(SarifEvidenceTrustStatus.MissingExpectedRun));
            Assert.That(ambiguousResult.Status, Is.EqualTo(SarifEvidenceTrustStatus.AmbiguousExpectedRun), ambiguousResult.Detail);
        });
    }

    [TestCase("false", SarifEvidenceTrustStatus.FailedExecution)]
    [TestCase("missing", SarifEvidenceTrustStatus.IncompleteExecution)]
    [TestCase("null", SarifEvidenceTrustStatus.IncompleteExecution)]
    public void Read_FailedOrIncompleteInvocation_IsNeverSuccessful(
        string executionValue,
        SarifEvidenceTrustStatus expectedStatus)
    {
        string invocation = executionValue == "missing"
            ? "{}"
            : $"{{\"executionSuccessful\":{executionValue}}}";
        string json = Sarif("Acme.Scanner", "assessment-42", invocation: invocation);
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo/scan.sarif", json, DateTime.UtcNow);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(), "/repo", new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_OptionalAbsent_IsExplicitlyNotConfigured()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement();
        requirement.Required = false;

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            requirement,
            "/repo",
            artifact: null,
            expectedContext: new SarifEvidenceAssessmentContext(),
            limits: new SarifEvidenceLimits(4096, 4, 10));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.OptionalNotConfigured));
            Assert.That(result.ReasonCode, Is.EqualTo("optional_not_configured"));
            Assert.That(result.ResultCount, Is.Null);
            Assert.That(result.ArtifactSha256, Is.Null);
        });
    }

    [Test]
    public void Read_EquivalentBytesAndContext_ProduceEqualProvenance()
    {
        string json = Sarif("Acme.Scanner", "assessment-42", results: "[]");
        var firstFileSystem = new FakeArchitectureFileSystem();
        var secondFileSystem = new FakeArchitectureFileSystem();
        firstFileSystem.AddFile("/repo/reports/scan.sarif", json, DateTime.UtcNow);
        secondFileSystem.AddFile("/repo/reports/scan.sarif", json, DateTime.UtcNow.AddDays(2));
        var artifact = new SarifEvidenceArtifactReference("reports/scan.sarif", "external.scan");
        var context = new SarifEvidenceAssessmentContext("repo", "revision", null);
        var readerA = new SarifEvidenceReader(firstFileSystem);
        var readerB = new SarifEvidenceReader(secondFileSystem);

        SarifEvidenceProvenance first = readerA.Read(Requirement(), "/repo", artifact, context).Provenance;
        SarifEvidenceProvenance second = readerB.Read(Requirement(), "/repo", artifact, context).Provenance;

        Assert.That(second, Is.EqualTo(first));
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
        RequireScope = false,
    };

    private static string Sarif(
        string tool,
        string runId,
        string repository = "repo",
        string revision = "revision",
        string results = "[]",
        string invocation = "{\"executionSuccessful\":true}") =>
        $"{{\"version\":\"2.1.0\",\"runs\":[{RunObject(tool, runId, repository, revision, results, invocation)}]}}";

    private static string RunObject(
        string tool,
        string runId,
        string repository = "repo",
        string revision = "revision",
        string results = "[]",
        string invocation = "{\"executionSuccessful\":true}") =>
        $"{{\"tool\":{{\"driver\":{{\"name\":\"{tool}\",\"version\":\"7.2\"}}}},\"automationDetails\":{{\"id\":\"{runId}\"}},\"invocations\":[{invocation}],\"versionControlProvenance\":[{{\"repositoryUri\":\"{repository}\",\"revisionId\":\"{revision}\"}}],\"results\":{results}}}";
}
