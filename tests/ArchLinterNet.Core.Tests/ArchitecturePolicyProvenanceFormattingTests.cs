using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyProvenanceFormattingTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();
    private static readonly ArchitectureSarifFormatter _sarifFormatter = new();

    [Test]
    public void HumanOutput_FragmentViolation_AppendsPortablePolicyAndRootContext()
    {
        ArchitectureViolation violation = ViolationWithPolicyLocation();

        string output = _formatter.FormatViolationsForHumans(new[] { violation });

        Assert.That(output, Does.Contain(
            "policy: architecture/policy/domain.yml:contracts.strict[0]; root: architecture/root.yml"));
        Assert.That(output, Does.Contain("[no-domain] [no domain] App.Application.Service -> App.Domain"));
    }

    [Test]
    public void CiJson_FragmentViolation_AddsStructuredPolicyLocation()
    {
        ArchitectureViolation violation = ViolationWithPolicyLocation();

        string json = _formatter.FormatViolationsForCiArtifacts("no domain", "no-domain", new[] { violation });
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement entry = document.RootElement.GetProperty("violations")[0];
        JsonElement location = entry.GetProperty("policy_location");

        Assert.Multiple(() =>
        {
            Assert.That(location.GetProperty("root_path").GetString(), Is.EqualTo("architecture/root.yml"));
            Assert.That(location.GetProperty("source_path").GetString(),
                Is.EqualTo("architecture/policy/domain.yml"));
            Assert.That(location.GetProperty("role").GetString(), Is.EqualTo("fragment"));
            Assert.That(location.GetProperty("yaml_path").GetString(), Is.EqualTo("contracts.strict[0]"));
            Assert.That(location.GetProperty("source_ordinal").GetInt32(), Is.EqualTo(1));
            Assert.That(location.GetProperty("contract_family").GetString(), Is.EqualTo("dependency"));
            Assert.That(location.GetProperty("contract_id").GetString(), Is.EqualTo("no-domain"));
            Assert.That(entry.GetProperty("source").GetString(), Is.EqualTo("App.Application.Service"));
        });
    }

    [Test]
    public void CiJson_ViolationWithoutProvenance_PreservesExistingShape()
    {
        var violation = new ArchitectureViolation(
            "no domain", "no-domain", "App.Application.Service", "App.Domain", new[] { "App.Domain.Entity" });

        string json = _formatter.FormatViolationsForCiArtifacts("no domain", "no-domain", new[] { violation });
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement entry = document.RootElement.GetProperty("violations")[0];

        Assert.That(entry.TryGetProperty("policy_location", out _), Is.False);
        Assert.That(entry.GetProperty("forbidden_namespace").GetString(), Is.EqualTo("App.Domain"));
    }

    [Test]
    public void Sarif_FragmentViolation_AddsRelatedPolicyLocationWithoutReplacingLogicalLocation()
    {
        ArchitectureViolation violation = ViolationWithPolicyLocation();

        string json = _sarifFormatter.FormatResultAsSarif(
            "strict", new[] { violation }, Array.Empty<string>(), "1.0.0");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement related = result.GetProperty("relatedLocations")[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("logicalLocations")[0].GetProperty("fullyQualifiedName").GetString(),
                Is.EqualTo("App.Application.Service"));
            Assert.That(related.GetProperty("physicalLocation").GetProperty("artifactLocation")
                    .GetProperty("uri").GetString(),
                Is.EqualTo("architecture/policy/domain.yml"));
            Assert.That(related.GetProperty("physicalLocation").GetProperty("region")
                .GetProperty("startLine").GetInt32(), Is.EqualTo(12));
            Assert.That(related.GetProperty("message").GetProperty("text").GetString(),
                Does.Contain("contracts.strict[0]"));
        });
    }

    private static ArchitectureViolation ViolationWithPolicyLocation()
    {
        var source = new ArchitecturePolicySourceDescriptor(
            "architecture/root.yml",
            "architecture/policy/domain.yml",
            ArchitecturePolicyDocumentRole.Fragment,
            1,
            "architecture/root.yml",
            "policy/domain.yml",
            new[] { "architecture/root.yml", "architecture/policy/domain.yml" });
        var location = new ArchitecturePolicySourceLocation(
            source,
            "contracts.strict[0]",
            12,
            5,
            "dependency",
            "no-domain");

        return new ArchitectureViolation(
            "no domain",
            "no-domain",
            "App.Application.Service",
            "App.Domain",
            new[] { "App.Domain.Entity" })
        {
            PolicyLocation = location
        };
    }
}
