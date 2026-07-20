using System.Text.Json;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class BaselineCommandHandlerTests
{
    [Test]
    public void BaselineMigrate_Success_WritesYamlAndFormatsJsonAndHumanOutput()
    {
        var report = new List<BaselineMigrateEntryReport>
        {
            new("strict_cycles", "package-cycles", "Source.A", "Forbidden.A", "matched", 1),
            new("strict_cycles", "package-cycles", "Source.B", "Forbidden.B", "stale", 0),
        };
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(true, "version: 2", 1, 1, 0, report, Array.Empty<ArchitectureViolation>())
        };

        var jsonConsole = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        int jsonResult = new BaselineMigrateCommandHandler(runtime, jsonConsole, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "migrated.yml", "ci", "json", false, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.MigrateRequest, Is.Not.Null);
            Assert.That(runtime.MigrateRequest!.DryRun, Is.False);
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("migrated.yml"));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo("version: 2"));
            Assert.That(json.RootElement.GetProperty("matchedCount").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("staleCount").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("ambiguousCount").GetInt32(), Is.EqualTo(0));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselineMigrateCommandHandler(runtime, humanConsole, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "migrated.yml", "ci", "human", false, false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("Matched (migrated to version 2): 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("[stale] strict_cycles/package-cycles: Source.B -> Forbidden.B"));
        Assert.That(humanConsole.OutputText, Does.Contain("Output: migrated.yml"));
    }

    [Test]
    public void BaselineMigrate_ConfigurationViolations_ReportDetailedErrorAndDoesNotWrite()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                false, null, 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(),
                [CreateViolation("Source.Cfg", "Forbidden.Cfg")])
        };
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "out.yml", null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be migrated"));
            Assert.That(console.ErrorText, Does.Contain("Source.Cfg: Forbidden.Cfg"));
        });
    }

    [Test]
    public void BaselineMigrate_DryRunWithAmbiguousEntries_ReportsAmbiguousDryRunMessage()
    {
        var report = new List<BaselineMigrateEntryReport>
        {
            new("strict_cycles", "package-cycles", "Source.A", "Forbidden.A", "ambiguous", 2),
        };
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(false, null, 0, 0, 1, report, Array.Empty<ArchitectureViolation>())
        };
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", null, null, "human", true, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.OutputText, Does.Contain("Dry run: ambiguous entries found, no file would be written."));
        });
    }

    [Test]
    public void BaselineMigrate_Ambiguous_DoesNotWriteAndFailsClosed()
    {
        var report = new List<BaselineMigrateEntryReport>
        {
            new("strict_cycles", "package-cycles", "Source.A", "Forbidden.A", "ambiguous", 2),
        };
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(false, null, 0, 0, 1, report, Array.Empty<ArchitectureViolation>())
        };
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "migrated.yml", null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.OutputText, Does.Contain("Ambiguous (multiple current matches, requires manual review): 1"));
            Assert.That(console.OutputText, Does.Contain("(2 current matches)"));
        });
    }

    [Test]
    public void BaselineMigrate_DryRun_NeverWritesEvenWhenClean()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                true, "version: 2", 1, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>())
        };
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", null, null, "human", true, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.MigrateRequest!.DryRun, Is.True);
            Assert.That(fileSystem.LastWritePath, Is.Null);
        });
    }

    [Test]
    public void BaselineMigrate_ApplicationServiceError_ReportsErrorAndDoesNotWrite()
    {
        var runtime = new StubRuntime
        {
            MigrateOutcome = new BaselineMigrateOutcome(
                false, null, 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>(),
                Error: "--output must not be the same path as --baseline; baseline migrate never overwrites the source file.")
        };
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");
        var console = new RecordingConsole();

        int result = new BaselineMigrateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "baseline.yml", null, "human", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.LastWritePath, Is.Null);
            Assert.That(console.ErrorText, Does.Contain("never overwrites the source file"));
        });
    }

    [Test]
    public void BaselineMigrate_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineMigrateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineMigrateCommandOptions("policy.yml", null, "out.yml", null, "json", false, false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselineMigrateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", null, null, "json", false, false)),
            "--output is required");

        AssertGuardCase(console => new BaselineMigrateCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "out.yml", null, "json", false, false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselineMigrateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "out.yml", null, "json", false, false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { MigrateException = new InvalidOperationException("migrate boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineMigrateCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineMigrateCommandOptions("policy.yml", "baseline.yml", "out.yml", null, "json", false, false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline migrate error: migrate boom"));
    }
}
