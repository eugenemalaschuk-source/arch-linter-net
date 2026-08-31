using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifEvidenceReaderSourceProjectionTests
{
    private SarifEvidenceTestRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new SarifEvidenceTestRepository();

    [TearDown]
    public void TearDown() => _repository.Dispose();

    [Test]
    public void Read_ValidRunProjectsTypedSourceFactsAndExactDriverRuleTags()
    {
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(
                "\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[\"security\",\"injection\"]}},{\"id\":\"OTHER\",\"properties\":{\"tags\":[\"other\"]}}]",
                "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"unsafe input\"},\"level\":\"error\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"src\\\\App.cs\"},\"region\":{\"startLine\":7,\"startColumn\":3,\"endLine\":7,\"endColumn\":9}}}],\"fingerprints\":{\"zeta\":\"z-value\",\"alpha\":\"a-value\"},\"partialFingerprints\":{\"zeta-partial\":\"z-value\",\"alpha-partial\":\"a-value\"}}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid), result.Detail);
        SarifEvidenceSourceDiagnostic diagnostic = result.SourceDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(diagnostic.Message, Is.EqualTo("unsafe input"));
            Assert.That(diagnostic.RuleId, Is.EqualTo("SEC100"));
            Assert.That(diagnostic.SourceSeverity, Is.EqualTo(SarifEvidenceSourceSeverity.Error));
            Assert.That(diagnostic.Project, Is.EqualTo("App"));
            Assert.That(diagnostic.PrimaryLocation!.Path, Is.EqualTo("src/App.cs"));
            Assert.That(diagnostic.PrimaryLocation.Region!.StartLine, Is.EqualTo(7));
            Assert.That(diagnostic.PrimaryLocation.Region.EndColumn, Is.EqualTo(9));
            Assert.That(diagnostic.DriverRuleTags, Is.EqualTo(new[] { "security", "injection" }));
            Assert.That(diagnostic.Fingerprints, Is.EqualTo(new[]
            {
                new SarifEvidenceSourceFingerprint("alpha", "a-value"),
                new SarifEvidenceSourceFingerprint("zeta", "z-value"),
            }));
            Assert.That(diagnostic.PartialFingerprints, Is.EqualTo(new[]
            {
                new SarifEvidenceSourceFingerprint("alpha-partial", "a-value", isPartial: true),
                new SarifEvidenceSourceFingerprint("zeta-partial", "z-value", isPartial: true),
            }));
            Assert.That(diagnostic.FingerprintPairs, Is.EqualTo(new[]
            {
                new SarifEvidenceSourceFingerprint("alpha", "a-value"),
                new SarifEvidenceSourceFingerprint("zeta", "z-value"),
                new SarifEvidenceSourceFingerprint("alpha-partial", "a-value", isPartial: true),
                new SarifEvidenceSourceFingerprint("zeta-partial", "z-value", isPartial: true),
            }));
        });
    }

    [Test]
    public void Read_MissingResultMessageIsRejectedFailClosed()
    {
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, "{}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain("must contain a message"));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Read_MessageIdWithoutResolvedTextIsRejectedFailClosed()
    {
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(string.Empty, "{\"ruleId\":\"SEC100\",\"message\":{\"id\":\"message-1\"}}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain("message.id"));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [TestCase("\"rules\":{}", "rules member")]
    [TestCase("\"rules\":[false]", "rule must be an object")]
    [TestCase("\"rules\":[{}]", "declare a string id")]
    [TestCase("\"rules\":[{\"id\":\" \"}]", "non-blank id")]
    [TestCase("\"rules\":[{\"id\":\"SEC100\",\"properties\":false}]", "properties member")]
    [TestCase("\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":false}}]", "tags member")]
    [TestCase("\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[false]}}]", "tag must be a string")]
    public void Read_MalformedDriverRuleDescriptorsAreRejectedFailClosed(string driverMembers, string expectedDetail)
    {
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(driverMembers, "{\"message\":{\"text\":\"unread result\"}}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain(expectedDetail));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [TestCase("{\"message\":false}", "message member")]
    [TestCase("{\"message\":{\"text\":false}}", "string text")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"level\":\"trace\"}", "level must be one")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"ruleId\":false}", "ruleId member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"rule\":false}", "rule member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"rule\":{\"id\":false}}", "rule.id member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"ruleIndex\":-1}", "ruleIndex member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"properties\":false}", "properties member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"properties\":{\"project\":false}}", "properties.project member")]
    public void Read_MalformedProjectedResultMembersAreRejectedFailClosed(string sourceResult, string expectedDetail)
    {
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, sourceResult));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain(expectedDetail));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":false}", "", "locations member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[false]}", "", "primary location must be an object")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[{\"physicalLocation\":false}]}", "", "physicalLocation member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":false}}]}", "", "artifactLocation member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"../App.cs\"}}}]}", "", "repository-relative path")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[{\"physicalLocation\":{\"region\":false}}]}", "", "region member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"fingerprints\":false}", "", "fingerprints member")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"partialFingerprints\":{\"pair\":false}}", "", "partialFingerprints must contain")]
    [TestCase("{\"message\":{\"text\":\"x\"}}", "\"artifacts\":false", "artifacts member")]
    [TestCase("{\"message\":{\"text\":\"x\"}}", "\"artifacts\":[false]", "artifact must be an object")]
    [TestCase("{\"message\":{\"text\":\"x\"}}", "\"artifacts\":[{\"location\":false}]", "artifact location must be an object")]
    [TestCase("{\"message\":{\"text\":\"x\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"src/B.cs\",\"index\":0}}}]}", "\"artifacts\":[{\"location\":{\"uri\":\"src/A.cs\"}}]", "resolve to different paths")]
    public void Read_MalformedSourceLocationsAndArtifactsAreRejectedFailClosed(
        string sourceResult,
        string runMembers,
        string expectedDetail)
    {
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, sourceResult, runMembers));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain(expectedDetail));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Read_ResolvesRuleReferencesByIdAndIndex()
    {
        const string Rules = "\"rules\":[{\"id\":\"SEC100\"},{\"id\":\"SEC200\"}]";
        string results =
            "[{\"rule\":{\"id\":\"SEC100\"},\"message\":{\"text\":\"by id\"}}," +
            "{\"ruleIndex\":1,\"message\":{\"text\":\"by index\"}}]";
        _repository.AddUtf8File("scan.sarif", Sarif(Rules, results));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid), result.Detail);
            Assert.That(result.SourceDiagnostics.Select(diagnostic => diagnostic.RuleId), Is.EqualTo(new[] { "SEC100", "SEC200" }));
        });
    }

    [Test]
    public void Read_UsesIndexedDescriptorTagsWhenDriverRuleIdsRepeat()
    {
        const string Rules =
            "\"rules\":[{\"id\":\"DUP\",\"properties\":{\"tags\":[\"first\"]}}," +
            "{\"id\":\"DUP\",\"properties\":{\"tags\":[\"second\"]}}," +
            "{\"id\":\"SEC100/injection\",\"properties\":{\"tags\":[\"hierarchical\"]}}]";
        const string Results =
            "[{\"ruleId\":\"DUP\",\"ruleIndex\":0,\"message\":{\"text\":\"first\"}}," +
            "{\"rule\":{\"id\":\"DUP\",\"index\":1},\"message\":{\"text\":\"second\"}}," +
            "{\"rule\":{\"id\":\"SEC100/injection\",\"index\":2},\"message\":{\"text\":\"hierarchical\"}}]";
        _repository.AddUtf8File("scan.sarif", Sarif(Rules, Results));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid), result.Detail);
            Assert.That(result.SourceDiagnostics.Select(diagnostic => diagnostic.RuleId),
                Is.EqualTo(["DUP", "DUP", "SEC100/injection"]));
            Assert.That(result.SourceDiagnostics.Select(diagnostic => diagnostic.DriverRuleTags), Is.EqualTo(
            [
                new[] { "first" },
                new[] { "second" },
                new[] { "hierarchical" },
            ]));
        });
    }

    [Test]
    public void Read_AmbiguousRepeatedDriverRuleIdWithoutIndexIsRejectedFailClosed()
    {
        const string Rules =
            "\"rules\":[{\"id\":\"DUP\",\"properties\":{\"tags\":[\"first\"]}}," +
            "{\"id\":\"DUP\",\"properties\":{\"tags\":[\"second\"]}}]";
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(Rules, "{\"ruleId\":\"DUP\",\"message\":{\"text\":\"ambiguous\"}}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain("multiple tool driver descriptors"));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Read_ResolvesArtifactIndexesAndKeepsDistinctPaths()
    {
        const string Artifacts = "\"artifacts\":[{\"location\":{\"uri\":\"src/A.cs\"}},{\"location\":{\"uri\":\"src/B.cs\"}}]";
        string results =
            "[{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"first\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"index\":0}}}]}," +
            "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"second\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"index\":1}}}]}]";
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, results, Artifacts));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid), result.Detail);
            Assert.That(result.SourceDiagnostics.Select(diagnostic => diagnostic.PrimaryLocation!.Path), Is.EqualTo(new[] { "src/A.cs", "src/B.cs" }));
        });
    }

    [Test]
    public void Read_UnresolvableArtifactIndexIsRejectedFailClosed()
    {
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(string.Empty, "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"missing artifact\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"index\":3}}}]}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(result.Detail, Does.Contain("cannot be resolved"));
            Assert.That(result.SourceDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Read_EmptyPhysicalLocationIsCollapsedToNoSourceLocation()
    {
        _repository.AddUtf8File(
            "scan.sarif",
            Sarif(string.Empty, "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"no location fact\"},\"locations\":[{\"physicalLocation\":{}}]}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid), result.Detail);
            Assert.That(result.SourceDiagnostics.Single().PrimaryLocation, Is.Null);
        });
    }

    [TestCase("startLine", 0)]
    [TestCase("startColumn", 0)]
    [TestCase("endLine", -1)]
    [TestCase("endColumn", 0)]
    [TestCase("charOffset", -1)]
    [TestCase("charLength", -1)]
    public void Read_RegionValuesOutsideSarifBoundsAreRejectedFailClosed(string property, int value)
    {
        string result =
            "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"invalid region\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"src/App.cs\"},\"region\":{\""
            + property + "\":" + value + "}}}]}";
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, result));

        SarifEvidenceReadResult read = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(read.Detail, Does.Contain($"region.{property}"));
            Assert.That(read.SourceDiagnostics, Is.Empty);
        });
    }

    [TestCase("\"startLine\":7,\"endLine\":6", "region.endLine")]
    [TestCase("\"startLine\":7,\"startColumn\":8,\"endLine\":7,\"endColumn\":7", "region.endColumn")]
    public void Read_RegionEndingBeforeItsStartIsRejectedFailClosed(string members, string expectedDetail)
    {
        string result =
            "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"inconsistent region\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"src/App.cs\"},\"region\":{"
            + members + "}}}]}";
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, result));

        SarifEvidenceReadResult read = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(read.Detail, Does.Contain(expectedDetail));
            Assert.That(read.SourceDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void Read_MalformedSourceFactOrWrongContextExposesNoDiagnostics()
    {
        _repository.AddUtf8File("malformed.sarif", Sarif(string.Empty, "{\"level\":false}"));
        _repository.AddUtf8File("wrong-context.sarif", Sarif(string.Empty, "{\"ruleId\":\"SEC100\"}"));
        var reader = new SarifEvidenceReader();

        SarifEvidenceReadResult malformed = reader.Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("malformed.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));
        SarifEvidenceReadResult wrongContext = reader.Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("wrong-context.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("other-repo", "revision"));

        Assert.Multiple(() =>
        {
            Assert.That(malformed.Status, Is.EqualTo(SarifEvidenceTrustStatus.UnsupportedShape));
            Assert.That(malformed.SourceDiagnostics, Is.Empty);
            Assert.That(wrongContext.Status, Is.EqualTo(SarifEvidenceTrustStatus.WrongRepository));
            Assert.That(wrongContext.SourceDiagnostics, Is.Empty);
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
        DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
        {
            Severity = new Dictionary<string, string> { ["error"] = "strict" },
        },
    };

    private static string Sarif(string driverMembers, string result, string runMembers = "") =>
        "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"" +
        (string.IsNullOrEmpty(driverMembers) ? string.Empty : "," + driverMembers) +
        "}},\"automationDetails\":{\"id\":\"assessment-42\"},\"invocations\":[{\"executionSuccessful\":true}]," +
        (string.IsNullOrEmpty(runMembers) ? string.Empty : runMembers + ",") +
        "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"revision\"}],\"results\":" +
        (result.TrimStart().StartsWith("[", StringComparison.Ordinal) ? result : "[" + result + "]") + "}]}";
}
