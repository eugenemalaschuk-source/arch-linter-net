using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureDiagnosticFormatterTests
{
    private static readonly string[] _frameworkDependencyReferences = ["Microsoft.AspNetCore.App"];
    private static readonly string[] _frameworkAllowOnlyReferences = ["Microsoft.WindowsDesktop.App"];
    private static readonly string[] _frameworkAllowOnlyGroups = ["approved_core"];

    [Test]
    public void FormatViolationsForHumans_FrameworkReferenceDiagnostic_IncludesSourceAndReferences()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-aspnet", "domain-no-aspnet-id", "MyApp.Domain", "framework group 'forbidden_web'",
                _frameworkDependencyReferences)
            {
                Payload = new FrameworkReferencePayload("forbidden_web")
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);

        Assert.That(human, Does.Contain("MyApp.Domain"));
        Assert.That(human, Does.Contain("framework group 'forbidden_web'"));
        Assert.That(human, Does.Contain("Microsoft.AspNetCore.App"));
    }

    [Test]
    public void FormatResultForCiArtifacts_FrameworkReferenceDiagnostic_IncludesSourceAndForbiddenFrameworkGroup()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-aspnet", "domain-no-aspnet-id", "MyApp.Domain", "framework group 'forbidden_web'",
                _frameworkDependencyReferences)
            {
                Payload = new FrameworkReferencePayload("forbidden_web")
            }
        };

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement violation = document.RootElement.GetProperty("violations")[0];

        Assert.That(violation.GetProperty("source").GetString(), Is.EqualTo("MyApp.Domain"));
        Assert.That(violation.GetProperty("forbidden_namespace").GetString(), Is.EqualTo("framework group 'forbidden_web'"));
        Assert.That(
            violation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_frameworkDependencyReferences));
        Assert.That(violation.GetProperty("forbidden_framework_group").GetString(), Is.EqualTo("forbidden_web"));
    }

    [Test]
    public void FormatViolationsForHumans_FrameworkReferenceAllowOnlyDiagnostic_IncludesSourceAndReferences()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed framework groups",
                _frameworkAllowOnlyReferences)
            {
                Payload = new FrameworkReferenceAllowOnlyPayload(_frameworkAllowOnlyGroups)
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);

        Assert.That(human, Does.Contain("MyApp.Domain"));
        Assert.That(human, Does.Contain("outside allowed framework groups"));
        Assert.That(human, Does.Contain("Microsoft.WindowsDesktop.App"));
    }

    [Test]
    public void FormatResultForCiArtifacts_FrameworkReferenceAllowOnlyDiagnostic_IncludesSourceAndAllowedFrameworkGroups()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed framework groups",
                _frameworkAllowOnlyReferences)
            {
                Payload = new FrameworkReferenceAllowOnlyPayload(_frameworkAllowOnlyGroups)
            }
        };

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement violation = document.RootElement.GetProperty("violations")[0];

        Assert.That(violation.GetProperty("source").GetString(), Is.EqualTo("MyApp.Domain"));
        Assert.That(
            violation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_frameworkAllowOnlyReferences));
        Assert.That(
            violation.GetProperty("allowed_framework_groups").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_frameworkAllowOnlyGroups));
    }

    [Test]
    public void FrameworkReferenceAllowOnlyDiagnostic_HasDistinctKind()
    {
        var violation = new ArchitectureViolation(
            "domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed framework groups",
            _frameworkAllowOnlyReferences)
        {
            Payload = new FrameworkReferenceAllowOnlyPayload(_frameworkAllowOnlyGroups)
        };

        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.FrameworkReferenceAllowOnly));
        Assert.That(diagnostic, Is.InstanceOf<FrameworkReferenceAllowOnlyDiagnostic>());
    }

    [Test]
    public void FrameworkReferenceDiagnostic_HasDistinctKind()
    {
        var violation = new ArchitectureViolation(
            "domain-no-aspnet", "domain-no-aspnet-id", "MyApp.Domain", "framework group 'forbidden_web'",
            _frameworkDependencyReferences)
        {
            Payload = new FrameworkReferencePayload("forbidden_web")
        };

        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.FrameworkReference));
        Assert.That(diagnostic, Is.InstanceOf<FrameworkReferenceDiagnostic>());
    }

    [Test]
    public void FrameworkReferenceViolation_HumanJsonAndSarif_ReportEquivalentEvidence()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-aspnet", "domain-no-aspnet-id", "MyApp.Domain", "framework group 'forbidden_web'",
                _frameworkDependencyReferences)
            {
                Payload = new FrameworkReferencePayload("forbidden_web")
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);

        using JsonDocument jsonDocument = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement jsonViolation = jsonDocument.RootElement.GetProperty("violations")[0];

        var sarifFormatter = new ArchitectureSarifFormatter();
        using JsonDocument sarifDocument = JsonDocument.Parse(
            sarifFormatter.FormatResultAsSarif("strict", violations, Array.Empty<string>(), "1.0.0"));
        JsonElement sarifResult = sarifDocument.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.That(human, Does.Contain("MyApp.Domain"));
        Assert.That(jsonViolation.GetProperty("source").GetString(), Is.EqualTo("MyApp.Domain"));
        Assert.That(
            sarifResult.GetProperty("logicalLocations")[0].GetProperty("fullyQualifiedName").GetString(),
            Is.EqualTo("MyApp.Domain"));

        Assert.That(human, Does.Contain("Microsoft.AspNetCore.App"));
        Assert.That(
            jsonViolation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_frameworkDependencyReferences));
        string sarifMessage = sarifResult.GetProperty("message").GetProperty("text").GetString()!;
        Assert.That(sarifMessage, Does.Contain("Microsoft.AspNetCore.App"));
    }

    [Test]
    public void FrameworkReferenceViolation_WithEvidence_HumanJsonAndSarifRenderStructuredFields()
    {
        var evidence = new[]
        {
            new FrameworkReferenceEvidence("Microsoft.AspNetCore.App", "net10.0", true, "/src/MyApp.Domain/MyApp.Domain.csproj"),
        };
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-aspnet", "domain-no-aspnet-id", "MyApp.Domain", "framework group 'forbidden_web'",
                _frameworkDependencyReferences)
            {
                Payload = new FrameworkReferencePayload("forbidden_web", evidence)
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);
        Assert.That(human, Does.Contain("net10.0"));
        Assert.That(human, Does.Contain("explicit"));
        Assert.That(human, Does.Contain("/src/MyApp.Domain/MyApp.Domain.csproj"));

        using JsonDocument jsonDocument = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement jsonEvidence = jsonDocument.RootElement.GetProperty("violations")[0].GetProperty("evidence")[0];
        Assert.That(jsonEvidence.GetProperty("framework_name").GetString(), Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(jsonEvidence.GetProperty("target_framework").GetString(), Is.EqualTo("net10.0"));
        Assert.That(jsonEvidence.GetProperty("explicit").GetBoolean(), Is.True);
        Assert.That(jsonEvidence.GetProperty("source_path").GetString(), Is.EqualTo("/src/MyApp.Domain/MyApp.Domain.csproj"));

        var sarifFormatter = new ArchitectureSarifFormatter();
        using JsonDocument sarifDocument = JsonDocument.Parse(
            sarifFormatter.FormatResultAsSarif("strict", violations, Array.Empty<string>(), "1.0.0"));
        JsonElement sarifResult = sarifDocument.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement sarifEvidence = sarifResult.GetProperty("properties").GetProperty("evidence")[0];
        Assert.That(sarifEvidence.GetProperty("framework_name").GetString(), Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(sarifEvidence.GetProperty("target_framework").GetString(), Is.EqualTo("net10.0"));
        Assert.That(sarifEvidence.GetProperty("explicit").GetBoolean(), Is.True);
        Assert.That(sarifEvidence.GetProperty("source_path").GetString(), Is.EqualTo("/src/MyApp.Domain/MyApp.Domain.csproj"));
        Assert.That(
            sarifResult.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString(),
            Is.EqualTo("/src/MyApp.Domain/MyApp.Domain.csproj"),
            "SourcePath must also be mapped to a SARIF physical location, not only properties.");
    }

    [Test]
    public void FrameworkReferenceAllowOnlyViolation_WithImplicitEvidence_HumanOutputShowsImplicitClassification()
    {
        var evidence = new[]
        {
            new FrameworkReferenceEvidence("Microsoft.NETCore.App", "net10.0", false, "/src/MyApp.Domain/MyApp.Domain.csproj"),
        };
        var violations = new List<ArchitectureViolation>
        {
            new("domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed framework groups",
                _frameworkAllowOnlyReferences)
            {
                Payload = new FrameworkReferenceAllowOnlyPayload(_frameworkAllowOnlyGroups, evidence)
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);
        Assert.That(human, Does.Contain("implicit"));

        using JsonDocument jsonDocument = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement jsonEvidence = jsonDocument.RootElement.GetProperty("violations")[0].GetProperty("evidence")[0];
        Assert.That(jsonEvidence.GetProperty("explicit").GetBoolean(), Is.False);
    }
}
