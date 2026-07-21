using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureDiagnosticFormatterTests
{
    private static readonly string[] _packageDependencyReferences = ["Microsoft.EntityFrameworkCore@8.0.0"];
    private static readonly string[] _packageAllowOnlyReferences = ["Acme.Sdk@1.2.3"];
    private static readonly string[] _packageAllowOnlyGroups = ["approved_infra"];

    [Test]
    public void FormatViolationsForHumans_PackageDependencyDiagnostic_IncludesSourceAndReferences()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-ef", "domain-no-ef-id", "MyApp.Domain", "package group 'forbidden_infra'",
                _packageDependencyReferences)
            {
                Payload = new PackageDependencyPayload("forbidden_infra")
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);

        Assert.That(human, Does.Contain("MyApp.Domain"));
        Assert.That(human, Does.Contain("package group 'forbidden_infra'"));
        Assert.That(human, Does.Contain("Microsoft.EntityFrameworkCore@8.0.0"));
    }

    [Test]
    public void FormatResultForCiArtifacts_PackageDependencyDiagnostic_IncludesSourceAndForbiddenPackageGroup()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-ef", "domain-no-ef-id", "MyApp.Domain", "package group 'forbidden_infra'",
                _packageDependencyReferences)
            {
                Payload = new PackageDependencyPayload("forbidden_infra")
            }
        };

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement violation = document.RootElement.GetProperty("violations")[0];

        Assert.That(violation.GetProperty("source").GetString(), Is.EqualTo("MyApp.Domain"));
        Assert.That(violation.GetProperty("forbidden_namespace").GetString(), Is.EqualTo("package group 'forbidden_infra'"));
        Assert.That(
            violation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_packageDependencyReferences));
        Assert.That(violation.GetProperty("forbidden_package_group").GetString(), Is.EqualTo("forbidden_infra"));
    }

    [Test]
    public void FormatViolationsForHumans_PackageAllowOnlyDiagnostic_IncludesSourceAndReferences()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed package groups",
                _packageAllowOnlyReferences)
            {
                Payload = new PackageAllowOnlyPayload(_packageAllowOnlyGroups)
            }
        };

        string human = _formatter.FormatViolationsForHumans(violations);

        Assert.That(human, Does.Contain("MyApp.Domain"));
        Assert.That(human, Does.Contain("outside allowed package groups"));
        Assert.That(human, Does.Contain("Acme.Sdk@1.2.3"));
    }

    [Test]
    public void FormatResultForCiArtifacts_PackageAllowOnlyDiagnostic_IncludesSourceAndAllowedPackageGroups()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed package groups",
                _packageAllowOnlyReferences)
            {
                Payload = new PackageAllowOnlyPayload(_packageAllowOnlyGroups)
            }
        };

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement violation = document.RootElement.GetProperty("violations")[0];

        Assert.That(violation.GetProperty("source").GetString(), Is.EqualTo("MyApp.Domain"));
        Assert.That(
            violation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_packageAllowOnlyReferences));
        Assert.That(
            violation.GetProperty("allowed_package_groups").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_packageAllowOnlyGroups));
    }

    [Test]
    public void PackageAllowOnlyDiagnostic_HasDistinctKind()
    {
        var violation = new ArchitectureViolation(
            "domain-approved-only", "domain-approved-only-id", "MyApp.Domain", "outside allowed package groups",
            _packageAllowOnlyReferences)
        {
            Payload = new PackageAllowOnlyPayload(_packageAllowOnlyGroups)
        };

        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.PackageAllowOnly));
        Assert.That(diagnostic, Is.InstanceOf<PackageAllowOnlyDiagnostic>());
    }

    [Test]
    public void PackageDependencyViolation_HumanJsonAndSarif_ReportEquivalentEvidence()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("domain-no-ef", "domain-no-ef-id", "MyApp.Domain", "package group 'forbidden_infra'",
                _packageDependencyReferences)
            {
                Payload = new PackageDependencyPayload("forbidden_infra")
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

        Assert.That(human, Does.Contain("Microsoft.EntityFrameworkCore@8.0.0"));
        Assert.That(
            jsonViolation.GetProperty("forbidden_references").EnumerateArray().Select(e => e.GetString()),
            Is.EquivalentTo(_packageDependencyReferences));
        string sarifMessage = sarifResult.GetProperty("message").GetProperty("text").GetString()!;
        Assert.That(sarifMessage, Does.Contain("Microsoft.EntityFrameworkCore@8.0.0"));
    }
}
