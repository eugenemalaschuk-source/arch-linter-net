using System.Text.Json;
using ArchLinterNet.Cli.Commands.Coverage.Application;
using ArchLinterNet.Cli.Commands.Validate.Application;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CanonicalAnnotationReportTests
{
    private static ValidationOutcome Outcome => new(
        true,
        Array.Empty<ArchitectureViolation>(),
        Array.Empty<string>(),
        Array.Empty<ArchitectureViolation>(),
        "off",
        Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        "off",
        Array.Empty<PolicyConsistencyDiagnostic>(),
        "off",
        Array.Empty<ArchitectureCoverageSummary>(),
        Array.Empty<ArchitectureClassificationConflict>(),
        Array.Empty<ArchitectureClassificationMetadataFailure>())
    {
        ClassificationRoles =
        [
            new ArchitectureClassificationRoleFact(
                "Example.Order", "Entity", ArchitectureClassificationSource.TypeAttribute,
                "ArchLinterNet.Annotations.EntityAttribute", new Dictionary<string, object>())
            {
                EvidenceSources = [
                    "ArchLinterNet.Annotations.EntityAttribute",
                    "Example.DomainMarkerAttribute"
                ]
            }
        ],
        CanonicalAnnotationDiagnostics =
        [
            ArchitectureCanonicalAnnotationDiagnostic.Create(
                "Example.Order", "MissingCatalogIdentity",
                "Assembly 'Example' is missing its annotation catalog marker.",
                "ArchLinterNet.Annotations.EntityAttribute")
        ]
    };

    [Test]
    public void JsonReport_ProjectsCanonicalDiagnosticsAndAllRoleEvidenceSources()
    {
        var renderer = new StructuredReportRenderer(new CliRuntime());
        string result = renderer.Render("json", true, [("strict", Outcome)]);
        using JsonDocument json = JsonDocument.Parse(result);

        JsonElement role = json.RootElement.GetProperty("classification_roles")[0];
        JsonElement diagnostic = json.RootElement.GetProperty("canonical_annotation_diagnostics")[0];
        Assert.Multiple(() =>
        {
            Assert.That(json.RootElement.GetProperty("passed").GetBoolean(), Is.True);
            Assert.That(role.GetProperty("evidence_sources").EnumerateArray()
                .Select(value => value.GetString()).ToArray(), Is.EqualTo(new[]
                {
                    "ArchLinterNet.Annotations.EntityAttribute",
                    "Example.DomainMarkerAttribute"
                }));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo("MissingCatalogIdentity"));
            Assert.That(diagnostic.GetProperty("evidence_sources")[0].GetString(),
                Is.EqualTo("ArchLinterNet.Annotations.EntityAttribute"));
        });
    }

    [Test]
    public void HumanAndSarifReports_ExposeCanonicalDiagnosticsWithoutChangingPassState()
    {
        string human = new HumanReportRenderer(new CliRuntime()).Render(true, [("strict", Outcome)]);
        string sarif = new StructuredReportRenderer(new CliRuntime()).Render("sarif", true, [("strict", Outcome)]);
        using JsonDocument json = JsonDocument.Parse(sarif);

        Assert.Multiple(() =>
        {
            Assert.That(Outcome.Passed, Is.True);
            Assert.That(human, Does.Contain("MissingCatalogIdentity"));
            Assert.That(human, Does.Contain("Classification findings:"));
            Assert.That(sarif, Does.Contain("canonical-annotation:MissingCatalogIdentity"));
            Assert.That(json.RootElement.GetProperty("runs")[0].GetProperty("results")
                .EnumerateArray().Any(result => result.GetProperty("ruleId").GetString()
                    == "canonical-annotation:MissingCatalogIdentity"), Is.True);
        });
    }

    [Test]
    public void CoverageReport_ListsCanonicalDiagnosticsAsInformationalEvidence()
    {
        string validationJson = new StructuredReportRenderer(new CliRuntime())
            .Render("json", true, [("strict", Outcome)]);
        using JsonDocument json = JsonDocument.Parse(validationJson);
        string coverage = CoverageReportRenderer.Render(json.RootElement, null, "/repo", diffFailed: false, maxFailures: null);

        Assert.Multiple(() =>
        {
            Assert.That(coverage, Does.Contain("**Status:** ✅ pass"));
            Assert.That(coverage, Does.Contain("### Canonical annotation diagnostics (1)"));
            Assert.That(coverage, Does.Contain("MissingCatalogIdentity"));
            Assert.That(coverage, Does.Contain("ArchLinterNet.Annotations.EntityAttribute"));
        });
    }
}
