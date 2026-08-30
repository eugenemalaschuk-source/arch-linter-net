using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SarifExternalDiagnosticSelectorTests
{
    private SarifEvidenceTestRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new SarifEvidenceTestRepository();

    [TearDown]
    public void TearDown() => _repository.Dispose();

    [Test]
    public void Select_AppliesAllFiltersMapsSeverityAndPreservesSourceProvenance()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            ruleIds: ["SEC100"],
            ruleTags: ["security"],
            projects: ["App"],
            pathPrefixes: ["src/App"],
            severity: new Dictionary<string, string>
            {
                ["error"] = "strict",
                ["warning"] = "audit",
            });
        SarifEvidenceReadResult evidence = Read(
            requirement,
            "mapped.sarif",
            Results(
                Result(
                    "SEC100",
                    "error",
                    "src/App/One.cs",
                    "one",
                    fingerprints: "{\"zeta\":\"z-value\",\"alpha\":\"a-value\"}"),
                Result(
                    "SEC100",
                    "warning",
                    "src/App/Two.cs",
                    "two",
                    partialFingerprints: "{\"partial\":\"partial-value\"}")));

        SarifExternalDiagnosticSelectionResult result = new SarifExternalDiagnosticSelector().Select(
        [
            new SarifExternalDiagnosticSelectionInput(evidence),
        ]);

        SarifSelectedExternalDiagnostic strict = result.Diagnostics.Single(diagnostic =>
            diagnostic.GovernanceMode == SarifExternalDiagnosticGovernanceMode.Strict);
        SarifSelectedExternalDiagnostic audit = result.Diagnostics.Single(diagnostic =>
            diagnostic.GovernanceMode == SarifExternalDiagnosticGovernanceMode.Audit);
        Assert.Multiple(() =>
        {
            Assert.That(result.FilterMismatches, Is.Empty);
            Assert.That(strict.Fingerprint, Is.EqualTo(new SarifExternalDiagnosticFingerprint(
                SarifExternalDiagnosticFingerprintOrigin.Source,
                "a-value",
                "alpha")));
            Assert.That(strict.SourceDiagnostic.DriverRuleTags, Is.EqualTo(new[] { "security", "code" }));
            Assert.That(strict.EvidenceProvenances, Has.Count.EqualTo(1));
            Assert.That(strict.EvidenceProvenances.Single().LogicalId, Is.EqualTo("external.scan"));
            Assert.That(strict.EvidenceProvenances.Single().Context!.Revision, Is.EqualTo("revision"));
            Assert.That(audit.Fingerprint.Origin, Is.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Deterministic));
            Assert.That(audit.Fingerprint.Value, Does.StartWith("sha256:"));
            Assert.That(audit.Fingerprint.Value, Does.Match("^sha256:[0-9a-f]{64}$"));
        });
    }

    [Test]
    public void Select_ReportsUnknownRequiredFiltersInsteadOfSilentlyDroppingThem()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            requireMatches: true,
            ruleIds: ["SEC100", "SEC404"],
            ruleTags: ["security", "missing-tag"],
            projects: ["App"],
            pathPrefixes: ["src/App"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult evidence = Read(
            requirement,
            "required.sarif",
            Results(Result("SEC100", "error", "src/App/One.cs", "present")));

        SarifExternalDiagnosticSelectionResult result = new SarifExternalDiagnosticSelector().Select(
        [
            new SarifExternalDiagnosticSelectionInput(evidence),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.HasRequiredFilterMatches, Is.False);
            Assert.That(result.FilterMismatches, Is.EqualTo(
            [
                new SarifExternalDiagnosticFilterMismatch(
                    "external.scan", SarifExternalDiagnosticFilterDimension.RuleId, "SEC404"),
                new SarifExternalDiagnosticFilterMismatch(
                    "external.scan", SarifExternalDiagnosticFilterDimension.RuleTag, "missing-tag"),
            ]));
        });
    }

    [Test]
    public void Select_DeduplicatesEquivalentSemanticsAndOrdersIndependentOfInputOrder()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            ruleIds: ["SEC100"],
            ruleTags: ["security"],
            projects: ["App"],
            pathPrefixes: ["src/App"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult first = Read(
            requirement,
            "equivalent-a.sarif",
            Results(Result("SEC100", "error", "src/App/Shared.cs", "first display text", "{\"zeta\":\"noise\",\"stable\":\"same\"}")));
        SarifEvidenceReadResult second = Read(
            requirement,
            "equivalent-b.sarif",
            Results(Result("SEC100", "error", "src/App/Shared.cs", "second display text", "{\"stable\":\"same\",\"zeta\":\"noise\"}")));
        SarifEvidenceReadResult distinct = Read(
            requirement,
            "distinct.sarif",
            Results(Result("SEC100", "error", "src/App/Other.cs", "other", "{\"stable\":\"other\"}")));
        var selector = new SarifExternalDiagnosticSelector();

        SarifExternalDiagnosticSelectionResult forward = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(first),
            new SarifExternalDiagnosticSelectionInput(second),
            new SarifExternalDiagnosticSelectionInput(distinct),
        ]);
        SarifExternalDiagnosticSelectionResult reverse = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(distinct),
            new SarifExternalDiagnosticSelectionInput(second),
            new SarifExternalDiagnosticSelectionInput(first),
        ]);

        SarifSelectedExternalDiagnostic deduplicated = forward.Diagnostics.Single(diagnostic =>
            diagnostic.Fingerprint.Value == "same");
        Assert.Multiple(() =>
        {
            Assert.That(forward.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(deduplicated.EvidenceProvenances.Select(provenance => provenance.ArtifactPath), Is.EquivalentTo(
                new[] { "equivalent-a.sarif", "equivalent-b.sarif" }));
            Assert.That(
                deduplicated.EvidenceProvenances.Select(provenance => provenance.ArtifactPath).ToArray(),
                Is.EqualTo(reverse.Diagnostics.Single(diagnostic => diagnostic.Fingerprint.Value == "same")
                    .EvidenceProvenances.Select(provenance => provenance.ArtifactPath).ToArray()));
            Assert.That(
                forward.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity),
                Is.EqualTo(reverse.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity)));
        });
    }

    [Test]
    public void Select_UsesFallbackFingerprintAndKeepsLocationsAndRevisionsDistinct()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            ruleIds: ["SEC100"],
            ruleTags: ["security"],
            projects: ["App"],
            pathPrefixes: ["src/App"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult firstRevision = Read(
            requirement,
            "revision-one.sarif",
            Results(Result("SEC100", "error", "src/App/Same.cs", "one")),
            revision: "revision-one");
        SarifEvidenceReadResult secondRevision = Read(
            requirement,
            "revision-two.sarif",
            Results(Result("SEC100", "error", "src/App/Same.cs", "two")),
            revision: "revision-two");
        SarifEvidenceReadResult otherLocation = Read(
            requirement,
            "other-location.sarif",
            Results(Result("SEC100", "error", "src/App/Other.cs", "three")),
            revision: "revision-one");

        SarifExternalDiagnosticSelectionResult result = new SarifExternalDiagnosticSelector().Select(
        [
            new SarifExternalDiagnosticSelectionInput(firstRevision),
            new SarifExternalDiagnosticSelectionInput(secondRevision),
            new SarifExternalDiagnosticSelectionInput(otherLocation),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Has.Count.EqualTo(3));
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity).Distinct().Count(), Is.EqualTo(3));
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Fingerprint.Origin),
                Is.All.EqualTo(SarifExternalDiagnosticFingerprintOrigin.Deterministic));
        });
    }

    [Test]
    public void Select_RejectsUntrustedOrUnfilteredInputs()
    {
        ArchitectureExternalEvidenceRequirement filtered = Requirement(
            ruleIds: ["SEC100"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult untrusted = Read(
            filtered,
            "untrusted.sarif",
            Results(Result("SEC100", "error", "src/App/One.cs", "wrong context")),
            artifactRevision: "artifact-revision",
            revision: "assessment-revision");
        ArchitectureExternalEvidenceRequirement unfiltered = new()
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
        SarifEvidenceReadResult trusted = Read(
            unfiltered,
            "trusted.sarif",
            Results(Result("SEC100", "error", "src/App/One.cs", "trusted")));
        var selector = new SarifExternalDiagnosticSelector();

        Assert.Multiple(() =>
        {
            Assert.That(untrusted.IsValid, Is.False);
            Assert.That(() => selector.Select(
            [
                new SarifExternalDiagnosticSelectionInput(untrusted),
            ]), Throws.ArgumentException);
            Assert.That(() => selector.Select(
            [
                new SarifExternalDiagnosticSelectionInput(trusted),
            ]), Throws.ArgumentException);
        });
    }

    [Test]
    public void Select_UsesTheImmutableAuthorizationCapturedAtReadTime()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            ruleIds: ["SEC100"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult evidence = Read(
            requirement,
            "authorized.sarif",
            Results(Result("SEC100", "error", "src/App/One.cs", "trusted")));

        requirement.Tool = "Other.Scanner";
        requirement.ToolVersion = "99";
        requirement.Run = "other-run";
        requirement.RequireRepository = false;
        requirement.RequireRevision = false;
        requirement.RequireScope = true;
        requirement.DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
        {
            RuleIds = ["OTHER"],
            Severity = new Dictionary<string, string> { ["error"] = "audit" },
        };

        SarifExternalDiagnosticSelectionResult result = new SarifExternalDiagnosticSelector().Select(
        [
            new SarifExternalDiagnosticSelectionInput(evidence),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Authorization, Is.Not.Null);
            Assert.That(evidence.Authorization!.Tool, Is.EqualTo("Acme.Scanner"));
            Assert.That(evidence.Authorization.Run, Is.EqualTo("assessment-42"));
            Assert.That(evidence.Authorization.RequireRevision, Is.True);
            Assert.That(evidence.Authorization.DiagnosticFilter!.RuleIds, Is.EqualTo(new[] { "SEC100" }));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].GovernanceMode, Is.EqualTo(SarifExternalDiagnosticGovernanceMode.Strict));
        });
    }

    [Test]
    public void Select_AggregatesRequiredFiltersAcrossEquivalentTrustedEvidenceInEitherOrder()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            requireMatches: true,
            ruleIds: ["SEC100", "SEC200"],
            severity: new Dictionary<string, string> { ["error"] = "strict" });
        SarifEvidenceReadResult first = Read(
            requirement,
            "first.sarif",
            Results(Result("SEC100", "error", "src/App/One.cs", "first")));
        SarifEvidenceReadResult second = Read(
            requirement,
            "second.sarif",
            Results(Result("SEC200", "error", "src/App/Two.cs", "second")));
        var selector = new SarifExternalDiagnosticSelector();

        SarifExternalDiagnosticSelectionResult forward = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(first),
            new SarifExternalDiagnosticSelectionInput(second),
        ]);
        SarifExternalDiagnosticSelectionResult reverse = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(second),
            new SarifExternalDiagnosticSelectionInput(first),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(forward.HasRequiredFilterMatches, Is.True);
            Assert.That(forward.FilterMismatches, Is.Empty);
            Assert.That(reverse.FilterMismatches, Is.Empty);
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity),
                Is.EqualTo(reverse.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity)));
        });
    }

    [Test]
    public void Select_KeepsDifferentSourceSeveritiesAndGovernanceModesDistinct()
    {
        ArchitectureExternalEvidenceRequirement requirement = Requirement(
            ruleIds: ["SEC100"],
            severity: new Dictionary<string, string>
            {
                ["error"] = "audit",
                ["warning"] = "strict",
            });
        SarifEvidenceReadResult error = Read(
            requirement,
            "error.sarif",
            Results(Result("SEC100", "error", "src/App/Shared.cs", "error", "{\"stable\":\"same\"}")));
        SarifEvidenceReadResult warning = Read(
            requirement,
            "warning.sarif",
            Results(Result("SEC100", "warning", "src/App/Shared.cs", "warning", "{\"stable\":\"same\"}")));
        var selector = new SarifExternalDiagnosticSelector();

        SarifExternalDiagnosticSelectionResult forward = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(error),
            new SarifExternalDiagnosticSelectionInput(warning),
        ]);
        SarifExternalDiagnosticSelectionResult reverse = selector.Select(
        [
            new SarifExternalDiagnosticSelectionInput(warning),
            new SarifExternalDiagnosticSelectionInput(error),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(forward.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.GovernanceMode),
                Is.EquivalentTo(new[]
                {
                    SarifExternalDiagnosticGovernanceMode.Audit,
                    SarifExternalDiagnosticGovernanceMode.Strict,
                }));
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.SourceDiagnostic.SourceSeverity),
                Is.EquivalentTo(new[] { SarifEvidenceSourceSeverity.Error, SarifEvidenceSourceSeverity.Warning }));
            Assert.That(forward.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity),
                Is.EqualTo(reverse.Diagnostics.Select(diagnostic => diagnostic.CanonicalIdentity)));
        });
    }

    private static ArchitectureExternalEvidenceRequirement Requirement(
        string id = "external.scan",
        bool requireMatches = false,
        string[]? ruleIds = null,
        string[]? ruleTags = null,
        string[]? projects = null,
        string[]? pathPrefixes = null,
        Dictionary<string, string>? severity = null) => new()
        {
            Id = id,
            Format = "sarif",
            Required = true,
            Tool = "Acme.Scanner",
            ToolVersion = "7.2",
            Run = "assessment-42",
            RequireRepository = true,
            RequireRevision = true,
            DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
            {
                RuleIds = ruleIds?.ToList() ?? [],
                RuleTags = ruleTags?.ToList() ?? [],
                Projects = projects?.ToList() ?? [],
                PathPrefixes = pathPrefixes?.ToList() ?? [],
                Severity = severity ?? new Dictionary<string, string> { ["error"] = "strict" },
                RequireMatches = requireMatches,
            },
        };

    private SarifEvidenceReadResult Read(
        ArchitectureExternalEvidenceRequirement requirement,
        string path,
        string results,
        string revision = "revision",
        string? artifactRevision = null)
    {
        _repository.AddUtf8File(path, Sarif(results, artifactRevision ?? revision));
        return new SarifEvidenceReader().Read(
            requirement,
            _repository.Root,
            new SarifEvidenceArtifactReference(path, requirement.Id),
            new SarifEvidenceAssessmentContext("repo", revision));
    }

    private static string Results(params string[] results) => "[" + string.Join(",", results) + "]";

    private static string Result(
        string ruleId,
        string level,
        string path,
        string message,
        string? fingerprints = null,
        string? partialFingerprints = null) =>
        "{\"ruleId\":\"" + ruleId + "\",\"message\":{\"text\":\"" + message + "\"},\"level\":\"" + level
        + "\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\""
        + path + "\"},\"region\":{\"startLine\":7,\"startColumn\":3}}}]"
        + (fingerprints is null ? string.Empty : ",\"fingerprints\":" + fingerprints)
        + (partialFingerprints is null ? string.Empty : ",\"partialFingerprints\":" + partialFingerprints)
        + "}";

    private static string Sarif(string results, string revision) =>
        "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"Acme.Scanner\",\"version\":\"7.2\","
        + "\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[\"security\",\"code\"]}}]}},"
        + "\"automationDetails\":{\"id\":\"assessment-42\"},\"invocations\":[{\"executionSuccessful\":true}],"
        + "\"versionControlProvenance\":[{\"repositoryUri\":\"repo\",\"revisionId\":\"" + revision + "\"}],\"results\":"
        + results + "}]}";
}
