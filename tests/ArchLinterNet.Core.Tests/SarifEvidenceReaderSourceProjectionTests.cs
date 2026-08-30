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
                "{\"ruleId\":\"SEC100\",\"message\":{\"text\":\"unsafe input\"},\"level\":\"error\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\"src\\\\App.cs\"},\"region\":{\"startLine\":7,\"startColumn\":3,\"endLine\":7,\"endColumn\":9}}}],\"fingerprints\":{\"primary\":\"abc\"},\"partialFingerprints\":{\"partial\":\"def\"}}"));

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
            Assert.That(diagnostic.Fingerprints.Single(), Is.EqualTo(
                new SarifEvidenceSourceFingerprint("primary", "abc")));
            Assert.That(diagnostic.PartialFingerprints.Single(), Is.EqualTo(
                new SarifEvidenceSourceFingerprint("partial", "def", isPartial: true)));
        });
    }

    [Test]
    public void Read_AbsentOptionalSourceFactsRemainAbsentAndLevelIsUnspecified()
    {
        _repository.AddUtf8File("scan.sarif", Sarif(string.Empty, "{}"));

        SarifEvidenceReadResult result = new SarifEvidenceReader().Read(
            Requirement(),
            _repository.Root,
            new SarifEvidenceArtifactReference("scan.sarif", "external.scan"),
            new SarifEvidenceAssessmentContext("repo", "revision"));

        SarifEvidenceSourceDiagnostic diagnostic = result.SourceDiagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SarifEvidenceTrustStatus.Valid));
            Assert.That(diagnostic.Message, Is.Null);
            Assert.That(diagnostic.RuleId, Is.Null);
            Assert.That(diagnostic.SourceSeverity, Is.EqualTo(SarifEvidenceSourceSeverity.Unspecified));
            Assert.That(diagnostic.PrimaryLocation, Is.Null);
            Assert.That(diagnostic.Project, Is.Null);
            Assert.That(diagnostic.DriverRuleTags, Is.Empty);
            Assert.That(diagnostic.FingerprintPairs, Is.Empty);
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
    };

    private static string Sarif(string driverMembers, string result) =>
        "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\"" +
        (string.IsNullOrEmpty(driverMembers) ? string.Empty : "," + driverMembers) +
        "}},\"automationDetails\":{\"id\":\"assessment-42\"},\"invocations\":[{\"executionSuccessful\":true}]," +
        "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"revision\"}],\"results\":[" + result + "]}]}";
}
