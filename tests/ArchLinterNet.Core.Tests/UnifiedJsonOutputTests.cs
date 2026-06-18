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
    [Test]
    public void FormatResultForCiArtifacts_Passed_ReturnsValidJsonWithPassedTrue()
    {
        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
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
            new("test-contract", "MyApp.Web.Foo", "MyApp.Core", new[] { "ref1", "ref2" })
        };
        var cycles = new[] { "A -> B -> A" };

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
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
            new("c1", "src", "ns", new[] { "ref" })
        };
        var cycles = new[] { "X -> Y -> X" };

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict", false, violations, cycles);

        Assert.That(json.StartsWith("{"), Is.True);
        Assert.That(json.TrimEnd().EndsWith("}"), Is.True);

        int openBraces = json.Count(c => c == '{');
        int closeBraces = json.Count(c => c == '}');
        Assert.That(openBraces, Is.EqualTo(closeBraces));
    }
}
