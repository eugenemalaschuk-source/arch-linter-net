using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class PublicApiCommandHandlerTests
{
    [Test]
    public void Capture_PreflightBlockedWithJson_WritesOneStructuredErrorDocument()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(
                false, null, 0, null,
                [
                    new BuildStatePreflightDiagnostic(
                        "Acme.Module", null, BuildStatePreflightState.MissingArtifact,
                        new BuildStatePreflightEvidence(
                            "Acme.Module.csproj", "Acme.Module", "Release", "Debug", "net10.0", "net9.0",
                            "bin/Release/net10.0/Acme.Module.dll", ["bin/Release/net10.0/Acme.Module.dll"],
                            "dotnet build Acme.Module.csproj", "Receipt is stale")),
                ],
                "Build state preflight is blocked"),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", false, false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Is.Empty);
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("category").GetString(),
                Is.EqualTo("build-state-preflight-failed"));
            JsonElement diagnostic = document.RootElement.GetProperty("error").GetProperty("details").GetProperty("diagnostics")[0];
            Assert.That(diagnostic.GetProperty("contract_name").GetString(), Is.EqualTo("Acme.Module"));
            Assert.That(diagnostic.GetProperty("state").GetString(), Is.EqualTo("missing-artifact"));
            Assert.That(diagnostic.GetProperty("project_path").GetString(), Is.EqualTo("Acme.Module.csproj"));
            Assert.That(diagnostic.GetProperty("requested_target_framework").GetString(), Is.EqualTo("net10.0"));
            Assert.That(diagnostic.GetProperty("build_command").GetString(), Is.EqualTo("dotnet build Acme.Module.csproj"));
        });
    }

    [Test]
    public void Migrate_DriftRefusalWithJson_PreservesTypedFailureAndEvidence()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                false, null, ["class Acme.Gone"], ["class Acme.New"], SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>(), "has 1 stale inline declaration(s)", PublicApiFailureKind.Drift),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", false, false, false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
        Assert.That(console.ErrorText, Is.Empty);
        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        JsonElement error = document.RootElement.GetProperty("error");
        Assert.That(error.GetProperty("category").GetString(), Is.EqualTo("public-api-drift"));
        JsonElement details = error.GetProperty("details");
        Assert.That(details.GetProperty("failure_kind").GetString(), Is.EqualTo("drift"));
        Assert.That(details.GetProperty("stale_declarations")[0].GetString(), Is.EqualTo("class Acme.Gone"));
        Assert.That(details.GetProperty("undeclared_surface")[0].GetString(), Is.EqualTo("class Acme.New"));
    }
}
