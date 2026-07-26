using System.Text.Json;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

/// <summary>
/// The gate between a proposed baseline document and the file system: preview, explicit overwrite
/// intent, refusal on unpreservable comments, and atomic writes.
/// </summary>
public sealed partial class BaselineCommandHandlerTests
{
    [Test]
    public void BaselineGenerate_WithoutOutput_PreviewsToStdoutWithoutWriting()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "version: 2", 1, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", null, _reasons, "all", null, "human", _write, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText.Trim(), Is.EqualTo("version: 2"));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineGenerate_ExistingOutputWithoutForce_RefusesAndDoesNotWrite()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "version: 2", 1, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "generated.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "all", null, "human", _write, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--force"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineGenerate_ExistingOutputWithForce_Writes()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "version: 2", 1, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "generated.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "all", null, "human",
                _write with { Force = true }, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("generated.yml"));
            Assert.That(fileSystem.RenameCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void BaselineGenerate_FailedWrite_ReportsErrorWithoutRenamingOverTheOriginal()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "version: 2", 1, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "generated.yml")
        {
            TempWriteException = new IOException("disk full"),
        };

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "all", null, "human",
                _write with { Force = true }, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("disk full"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineGenerate_DryRunWithJson_KeepsStdoutParsableAndCarriesTheProposal()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "version: 2", 1, Array.Empty<ArchitectureViolation>())
            {
                Entries = [new BaselineLifecycleEntry(
                    CreateEntry("strict", "rule-a", "Src.A", "Ref.A", "generated baseline"),
                    BaselineEntryLifecycle.New,
                    BaselineEntryDisposition.Added)],
            }
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "all", null, "json",
                _write with { DryRun = true }, Array.Empty<string>(), false));

        using JsonDocument json = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("dry-run"));
            Assert.That(json.RootElement.GetProperty("proposedContent").GetString(), Is.EqualTo("version: 2"));
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("new").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("entries")[0].GetProperty("disposition").GetString(), Is.EqualTo("added"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineGenerate_MalformedReasonMapping_ReportsTheCoreDiagnostic()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(false, null, 0, Array.Empty<ArchitectureViolation>())
            {
                Error = "--reason-for-family expects '<key>=<reason text>' but received 'composition'.",
            }
        };
        var console = new RecordingConsole();

        int result = new BaselineGenerateCommandHandler(runtime, console, new StubFileSystem("policy.yml")).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", _reasons, "all", null, "human", _write, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--reason-for-family expects"));
        });
    }

    [Test]
    public void BaselineUpdate_InPlaceOutput_WritesWithoutForce()
    {
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "version: 2", 1, 0, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "baseline.yml", _reasons, "all", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("baseline.yml"));
            Assert.That(fileSystem.RenameCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void BaselineUpdate_DifferentExistingOutput_RequiresForce()
    {
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "version: 2", 1, 0, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml", "other.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "other.yml", _reasons, "all", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--force"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineUpdate_UnpreservableComments_RefusesWriteButDryRunStillReports()
    {
        const string Diagnostic = "Baseline 'baseline.yml' has comments that cannot be safely preserved: line(s) 4. Re-run with --dry-run";
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "version: 2", 1, 0, Array.Empty<ArchitectureViolation>())
            {
                CommentDiagnostic = Diagnostic,
            }
        };
        var refusalConsole = new RecordingConsole();
        var refusalFileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int refusal = new BaselineUpdateCommandHandler(runtime, refusalConsole, refusalFileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "baseline.yml", _reasons, "all", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(refusal, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(refusalConsole.ErrorText, Does.Contain("line(s) 4"));
            Assert.That(refusalFileSystem.RenameCount, Is.Zero);
        });

        var dryRunConsole = new RecordingConsole();
        var dryRunFileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int dryRun = new BaselineUpdateCommandHandler(runtime, dryRunConsole, dryRunFileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "baseline.yml", _reasons, "all", null, "human",
                _write with { DryRun = true }, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(dryRun, Is.EqualTo(CliExitCodes.Success));
            Assert.That(dryRunConsole.OutputText, Does.Contain("version: 2"));
            Assert.That(dryRunConsole.OutputText, Does.Contain("line(s) 4"));
            Assert.That(dryRunFileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselinePrune_DryRun_ReportsWithoutWriting()
    {
        ArchitectureBaselineComparisonEntry resolved =
            CreateEntry("strict", "rule-a", "Src.Gone", "Ref.Gone", "old reason");
        var runtime = new StubRuntime
        {
            PruneOutcome = new BaselinePruneOutcome(
                true, "version: 2", [new BaselineRemovedEntry(resolved, BaselineEntryLifecycleNames.Resolved)],
                Array.Empty<ArchitectureViolation>())
            {
                Entries = [new BaselineLifecycleEntry(
                    resolved, BaselineEntryLifecycle.Resolved, BaselineEntryDisposition.Removed)],
            }
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselinePruneCommandHandler(runtime, console, fileSystem).Execute(
            new BaselinePruneCommandOptions(
                "policy.yml", "baseline.yml", "baseline.yml", "all", null, "human",
                _write with { DryRun = true }, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText, Does.Contain("Dry run"));
            Assert.That(console.OutputText, Does.Contain("resolved: 1"));
            Assert.That(console.OutputText, Does.Contain("[removed]"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineMigrate_ExistingOutputWithoutForce_RefusesAndDoesNotWrite()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                true, "version: 2", 1, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "legacy.yml", "migrated.yml");

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions(
                "policy.yml", "legacy.yml", "migrated.yml", null, "human", DryRun: false, Force: false, ShowHelp: false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--force"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineMigrate_DryRun_ShowsTheProposedDocumentAndWritesNothing()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                true, "version: 2\nbaseline: {}\n", 1, 0, 0, Array.Empty<BaselineMigrateEntryReport>(),
                Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "legacy.yml");

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions(
                "policy.yml", "legacy.yml", "migrated.yml", null, "human", DryRun: true, Force: false, ShowHelp: false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.OutputText, Does.Contain("Dry run"));
            Assert.That(console.OutputText, Does.Contain("version: 2"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineMigrate_DryRunWithJson_CarriesTheProposalInTheDocument()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                true, "version: 2", 1, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, new StubFileSystem("policy.yml", "legacy.yml")).Execute(
            new BaselineMigrateCommandOptions(
                "policy.yml", "legacy.yml", "migrated.yml", null, "json", DryRun: true, Force: false, ShowHelp: false));

        using JsonDocument json = JsonDocument.Parse(console.OutputText);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(json.RootElement.GetProperty("proposedContent").GetString(), Is.EqualTo("version: 2"));
            Assert.That(json.RootElement.GetProperty("output").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public void BaselineUpdate_CaseVariantOutputPath_IsNotTreatedAsTheInPlaceDestination()
    {
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "version: 2", 1, 0, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        // On a case-sensitive filesystem these are two different files, so replacing the second one
        // must still require --force rather than riding in on the in-place exemption.
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml", "BASELINE.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "BASELINE.yml", _reasons, "all", null, "human", _write,
                Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("--force"));
            Assert.That(fileSystem.RenameCount, Is.Zero);
        });
    }

    [Test]
    public void BaselineVerify_AmbiguousEntry_IsReportedWithCanonicalIdentityAndCounts()
    {
        ArchitectureViolationIdentity identity = new(
            ArchitectureViolationIdentity.CurrentVersion, "strict", "dependency", "rule-a",
            "Host.A", "Src.Program", null, "Infra", null, "Infra.Db", 0);
        ArchitectureBaselineComparisonEntry ambiguous =
            new("strict", "rule-a", "Src.Program", "Infra.Db", "legacy pair", identity);

        var runtime = new StubRuntime
        {
            VerifyOutcome = new BaselineVerifyOutcome(
                true,
                false,
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureViolation>())
            {
                Ambiguous = [ambiguous],
                Entries = [new BaselineLifecycleEntry(ambiguous, BaselineEntryLifecycle.Ambiguous)],
            }
        };
        var console = new RecordingConsole();

        int result = new BaselineVerifyCommandHandler(runtime, console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", null, "json", Array.Empty<string>(), false));

        using JsonDocument json = JsonDocument.Parse(console.OutputText);
        JsonElement entry = json.RootElement.GetProperty("ambiguous")[0];

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(entry.GetProperty("status").GetString(), Is.EqualTo("ambiguous"));
            Assert.That(entry.GetProperty("identity").GetProperty("sourceAssembly").GetString(), Is.EqualTo("Host.A"));
            Assert.That(entry.GetProperty("identity").GetProperty("canonical").GetString(), Is.Not.Empty);
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("ambiguous").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("resolved").GetInt32(), Is.Zero);
        });
    }
}
