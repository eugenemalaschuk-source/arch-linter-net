using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public partial class CliIntegrationTests
{
    private static string ImportedProvenancePolicy => Path.Combine(
        _repoRoot,
        "tests",
        "ArchLinterNet.Cli.Tests",
        "TestPolicies",
        "imported-provenance-root.yml");

    [Test]
    public void ImportedPolicy_HumanDiagnostic_IncludesPortableFragmentAndRootContext()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "--policy", ImportedProvenancePolicy, "--format", "human", "--strict");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
            Assert.That(stdout, Does.Contain(
                "policy: imported-provenance-fragment.yml:" +
                "analysis.target_assemblies[0]"));
            Assert.That(stdout, Does.Contain("root: imported-provenance-root.yml"));
        });
    }

    [Test]
    public void ImportedPolicy_JsonDiagnostic_IncludesStableStructuredPolicyLocation()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "--policy", ImportedProvenancePolicy, "--format", "json", "--strict");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement location = document.RootElement.GetProperty("violations")[0].GetProperty("policy_location");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
            Assert.That(location.GetProperty("source_path").GetString(),
                Is.EqualTo("imported-provenance-fragment.yml"));
            Assert.That(location.GetProperty("root_path").GetString(),
                Is.EqualTo("imported-provenance-root.yml"));
            Assert.That(location.GetProperty("role").GetString(), Is.EqualTo("fragment"));
            Assert.That(location.GetProperty("yaml_path").GetString(), Is.EqualTo("analysis.target_assemblies[0]"));
        });
    }

    [Test]
    public void ImportedPolicy_SarifDiagnostic_PreservesLogicalAndPolicyLocations()
    {
        var (exitCode, stdout, stderr) = RunCli(
            "--policy", ImportedProvenancePolicy, "--format", "sarif", "--strict");

        using JsonDocument document = JsonDocument.Parse(stdout);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement relatedLocation = result.GetProperty("relatedLocations")[0];

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(stderr, Is.Empty);
            Assert.That(result.TryGetProperty("logicalLocations", out _), Is.True);
            Assert.That(relatedLocation.GetProperty("physicalLocation")
                .GetProperty("artifactLocation").GetProperty("uri").GetString(),
                Is.EqualTo("imported-provenance-fragment.yml"));
            Assert.That(relatedLocation.GetProperty("message").GetProperty("text").GetString(), Does.Contain(
                "analysis.target_assemblies[0]"));
        });
    }
}
