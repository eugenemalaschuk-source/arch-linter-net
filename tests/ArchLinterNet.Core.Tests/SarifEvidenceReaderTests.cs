using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderTests
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

    [Test]
    public void Read_MatchingSuccessfulZeroResultRun_IsValidAndCarriesStableProvenance()
    {
        string json = Sarif(
            tool: "Acme.Scanner",
            runId: "assessment-42",
            repository: "https://example.test/acme/repo",
            revision: "abc123",
            results: "[]");
        _repository.AddUtf8File("reports/scan.sarif", json);
        var requirement = Requirement();
        var artifact = new SarifEvidenceArtifactReference(
            "reports\\scan.sarif",
            "external.scan",
            new SarifEvidenceProducerContext("https://example.test/acme/repo", "abc123", "strict"));
        var context = new SarifEvidenceAssessmentContext(
            "https://example.test/acme/repo",
            "abc123",
            "strict");

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            requirement,
            _repository.Root,
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
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
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
    [TestCase("[]", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"runs\":[]}", SarifEvidenceTrustStatus.UnsupportedVersion)]
    [TestCase("{\"version\":false,\"runs\":[]}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"version\":\"2.0.0\",\"runs\":[]}", SarifEvidenceTrustStatus.UnsupportedVersion)]
    [TestCase("{\"version\":\"2.1.0\",\"runs\":{}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    public void Read_InvalidDocument_ReturnsDistinctTrustStatus(
        string json,
        SarifEvidenceTrustStatus expectedStatus)
    {
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"),
            new SarifEvidenceLimits(4096, 4, 10));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
        Assert.That(result.ArtifactSha256, Is.Not.Null);
    }

    [TestCase("{}", SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase("{\"tool\":null}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"tool\":{}}", SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase("{\"tool\":{\"driver\":null}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"tool\":{\"driver\":{}}}", SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":false}}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":false}}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"}}}", SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"}},\"automationDetails\":null}", SarifEvidenceTrustStatus.UnsupportedShape)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"}},\"automationDetails\":{}}", SarifEvidenceTrustStatus.MissingExpectedRun)]
    [TestCase("{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"}},\"automationDetails\":{\"id\":false}}", SarifEvidenceTrustStatus.UnsupportedShape)]
    public void Read_IncompleteOrIllTypedRunIdentity_IsNeverSelected(
        string run,
        SarifEvidenceTrustStatus expectedStatus)
    {
        _repository.AddUtf8File("scan.sarif", SarifDocumentWithRun(run));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_MalformedSelectedRunMembers_AreRejectedWithTheirSpecificTrustStatus()
    {
        string standard = RunObject("Acme.Scanner", "assessment-42");
        string resultShape = standard.Replace("\"results\":[]", "\"results\":{}", StringComparison.Ordinal);
        string missingInvocation = standard.Replace(
            "\"invocations\":[{\"executionSuccessful\":true}],",
            string.Empty,
            StringComparison.Ordinal);
        string invocationShape = standard.Replace(
            "\"invocations\":[{\"executionSuccessful\":true}]",
            "\"invocations\":{}",
            StringComparison.Ordinal);
        string emptyInvocation = standard.Replace(
            "\"invocations\":[{\"executionSuccessful\":true}]",
            "\"invocations\":[]",
            StringComparison.Ordinal);
        _repository.AddUtf8File("result-shape.sarif", SarifDocumentWithRun(resultShape));
        _repository.AddUtf8File("missing-invocation.sarif", SarifDocumentWithRun(missingInvocation));
        _repository.AddUtf8File("invocation-shape.sarif", SarifDocumentWithRun(invocationShape));
        _repository.AddUtf8File("empty-invocation.sarif", SarifDocumentWithRun(emptyInvocation));
        var reader = new SarifEvidenceReader();

        SarifEvidenceReadResult malformedResults = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("result-shape.sarif", "external.scan"));
        SarifEvidenceReadResult missingInvocations = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("missing-invocation.sarif", "external.scan"));
        SarifEvidenceReadResult malformedInvocations = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("invocation-shape.sarif", "external.scan"));
        SarifEvidenceReadResult emptyInvocations = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("empty-invocation.sarif", "external.scan"));

        Assert.Multiple(() =>
        {
            Assert.That(malformedResults.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(missingInvocations.Status, Is.EqualTo(SarifEvidenceTrustStatus.IncompleteExecution));
            Assert.That(malformedInvocations.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(emptyInvocations.Status, Is.EqualTo(SarifEvidenceTrustStatus.IncompleteExecution));
        });
    }

    [TestCase("{}")]
    [TestCase("[false]")]
    [TestCase("[{\"repositoryUri\":false}]")]
    public void Read_MalformedVersionControlProvenance_IsRejected(string provenance)
    {
        string run = RunObject("Acme.Scanner", "assessment-42").Replace(
            "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"revision\"}]",
            $"\"versionControlProvenance\":{provenance}",
            StringComparison.Ordinal);
        _repository.AddUtf8File("scan.sarif", SarifDocumentWithRun(run));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
    }

    [Test]
    public void Read_DuplicateProperties_AreRejectedAfterTheSelectedResultBound()
    {
        string json = Sarif(
            tool: "Acme.Scanner",
            runId: "assessment-42",
            results: "[{\"properties\":{\"duplicate\":1,\"duplicate\":2}}]");
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
    }

    [Test]
    public void Read_SplitVersionControlProvenanceEntries_DoNotProveARepositoryRevisionPair()
    {
        string run = RunObject("Acme.Scanner", "assessment-42").Replace(
            "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"revision\"}]",
            "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\"},{\"revisionId\":\"revision\"}]",
            StringComparison.Ordinal);
        _repository.AddUtf8File("scan.sarif", SarifDocumentWithRun(run));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.ConflictingContext));
    }

    [Test]
    public void Read_MissingExpectedAndAmbiguousRuns_AreNotZeroResultSuccess()
    {
        string missing = Sarif("Other.Scanner", "assessment-42", results: "[]");
        string ambiguous =
            $"{{\"version\":\"2.1.0\",\"runs\":[{RunObject("Acme.Scanner", "assessment-42", results: "[]")},{RunObject("Acme.Scanner", "assessment-42", results: "[]")}]}}";
        _repository.AddUtf8File("missing.sarif", missing);
        _repository.AddUtf8File("ambiguous.sarif", ambiguous);
        var reader = new SarifEvidenceReader();

        SarifEvidenceReadResult missingResult = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("missing.sarif", "external.scan"));
        SarifEvidenceReadResult ambiguousResult = reader.Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("ambiguous.sarif", "external.scan"));

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
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.That(result.Status, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Read_OptionalAbsent_IsExplicitlyNotConfigured()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement();
        requirement.Required = false;

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            requirement,
            _repository.Root,
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
        _repository.AddUtf8File("reports/scan.sarif", json);
        using var secondRepository = new SarifEvidenceTestRepository();
        secondRepository.AddUtf8File("reports/scan.sarif", json);
        var artifact = new SarifEvidenceArtifactReference("reports/scan.sarif", "external.scan");
        var context = new SarifEvidenceAssessmentContext("repo", "revision", null);

        SarifEvidenceProvenance first = new SarifEvidenceReader()
            .Read(Requirement(), _repository.Root, artifact, context).Provenance;
        SarifEvidenceProvenance second = new SarifEvidenceReader()
            .Read(Requirement(), secondRepository.Root, artifact, context).Provenance;

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Read_HashesExactArtifactBytesWithoutTextRoundTrip()
    {
        byte[] artifactBytes = [0xFF, 0x00, 0xFE, 0x7B, 0x7D];
        _repository.AddFile("binary.sarif", artifactBytes);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(), _repository.Root, new SarifEvidenceArtifactReference("binary.sarif", "external.scan"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.MalformedInput));
            Assert.That(result.ArtifactSha256, Is.EqualTo(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(artifactBytes))));
        });
    }

    [Test]
    public void Read_InjectedVerifiedEvidenceFileSystem_UsesTheSuppliedByteStream()
    {
        _repository.AddUtf8File("scan.sarif", "not sarif");
        string supplied = Sarif("Acme.Scanner", "assessment-42");
        var fileSystem = new InMemoryEvidenceFileSystem(supplied);

        SarifEvidenceReadResult result = new SarifEvidenceReader(fileSystem).Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(fileSystem.OpenedPath, Is.EqualTo("scan.sarif"));
            Assert.That(result.ArtifactSha256, Is.EqualTo(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(supplied)))));
        });
    }

    [Test]
    public void Read_SeparateArchitectureFileSystemInstance_IsAWorkingEvidenceCapability()
    {
        _repository.AddUtf8File("scan.sarif", Sarif("Acme.Scanner", "assessment-42"));

        SarifEvidenceReadResult result = new SarifEvidenceReader(new ArchitectureFileSystem()).Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
    }

    [Test]
    public void Read_EvidenceCapabilityIoFailure_IsReportedAsUnreadableInput()
    {
        var reader = new SarifEvidenceReader(new ThrowingEvidenceFileSystem());

        SarifEvidenceReadResult result = reader.Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnreadableInput));
            Assert.That(result.ArtifactPath, Is.Null);
            Assert.That(result.ArtifactSha256, Is.Null);
        });
    }

    [Test]
    public void Read_UnboundedByteLimit_RemainsBytePreserving()
    {
        string json = Sarif("Acme.Scanner", "assessment-42");
        _repository.AddUtf8File("scan.sarif", json);

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"),
            new SarifEvidenceLimits(long.MaxValue, 4, 10));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Read_BlankProgrammaticToolVersion_IsRejected(string toolVersion)
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement();
        requirement.ToolVersion = toolVersion;

        Assert.That(
            () => new SarifEvidenceReader().Read(
                requirement,
                _repository.Root,
                new SarifEvidenceArtifactReference("scan.sarif", "external.scan")),
            Throws.TypeOf<ArgumentException>());
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

    private static string SarifDocumentWithRun(string run) =>
        $"{{\"version\":\"2.1.0\",\"runs\":[{run}]}}";

    private static string RunObject(
        string tool,
        string runId,
        string repository = "repo",
        string revision = "revision",
        string results = "[]",
        string invocation = "{\"executionSuccessful\":true}") =>
        $"{{\"tool\":{{\"driver\":{{\"name\":\"{tool}\",\"version\":\"7.2\"}}}},\"automationDetails\":{{\"id\":\"{runId}\"}},\"invocations\":[{invocation}],\"versionControlProvenance\":[{{\"repositoryUri\":\"{repository}\",\"revisionId\":\"{revision}\"}}],\"results\":{results}}}";

    private sealed class InMemoryEvidenceFileSystem(string artifact) : IArchitectureEvidenceFileSystem
    {
        public string? OpenedPath { get; private set; }

        public Stream OpenRepositoryLocalRegularFile(string repositoryRoot, string repositoryRelativePath)
        {
            OpenedPath = repositoryRelativePath;
            return new MemoryStream(Encoding.UTF8.GetBytes(artifact), writable: false);
        }
    }

    private sealed class ThrowingEvidenceFileSystem : IArchitectureEvidenceFileSystem
    {
        public Stream OpenRepositoryLocalRegularFile(string repositoryRoot, string repositoryRelativePath)
        {
            throw new IOException("The evidence source was unavailable.");
        }
    }
}
