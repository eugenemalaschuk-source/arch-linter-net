using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class PublicApiCommandHandlerTests
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
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, false, false));

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
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", true, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo(CapturedSnapshot));
            Assert.That(console.OutputText, Does.Contain("[stale] class Acme.Gone"));
            Assert.That(console.OutputText, Does.Contain($"api_snapshot: {SnapshotPath}"));
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
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "human", false, true, false));

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
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "sarif", false, false, false));

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
            new PublicApiMigrateCommandOptions(PolicyPath, ContractId, SnapshotPath, null, "json", true, false, false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("migrated"));
            Assert.That(document.RootElement.GetProperty("acceptedDrift").GetBoolean(), Is.True);
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
            new PublicApiMigrateCommandOptions(PolicyPath, "absent", SnapshotPath, null, "human", false, false, false));

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
            new PublicApiMigrateCommandOptions(PolicyPath, null, null, null, "human", false, false, true));

        Assert.Multiple(() =>
        {
            Assert.That(new[] { capture, diff, update, migrate }, Is.All.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText, Does.Contain("public-api capture"));
            Assert.That(console.OutputText, Does.Contain("public-api migrate"));
        });
    }

    private sealed class StubFileSystem(params string[] existingPaths) : IFileSystem
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);

        public string? LastWritePath { get; private set; }

        public string? LastWriteContents { get; private set; }

        public string ReadContents { get; init; } = string.Empty;

        public bool FileExists(string path) => _existingPaths.Contains(path);

        public string ReadAllText(string path) => ReadContents;

        public void WriteAllText(string path, string contents)
        {
            LastWritePath = path;
            LastWriteContents = contents;
        }

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            LastWritePath = targetPath;
            LastWriteContents = contents;
            return targetPath + ".tmp";
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
        }

        public void DeleteFile(string path)
        {
        }

        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public RecordingConsole()
        {
            Out = new StringWriter(_output);
            Error = new StringWriter(_error);
        }

        public TextWriter Out { get; }

        public TextWriter Error { get; }

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();
    }

    private sealed class StubRuntime : ICliRuntime
    {
        public PublicApiCaptureOutcome? CaptureOutcome { get; init; }

        public PublicApiDiffOutcome? DiffOutcome { get; init; }

        public PublicApiUpdateOutcome? UpdateOutcome { get; init; }

        public PublicApiMigrateOutcome? MigrateOutcome { get; init; }

        public string Version => "1.0.0";

        public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request) =>
            CaptureOutcome ?? throw new NotSupportedException();

        public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request) =>
            DiffOutcome ?? throw new NotSupportedException();

        public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request) =>
            UpdateOutcome ?? throw new NotSupportedException();

        public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request) =>
            MigrateOutcome ?? throw new NotSupportedException();

        // The delta formatter routes human output through the real Core formatter, so this stub
        // uses it too: the assertions above then exercise the same rendering the CLI ships.
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) =>
            new ArchitectureDiagnosticFormatter().FormatViolationsForHumans(violations);

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => throw new NotSupportedException();

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();

        public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing) =>
            throw new NotSupportedException();

        public string FormatResultForCiArtifacts(
            string mode,
            bool passed,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<ArchitectureViolation> coverageFindings,
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
            IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
            IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
            IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
            IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();

        public string FormatResultAsSarif(
            string mode,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
            IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();

        public string FormatCyclesForHumans(
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) =>
            throw new NotSupportedException();

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) =>
            throw new NotSupportedException();

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) =>
            throw new NotSupportedException();

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) =>
            throw new NotSupportedException();

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
            ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();

        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) =>
            throw new NotSupportedException();

        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request) => throw new NotSupportedException();

        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request) => throw new NotSupportedException();

        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request) => throw new NotSupportedException();

        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request) => throw new NotSupportedException();

        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request) => throw new NotSupportedException();

        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request) => throw new NotSupportedException();

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => throw new NotSupportedException();

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => throw new NotSupportedException();

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => throw new NotSupportedException();
    }
}
