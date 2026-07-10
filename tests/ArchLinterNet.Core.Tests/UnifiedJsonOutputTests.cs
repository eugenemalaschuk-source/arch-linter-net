using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class UnifiedJsonOutputTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();
    private static readonly string[] Ref = { "ref" };
    private static readonly string[] Ref1 = { "ref1" };
    private static readonly string[] UnityEngineVector3 = { "UnityEngine.Vector3" };

    [Test]
    public void FormatResultForCiArtifacts_Passed_ReturnsValidJsonWithPassedTrue()
    {
        string json = _formatter.FormatResultForCiArtifacts(
            "strict", passed: true,
            Array.Empty<ArchitectureViolation>(), Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("passed").GetBoolean(), Is.True);
        Assert.That(doc.RootElement.GetProperty("mode").GetString(), Is.EqualTo("strict"));
        Assert.That(doc.RootElement.GetProperty("violations").GetArrayLength(), Is.EqualTo(0));
        Assert.That(doc.RootElement.GetProperty("cycles").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultForCiArtifacts_Failed_ContainsViolationsAndCycles()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("test-contract", null, "MyApp.Web.Foo", "MyApp.Core", new[] { "ref1", "ref2" })
        };
        var cycles = new[] { "A -> B -> A" };

        string json = _formatter.FormatResultForCiArtifacts(
            "audit", passed: false, violations, cycles);

        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("passed").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.GetProperty("mode").GetString(), Is.EqualTo("audit"));
        Assert.That(doc.RootElement.GetProperty("violations").GetArrayLength(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("cycles").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public void FormatResultForCiArtifacts_IsSingleJsonObject()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("c1", null, "src", "ns", Ref)
        };
        var cycles = new[] { "X -> Y -> X" };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, cycles);

        Assert.That(json.StartsWith('{'), Is.True);
        Assert.That(json.TrimEnd().EndsWith('}'), Is.True);

        int openBraces = json.Count(c => c == '{');
        int closeBraces = json.Count(c => c == '}');
        Assert.That(openBraces, Is.EqualTo(closeBraces));
    }

    [Test]
    public void FormatResultForCiArtifacts_IncludesContractId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("test-contract", "my-rule", "MyApp.Web.Foo", "MyApp.Core", Ref1)
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("contract_id").GetString(), Is.EqualTo("my-rule"));
        Assert.That(violation.GetProperty("contract").GetString(), Is.EqualTo("test-contract"));
    }

    [Test]
    public void FormatResultForCiArtifacts_NullContractId_Excluded()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("test-contract", null, "src", "ns", Ref)
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "audit", true, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("contract_id").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void FormatViolationsForHumans_IncludesContractId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-rule", "MyApp.Web.Foo", "MyApp.Core", Ref1)
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("[my-rule]"));
        Assert.That(output, Does.Contain("[My Contract]"));
    }

    [Test]
    public void FormatCyclesWithContractId_HumanOutputIncludesId()
    {
        var cycles = new[] { "[cycle-check] A -> B -> A", "[no-cycles] X -> Y -> X" };

        string output = _formatter.FormatCyclesForHumans(cycles);

        Assert.That(output, Does.Contain("[cycle-check]"));
        Assert.That(output, Does.Contain("[no-cycles]"));
        Assert.That(output, Does.Contain("A -> B -> A"));
    }

    [Test]
    public void FormatViolationsForHumans_FallbackId_ShowsNormalizedId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-contract", "src", "ns", Ref)
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("[my-contract]"));
    }

    [Test]
    public void FormatResultForCiArtifacts_ExternalViolation_IncludesExternalGroup()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("core-no-unity", "core-no-unity", "MyApp.Core.PlayerModel", "external dependency group 'unity_runtime'",
                UnityEngineVector3)
            {
                Payload = new ExternalDependencyPayload("unity_runtime")
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("forbidden_external_group").GetString(), Is.EqualTo("unity_runtime"));
    }

    [Test]
    public void FormatViolationsForHumans_ExternalViolation_IncludesExternalGroup()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("core-no-unity", "core-no-unity", "MyApp.Core.PlayerModel", "external dependency group 'unity_runtime'",
                UnityEngineVector3)
            {
                Payload = new ExternalDependencyPayload("unity_runtime")
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("external_group: unity_runtime"));
        Assert.That(output, Does.Contain("UnityEngine.Vector3"));
    }
}
