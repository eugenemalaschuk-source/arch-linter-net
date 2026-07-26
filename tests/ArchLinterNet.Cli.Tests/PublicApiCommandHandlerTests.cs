using System.Text.Json;
using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Commands.PublicApi;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class PublicApiCommandHandlerTests
{
    private const string PolicyPath = "architecture/dependencies.arch.yml";
    private const string SnapshotPath = "architecture/api/module-api.txt";
    private const string ContractId = "module-api";
    private const string CapturedSnapshot = "@format arch-linter-net/public-api-snapshot\n@version 1\n";

    private static PublicApiDelta DriftDelta()
    {
        return new PublicApiDelta(
            new[] { new PublicApiDeltaEntry(PublicApiDeltaKind.Added, "Acme", "class Acme.New", null) },
            new[] { new PublicApiDeltaEntry(PublicApiDeltaKind.Removed, "Acme", "class Acme.Gone", "class Acme.Gone") },
            new[]
            {
                new PublicApiDeltaEntry(
                    PublicApiDeltaKind.Changed, "Acme",
                    "method Acme.Thing.Do(): System.Boolean", "method Acme.Thing.Do(): System.Void"),
            });
    }

    [Test]
    public void Capture_WritesSnapshotWhenTargetDoesNotExist()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(true, CapturedSnapshot, 12, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo(SnapshotPath));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
            Assert.That(console.OutputText, Does.Contain("Captured 12 public API entries."));
        });
    }

    [Test]
    public void Capture_RefusesToOverwriteDifferingSnapshotWithoutForce()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = "different" };
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(true, CapturedSnapshot, 12, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("already exists and differs"));
        });
    }

    [Test]
    public void Capture_ForceReplacesDifferingSnapshot()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = "different" };
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(true, CapturedSnapshot, 12, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", true, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
        });
    }

    [Test]
    public void Capture_IdenticalSnapshotSucceedsWithoutWriting()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = CapturedSnapshot };
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(true, CapturedSnapshot, 12, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.OutputText, Does.Contain("already current"));
        });
    }

    [Test]
    public void Capture_MissingContractOption_FailsWithExitCodeTwo()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiCaptureCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, null, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--contract is required"));
        });
    }

    [Test]
    public void Capture_UnsupportedFormat_FailsWithExitCodeTwo()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiCaptureCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "xml", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Invalid format for public-api capture: xml"));
        });
    }

    [Test]
    public void Capture_PreflightBlocked_ReportsDiagnosticsAndDoesNotWrite()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(
                false, null, 0, null,
                new[]
                {
                    new BuildStatePreflightDiagnostic(
                        "Acme.Module", null, BuildStatePreflightState.MissingArtifact,
                        new BuildStatePreflightEvidence("Acme.Module.csproj", "Acme.Module")),
                },
                "Build state preflight is blocked"),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("preflight is blocked"));
            Assert.That(console.ErrorText, Does.Contain("MissingArtifact"));
        });
    }

    [Test]
    public void Diff_InSyncSnapshotReturnsSuccess()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            DiffOutcome = new PublicApiDiffOutcome(
                true, true, PublicApiDelta.Empty, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText, Does.Contain("in sync"));
        });
    }

    [Test]
    public void Diff_DriftReturnsValidationFailureAndSeparatesDeltas()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            DiffOutcome = new PublicApiDiffOutcome(
                true, false, DriftDelta(), SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(console.OutputText, Does.Contain("added: 1, removed: 1, changed: 1"));
        });
    }

    [Test]
    public void Diff_JsonFormat_EmitsOneParsableCiArtifactDocument()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            DiffOutcome = new PublicApiDiffOutcome(
                true, false, DriftDelta(), SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(document.RootElement.GetProperty("passed").GetBoolean(), Is.False);
        });
    }

    [Test]
    public void Diff_SarifFormat_EmitsParsableSarifDocument()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            DiffOutcome = new PublicApiDiffOutcome(
                true, false, DriftDelta(), SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "sarif", false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength(), Is.EqualTo(3));
        });
    }

    [Test]
    public void Diff_MissingSnapshotOption_FailsWithExitCodeTwo()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiDiffCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, null, null, "human", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--snapshot is required"));
        });
    }

    [Test]
    public void Update_DryRunPreviewsWithoutWriting()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, DriftDelta(), true, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", true, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.OutputText, Does.Contain("was not modified"));
            Assert.That(console.OutputText, Does.Contain(CapturedSnapshot));
        });
    }

    [Test]
    public void Update_WritesSnapshotWhenNotDryRun()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, DriftDelta(), false, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
        });
    }

    [Test]
    public void Update_InlineDeclarationRefusal_IsReportedAndNothingIsWritten()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, false, null, Array.Empty<BuildStatePreflightDiagnostic>(),
                "declares its surface inline via 'declared_api'", PublicApiFailureKind.InvalidInput),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("inline"));
        });
    }

    [Test]
    public void Migrate_DriftRefusalReportsStaleAndUndeclaredEntries()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                false, null,
                new[] { "class Acme.Gone" },
                new[] { "class Acme.New" },
                SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>(),
                "has 1 stale inline declaration(s)", PublicApiFailureKind.Drift),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("[stale] class Acme.Gone"));
            Assert.That(console.ErrorText, Does.Contain("[undeclared] class Acme.New"));
        });
    }

    [Test]
    public void Migrate_AcceptedDriftWritesSnapshotAndStillReportsDrift()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot,
                new[] { "class Acme.Gone" },
                Array.Empty<string>(),
                SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
            Assert.That(console.OutputText, Does.Contain("[stale] class Acme.Gone"));
            Assert.That(console.OutputText, Does.Contain($"api_snapshot: {SnapshotPath}"));
        });
    }

    // migrate writes a brand-new reviewed artifact, exactly like capture, so it must not silently
    // destroy an existing file — another contract's reviewed snapshot, or any other repository-local
    // file the caller pointed --output at by mistake.
    [Test]
    public void Migrate_RefusesToOverwriteDifferingExistingDestinationWithoutForce()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = "someone else's reviewed snapshot" };
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, Array.Empty<string>(), Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("already exists and differs"));
        });
    }

    [Test]
    public void Migrate_ForceReplacesDifferingExistingDestination()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = "someone else's reviewed snapshot" };
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, Array.Empty<string>(), Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, true, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
        });
    }

    [Test]
    public void Migrate_IdenticalExistingDestinationSucceedsWithoutWriting()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath) { ReadContents = CapturedSnapshot };
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, Array.Empty<string>(), Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void Migrate_DryRunDoesNotWrite()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, Array.Empty<string>(), Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false, true, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.OutputText, Does.Contain("was not written"));
        });
    }

    // Core resolves the destination against the policy boundary; the handler must probe and write
    // that path, not the raw --output string, or a --force run could replace an unrelated file.
    [Test]
    public void Capture_WritesToTheResolvedDestinationNotTheAuthoredPath()
    {
        const string Resolved = "/repo/architecture/api/module-api.txt";
        StubFileSystem fileSystem = new(PolicyPath);
        StubRuntime runtime = new()
        {
            CaptureOutcome = new PublicApiCaptureOutcome(
                true, CapturedSnapshot, 12, Resolved, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiCaptureCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, "../escape.txt", null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo(Resolved));
        });
    }

    [Test]
    public void Capture_RejectsSarifBecauseItHasNoFindingSetToRepresent()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiCaptureCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "sarif", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("Invalid format for public-api capture: sarif"));
        });
    }

    [Test]
    public void Update_RejectsSarif()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "sarif", false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [Test]
    public void Migrate_RejectsSarif()
    {
        RecordingConsole console = new();

        int exitCode = new PublicApiMigrateCommandHandler(new StubRuntime(), console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "sarif", false, false, false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    // Machine output must be one parsable document: appending human prose after a JSON payload was
    // the defect this guards against.
    [Test]
    public void Update_JsonOutputIsOneParsableDocumentEvenInDryRun()
    {
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, DriftDelta(), true, SnapshotPath, Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", true, false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("dry-run"));
            Assert.That(document.RootElement.GetProperty("snapshotPath").GetString(), Is.EqualTo(SnapshotPath));
            Assert.That(document.RootElement.GetProperty("proposedSnapshot").GetString(), Is.EqualTo(CapturedSnapshot));
            Assert.That(
                document.RootElement.GetProperty("delta").GetProperty("changed")[0]
                    .GetProperty("previous_api_signature").GetString(),
                Is.EqualTo("method Acme.Thing.Do(): System.Void"));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void Migrate_JsonOutputIsOneParsableDocument()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, new[] { "class Acme.Gone" }, Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", true, false, false, false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("migrated"));
            Assert.That(document.RootElement.GetProperty("acceptedDrift").GetBoolean(), Is.True);
        });
    }

    // A dry run still has to tell the caller what it *would* have written; only "dryRun: true"
    // signals that nothing was actually written, not a null "output".
    [Test]
    public void Migrate_DryRunJsonOutput_StillReportsTheDestination()
    {
        StubFileSystem fileSystem = new(PolicyPath);
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                true, CapturedSnapshot, Array.Empty<string>(), Array.Empty<string>(), SnapshotPath,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", false, false, true, false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("dry-run"));
            Assert.That(document.RootElement.GetProperty("dryRun").GetBoolean(), Is.True);
            Assert.That(document.RootElement.GetProperty("output").GetString(), Is.EqualTo(SnapshotPath));
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    // Only refused migration drift is a completed gate; everything else never completed and must
    // return 2 so CI can tell "reviewed surface drifted" from "the command could not run".
    [Test]
    public void Migrate_NonDriftFailureReturnsExitCodeTwo()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            MigrateOutcome = new PublicApiMigrateOutcome(
                false, null, Array.Empty<string>(), Array.Empty<string>(), null,
                Array.Empty<BuildStatePreflightDiagnostic>(),
                "Unknown public API surface contract 'absent'.", PublicApiFailureKind.InvalidInput),
        };

        int exitCode = new PublicApiMigrateCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, "absent", SnapshotPath, null, "human", false, false, false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [Test]
    public void Diff_NonDriftFailureReturnsExitCodeTwo()
    {
        RecordingConsole console = new();
        StubRuntime runtime = new()
        {
            DiffOutcome = new PublicApiDiffOutcome(
                false, false, PublicApiDelta.Empty, null, Array.Empty<BuildStatePreflightDiagnostic>(),
                "Public API snapshot not found: x.", PublicApiFailureKind.InvalidInput),
        };

        int exitCode = new PublicApiDiffCommandHandler(runtime, console, new StubFileSystem(PolicyPath)).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [Test]
    public void Update_WritesToTheResolvedDestinationNotTheAuthoredPath()
    {
        const string Resolved = "/repo/architecture/api/module-api.txt";
        StubFileSystem fileSystem = new(PolicyPath, SnapshotPath);
        StubRuntime runtime = new()
        {
            UpdateOutcome = new PublicApiUpdateOutcome(
                true, CapturedSnapshot, PublicApiDelta.Empty, false, Resolved,
                Array.Empty<BuildStatePreflightDiagnostic>()),
        };

        int exitCode = new PublicApiUpdateCommandHandler(runtime, new RecordingConsole(), fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, ContractId, "api/module-api.txt", null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo(Resolved));
        });
    }

    [Test]
    public void Handlers_ShowHelpWithoutTouchingTheRuntime()
    {
        RecordingConsole console = new();
        StubFileSystem fileSystem = new();
        StubRuntime runtime = new();

        int capture = new PublicApiCaptureCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiCaptureCommandOptions(PolicyPath, null, null, null, "human", false, true));
        int diff = new PublicApiDiffCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiDiffCommandOptions(PolicyPath, null, null, null, "human", true));
        int update = new PublicApiUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiUpdateCommandOptions(PolicyPath, null, null, null, "human", false, true));
        int migrate = new PublicApiMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new PublicApiMigrateCommandOptions(PolicyPath, null, null, null, "human", false, false, false, true));

        Assert.Multiple(() =>
        {
            Assert.That(new[] { capture, diff, update, migrate }, Is.All.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText, Does.Contain("public-api capture"));
            Assert.That(console.OutputText, Does.Contain("public-api migrate"));
        });
    }
}
