using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BuildStatePreflightFormatterTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    [Test]
    public void FormatBuildStatePreflightForHumans_NoDiagnostics_ReturnsEmpty()
    {
        Assert.That(_formatter.FormatBuildStatePreflightForHumans(Array.Empty<BuildStatePreflightDiagnostic>()), Is.Empty);
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_MissingArtifact_IncludesBuildCommand()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "src/Fixture/Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence(
                "src/Fixture/Fixture.csproj", "Fixture", BuildCommand: "dotnet build \"src/Fixture/Fixture.csproj\""));

        string result = _formatter.FormatBuildStatePreflightForHumans(new[] { diagnostic });

        Assert.That(result, Does.Contain("Build-state preflight:"));
        Assert.That(result, Does.Contain("[missing-artifact] Fixture (src/Fixture/Fixture.csproj)"));
        Assert.That(result, Does.Contain("build command: dotnet build \"src/Fixture/Fixture.csproj\""));
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_WrongConfiguration_IncludesRequestedAndObserved()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.WrongConfiguration,
            new BuildStatePreflightEvidence(
                "Fixture.csproj", "Fixture", RequestedConfiguration: "Release", ObservedConfiguration: "Debug"));

        string result = _formatter.FormatBuildStatePreflightForHumans(new[] { diagnostic });

        Assert.That(result, Does.Contain("requested configuration: Release, observed: Debug"));
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_WrongTargetFramework_IncludesRequestedAndObserved()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.WrongTargetFramework,
            new BuildStatePreflightEvidence(
                "Fixture.csproj", "Fixture", RequestedTargetFramework: "net10.0", ObservedTargetFramework: "net8.0"));

        string result = _formatter.FormatBuildStatePreflightForHumans(new[] { diagnostic });

        Assert.That(result, Does.Contain("requested target framework: net10.0, observed: net8.0"));
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_WithDetail_IncludesDetailText()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.UnverifiableArtifact,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture", Detail: "No ArchLinterNet build receipt was found."));

        string result = _formatter.FormatBuildStatePreflightForHumans(new[] { diagnostic });

        Assert.That(result, Does.Contain("No ArchLinterNet build receipt was found."));
    }

    [Test]
    public void FormatBuildStatePreflightForHumans_MultipleDiagnostics_OrdersByProjectPath()
    {
        var b = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "b.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("b.csproj", "B"));
        var a = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "a.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("a.csproj", "A"));

        string result = _formatter.FormatBuildStatePreflightForHumans(new[] { b, a });

        Assert.That(result.IndexOf("A (a.csproj)", StringComparison.Ordinal),
            Is.LessThan(result.IndexOf("B (b.csproj)", StringComparison.Ordinal)));
    }

    [Test]
    public void FormatResultForCiArtifacts_WithPreflightDiagnostics_SerializesPreflightSection()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.StaleArtifact,
            new BuildStatePreflightEvidence(
                "Fixture.csproj", "Fixture", ExpectedOutputPath: "bin/Debug/net10.0/Fixture.dll",
                Detail: "Selected source content changed."));

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureClassificationRoleFact>(), null, new[] { diagnostic });

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement preflight = document.RootElement.GetProperty("preflight_diagnostics");

        Assert.That(preflight.GetArrayLength(), Is.EqualTo(1));
        JsonElement entry = preflight[0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.GetProperty("state").GetString(), Is.EqualTo("stale-artifact"));
            Assert.That(entry.GetProperty("project_path").GetString(), Is.EqualTo("Fixture.csproj"));
            Assert.That(entry.GetProperty("assembly_name").GetString(), Is.EqualTo("Fixture"));
            Assert.That(entry.GetProperty("expected_output_path").GetString(), Is.EqualTo("bin/Debug/net10.0/Fixture.dll"));
            Assert.That(entry.GetProperty("detail").GetString(), Is.EqualTo("Selected source content changed."));
        });
    }

    [Test]
    public void FormatResultForCiArtifacts_NoPreflightDiagnostics_SerializesEmptyArray()
    {
        string json = _formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<ArchitectureClassificationRoleFact>(), null, Array.Empty<BuildStatePreflightDiagnostic>());

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("preflight_diagnostics").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultAsSarif_BlockingPreflightDiagnostic_IncludesRuleAndMessage()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence(
                "Fixture.csproj", "Fixture", BuildCommand: "dotnet build \"Fixture.csproj\""));

        ArchitectureSarifFormatter sarifFormatter = new();
        string sarif = sarifFormatter.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), new[] { diagnostic }, "1.0.0");

        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        JsonElement result = results[0];
        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("build-state-preflight/missing-artifact"));
            Assert.That(result.GetProperty("level").GetString(), Is.EqualTo("error"));
            Assert.That(result.GetProperty("message").GetProperty("text").GetString(), Does.Contain("Fixture"));
            Assert.That(result.GetProperty("message").GetProperty("text").GetString(), Does.Contain("Fixture.csproj"));
        });

        JsonElement rules = document.RootElement.GetProperty("runs")[0].GetProperty("tool")
            .GetProperty("driver").GetProperty("rules");
        Assert.That(rules.EnumerateArray().Any(r => r.GetProperty("id").GetString() == "build-state-preflight/missing-artifact"));
    }

    [Test]
    public void FormatResultAsSarif_CurrentStateDiagnostic_IsExcludedFromResults()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.Current,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture"));

        ArchitectureSarifFormatter sarifFormatter = new();
        string sarif = sarifFormatter.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), new[] { diagnostic }, "1.0.0");

        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.That(results.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultAsSarif_NoPreflightDiagnostics_ProducesEmptyResults()
    {
        ArchitectureSarifFormatter sarifFormatter = new();
        string sarif = sarifFormatter.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(), "1.0.0");

        using JsonDocument document = JsonDocument.Parse(sarif);
        Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultAsSarif_StaticCycleFindingOverload_IncludesPreflightDiagnostics()
    {
        var diagnostic = new BuildStatePreflightDiagnostic(
            "build-state-preflight", "Fixture.csproj", BuildStatePreflightState.BuildFailed,
            new BuildStatePreflightEvidence("Fixture.csproj", "Fixture", Detail: "`dotnet build` failed with exit code 1"));

        string sarif = ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), Array.Empty<ArchitectureCycleFinding>(),
            new[] { diagnostic }, "1.0.0");

        using JsonDocument document = JsonDocument.Parse(sarif);
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That(results[0].GetProperty("ruleId").GetString(), Is.EqualTo("build-state-preflight/build-failed"));
    }
}
