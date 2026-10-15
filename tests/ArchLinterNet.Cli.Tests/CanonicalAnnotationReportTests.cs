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
    private static ValidationOutcome CreateOutcome(
        IReadOnlyList<ArchitectureClassificationRoleFact>? classificationRoles = null,
        IReadOnlyList<ArchitectureCanonicalAnnotationDiagnostic>? canonicalAnnotationDiagnostics = null) => new(
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
            ClassificationRoles = classificationRoles ?? Array.Empty<ArchitectureClassificationRoleFact>(),
            CanonicalAnnotationDiagnostics = canonicalAnnotationDiagnostics
            ?? Array.Empty<ArchitectureCanonicalAnnotationDiagnostic>()
        };

    private static ValidationOutcome CanonicalRoleOutcome => CreateOutcome(
        classificationRoles:
        [
            new ArchitectureClassificationRoleFact(
                "ValidCatalog.Order", "CommandHandler", ArchitectureClassificationSource.TypeAttribute,
                "ArchLinterNet.Annotations.CommandHandlerAttribute",
                new Dictionary<string, object> { ["boundedContext"] = "Sales", ["module"] = "Orders" })
            {
                EvidenceSources = [
                    "ArchLinterNet.Annotations.CommandHandlerAttribute",
                    "Example.DomainMarkerAttribute"
                ]
            }
        ]);

    private static ValidationOutcome MissingCatalogIdentityOutcome => CreateOutcome(
        canonicalAnnotationDiagnostics:
        [
            ArchitectureCanonicalAnnotationDiagnostic.Create(
                "MissingIdentity.Order", "MissingCatalogIdentity",
                "Assembly 'MissingIdentity' is missing its annotation catalog marker.",
                "ArchLinterNet.Annotations.CommandHandlerAttribute")
        ]);

    private static ValidationOutcome ContextDiagnosticsOutcome => CreateOutcome(
        canonicalAnnotationDiagnostics:
        [
            ArchitectureCanonicalAnnotationDiagnostic.Create(
                "ContextConflict.Order", "CanonicalContextConflict",
                "Type context values conflict for key 'boundedContext'.",
                "ArchLinterNet.Annotations.BoundedContextAttribute"),
            ArchitectureCanonicalAnnotationDiagnostic.Create(
                "InvalidContext.Order", "InvalidCanonicalMetadata",
                "The context value must be a non-empty string.",
                "ArchLinterNet.Annotations.DomainAttribute")
        ]);

    [Test]
    public void JsonReport_ProjectsCanonicalRoleAndAllRoleEvidenceSources()
    {
        var renderer = new StructuredReportRenderer(new CliRuntime());
        string result = renderer.Render("json", true, [("strict", CanonicalRoleOutcome)]);
        using JsonDocument json = JsonDocument.Parse(result);

        JsonElement role = json.RootElement.GetProperty("classification_roles")[0];
        Assert.Multiple(() =>
        {
            Assert.That(json.RootElement.GetProperty("passed").GetBoolean(), Is.True);
            Assert.That(role.GetProperty("role").GetString(), Is.EqualTo("CommandHandler"));
            Assert.That(role.GetProperty("metadata").GetProperty("boundedContext").GetString(), Is.EqualTo("Sales"));
            Assert.That(role.GetProperty("evidence_sources").EnumerateArray()
                .Select(value => value.GetString()).ToArray(), Is.EqualTo(new[]
                {
                    "ArchLinterNet.Annotations.CommandHandlerAttribute",
                    "Example.DomainMarkerAttribute"
                }));
            Assert.That(
                json.RootElement.TryGetProperty("canonical_annotation_diagnostics", out _),
                Is.False);
        });
    }

    [Test]
    public void JsonReport_ProjectsMissingCatalogIdentityWithoutCanonicalRole()
    {
        var renderer = new StructuredReportRenderer(new CliRuntime());
        string result = renderer.Render("json", true, [("strict", MissingCatalogIdentityOutcome)]);
        using JsonDocument json = JsonDocument.Parse(result);

        JsonElement roles = json.RootElement.GetProperty("classification_roles");
        JsonElement diagnostic = json.RootElement.GetProperty("canonical_annotation_diagnostics")[0];
        Assert.Multiple(() =>
        {
            Assert.That(roles.GetArrayLength(), Is.Zero);
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo("MissingCatalogIdentity"));
            Assert.That(diagnostic.GetProperty("evidence_sources")[0].GetString(),
                Is.EqualTo("ArchLinterNet.Annotations.CommandHandlerAttribute"));
        });
    }

    [Test]
    public void HumanAndSarifReports_ExposeCanonicalDiagnosticsWithoutChangingPassState()
    {
        ValidationOutcome outcome = MissingCatalogIdentityOutcome;
        string human = new HumanReportRenderer(new CliRuntime()).Render(true, [("strict", outcome)]);
        string sarif = new StructuredReportRenderer(new CliRuntime()).Render("sarif", true, [("strict", outcome)]);
        using JsonDocument json = JsonDocument.Parse(sarif);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Passed, Is.True);
            Assert.That(human, Does.Contain("MissingCatalogIdentity"));
            Assert.That(human, Does.Contain("Classification findings:"));
            Assert.That(sarif, Does.Contain("canonical-annotation:MissingCatalogIdentity"));
            Assert.That(json.RootElement.GetProperty("runs")[0].GetProperty("results")
                .EnumerateArray().Any(result => result.GetProperty("ruleId").GetString()
                    == "canonical-annotation:MissingCatalogIdentity"), Is.True);
        });
    }

    [Test]
    public void JsonAndSarifReports_ProjectContextConflictAndInvalidValueDiagnostics()
    {
        var renderer = new StructuredReportRenderer(new CliRuntime());
        string jsonReport = renderer.Render("json", true, [("strict", ContextDiagnosticsOutcome)]);
        string sarifReport = renderer.Render("sarif", true, [("strict", ContextDiagnosticsOutcome)]);
        using JsonDocument json = JsonDocument.Parse(jsonReport);
        using JsonDocument sarif = JsonDocument.Parse(sarifReport);

        string[] jsonCodes = json.RootElement.GetProperty("canonical_annotation_diagnostics")
            .EnumerateArray().Select(diagnostic => diagnostic.GetProperty("code").GetString()!).ToArray();
        string[] sarifRuleIds = sarif.RootElement.GetProperty("runs")[0].GetProperty("results")
            .EnumerateArray().Select(result => result.GetProperty("ruleId").GetString()!).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(jsonCodes, Is.EquivalentTo(new[] { "CanonicalContextConflict", "InvalidCanonicalMetadata" }));
            Assert.That(sarifRuleIds, Is.EquivalentTo(new[]
            {
                "canonical-annotation:CanonicalContextConflict",
                "canonical-annotation:InvalidCanonicalMetadata"
            }));
        });
    }

    [Test]
    public void CoverageReport_ListsCanonicalDiagnosticsAsInformationalEvidence()
    {
        string validationJson = new StructuredReportRenderer(new CliRuntime())
            .Render("json", true, [("strict", MissingCatalogIdentityOutcome)]);
        using JsonDocument json = JsonDocument.Parse(validationJson);
        string coverage = CoverageReportRenderer.Render(json.RootElement, null, "/repo", diffFailed: false, maxFailures: null);

        Assert.Multiple(() =>
        {
            Assert.That(coverage, Does.Contain("**Status:** ✅ pass"));
            Assert.That(coverage, Does.Contain("### Canonical annotation diagnostics (1)"));
            Assert.That(coverage, Does.Contain("MissingCatalogIdentity"));
            Assert.That(coverage, Does.Contain("ArchLinterNet.Annotations.CommandHandlerAttribute"));
        });
    }
}
