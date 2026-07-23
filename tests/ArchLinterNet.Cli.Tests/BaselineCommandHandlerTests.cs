using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class BaselineCommandHandlerTests
{
    private static readonly string[] _contractIds = ["rule-a", "rule-b"];

    [Test]
    public void BaselineCommandOptions_RetainSuppliedValues()
    {
        var generate = new BaselineGenerateCommandOptions("policy.yml", "generate.yml", "reason", "all", "ci", _contractIds, false);
        var update = new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "update.yml", "reason", "strict", "ci", _contractIds, true);
        var prune = new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "prune.yml", "audit", "ci", "json", _contractIds, false);
        var diff = new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", "ci", "human", _contractIds, true);
        var verify = new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "json", _contractIds, false);

        Assert.Multiple(() =>
        {
            Assert.That(generate.OutputPath, Is.EqualTo("generate.yml"));
            Assert.That(generate.Reason, Is.EqualTo("reason"));
            Assert.That(generate.ContractIds, Is.EqualTo(_contractIds));
            Assert.That(update.BaselinePath, Is.EqualTo("baseline.yml"));
            Assert.That(update.ShowHelp, Is.True);
            Assert.That(prune.Format, Is.EqualTo("json"));
            Assert.That(prune.OutputPath, Is.EqualTo("prune.yml"));
            Assert.That(diff.Mode, Is.EqualTo("strict"));
            Assert.That(diff.ShowHelp, Is.True);
            Assert.That(verify.PolicyPath, Is.EqualTo("policy.yml"));
            Assert.That(verify.BaselinePath, Is.EqualTo("baseline.yml"));
        });
    }

    [Test]
    public void SystemCliConsole_WritesToCurrentConsoleStreams()
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            var console = new SystemCliConsole();
            console.Out.Write("hello");
            console.Error.Write("boom");

            Assert.Multiple(() =>
            {
                Assert.That(outWriter.ToString(), Is.EqualTo("hello"));
                Assert.That(errorWriter.ToString(), Is.EqualTo("boom"));
            });
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Test]
    public void BaselineGenerate_Success_WritesYamlAndForwardsRequest()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "generated: yaml", 2, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions("policy.yml", "generated.yml", "generated reason", "all", "ci", _contractIds, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("generated.yml"));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo("generated: yaml"));
            Assert.That(runtime.GenerateRequest, Is.Not.Null);
            Assert.That(runtime.GenerateRequest!.ConditionSetName, Is.EqualTo("ci"));
            Assert.That(runtime.GenerateRequest.ContractIds, Is.EqualTo(_contractIds));
            Assert.That(console.OutputText, Does.Contain("Generated baseline with 2 violation entries."));
            Assert.That(console.OutputText, Does.Contain("Output: generated.yml"));
        });
    }

    [Test]
    public void BaselineGenerate_ConfigurationViolations_ReportDetailedError()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(false, null, 0, [CreateViolation("Source.A", "Forbidden.A")])
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions("policy.yml", "generated.yml", "reason", "strict", null, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be generated"));
            Assert.That(console.ErrorText, Does.Contain("Source.A: Forbidden.A"));
        });
    }

    [Test]
    public void BaselineGenerate_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineGenerateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineGenerateCommandOptions("policy.yml", "generated.yml", "reason", "invalid", null, Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineGenerateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineGenerateCommandOptions("policy.yml", null, "reason", "strict", null, Array.Empty<string>(), false)),
            "--output is required");

        AssertGuardCase(console => new BaselineGenerateCommandHandler(new StubRuntime(), console, new StubFileSystem()).Execute(
                new BaselineGenerateCommandOptions("policy.yml", "generated.yml", "reason", "strict", null, Array.Empty<string>(), false)),
            "Policy file not found");

        var throwingRuntime = new StubRuntime { GenerateException = new InvalidOperationException("generate boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineGenerateCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml")).Execute(
            new BaselineGenerateCommandOptions("policy.yml", "generated.yml", "reason", "strict", null, Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline generation error: generate boom"));
    }

    [Test]
    public void BaselineUpdate_Success_WritesYamlAndForwardsRequest()
    {
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(true, "updated: yaml", 3, 1, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "update reason", "audit", "ci", _contractIds, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("updated.yml"));
            Assert.That(runtime.UpdateRequest, Is.Not.Null);
            Assert.That(runtime.UpdateRequest!.BaselinePath, Is.EqualTo("baseline.yml"));
            Assert.That(runtime.UpdateRequest.ContractIds, Is.EqualTo(_contractIds));
            Assert.That(console.OutputText, Does.Contain("preserved 3, added 1 new entries"));
        });
    }

    [Test]
    public void BaselineUpdate_ConfigurationViolations_ReportDetailedError()
    {
        var runtime = new StubRuntime
        {
            UpdateOutcome = new BaselineUpdateOutcome(false, null, 0, 0, [CreateViolation("Source.B", "Forbidden.B")])
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int result = new BaselineUpdateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "reason", "strict", null, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be updated"));
            Assert.That(console.ErrorText, Does.Contain("Source.B: Forbidden.B"));
        });
    }

    [Test]
    public void BaselineUpdate_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "reason", "invalid", null, Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", null, "updated.yml", "reason", "strict", null, Array.Empty<string>(), false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", null, "reason", "strict", null, Array.Empty<string>(), false)),
            "--output is required");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "reason", "strict", null, Array.Empty<string>(), false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "reason", "strict", null, Array.Empty<string>(), false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { UpdateException = new InvalidOperationException("update boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineUpdateCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", "reason", "strict", null, Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline update error: update boom"));
    }

    [Test]
    public void BaselinePrune_Success_FormatsJsonAndHumanOutput()
    {
        var removedEntry = new BaselineRemovedEntry(CreateEntry("group-a", "contract-a", "Source.C", "Forbidden.C", "reason-a"), "stale");
        var runtime = new StubRuntime
        {
            PruneOutcome = new BaselinePruneOutcome(true, "pruned: yaml", [removedEntry], Array.Empty<ArchitectureViolation>())
        };
        var jsonConsole = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int jsonResult = new BaselinePruneCommandHandler(runtime, jsonConsole, fileSystem).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "all", "ci", "json", _contractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("pruned.yml"));
            Assert.That(json.RootElement.GetProperty("output").GetString(), Is.EqualTo("pruned.yml"));
            Assert.That(json.RootElement.GetProperty("removed")[0].GetProperty("removalReason").GetString(), Is.EqualTo("stale"));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselinePruneCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "all", "ci", "human", _contractIds, false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("Pruned baseline: removed 1 entries."));
        Assert.That(humanConsole.OutputText, Does.Contain("[stale] group-a/contract-a: Source.C -> Forbidden.C"));
    }

    [Test]
    public void BaselinePrune_ConfigurationViolations_ReportDetailedError()
    {
        var runtime = new StubRuntime
        {
            PruneOutcome = new BaselinePruneOutcome(false, null, Array.Empty<BaselineRemovedEntry>(), [CreateViolation("Source.D", "Forbidden.D")])
        };
        var console = new RecordingConsole();

        int result = new BaselinePruneCommandHandler(runtime, console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be pruned"));
            Assert.That(console.ErrorText, Does.Contain("Source.D: Forbidden.D"));
        });
    }

    [Test]
    public void BaselinePrune_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "invalid", null, "json", Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", null, "pruned.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", null, "strict", null, "json", Array.Empty<string>(), false)),
            "--output is required");

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { PruneException = new InvalidOperationException("prune boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselinePruneCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline prune error: prune boom"));
    }

    [Test]
    public void BaselineDiff_Success_FormatsJsonAndHumanOutput()
    {
        var newEntry = CreateEntry("group-b", "contract-b", "Source.E", "Forbidden.E", "reason-b");
        var frozenEntry = CreateEntry("group-b", "contract-b", "Source.Frozen", "Forbidden.Frozen", "reason-frozen");
        var resolvedEntry = CreateEntry("group-b", "contract-b", "Source.Resolved", "Forbidden.Resolved", "reason-resolved");
        var configErrorEntry = CreateEntry("group-b", "unknown-contract", "Source.Cfg", "Forbidden.Cfg", "reason-cfg");
        var runtime = new StubRuntime
        {
            DiffOutcome = new BaselineDiffOutcome(true, [newEntry], [frozenEntry], [resolvedEntry], [configErrorEntry], Array.Empty<ArchitectureViolation>())
        };

        var jsonConsole = new RecordingConsole();
        int jsonResult = new BaselineDiffCommandHandler(runtime, jsonConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "audit", "ci", "json", _contractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.DiffRequest, Is.Not.Null);
            Assert.That(runtime.DiffRequest!.ContractIds, Is.EqualTo(_contractIds));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("contractId").GetString(), Is.EqualTo("contract-b"));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("status").GetString(), Is.EqualTo("new"));
            Assert.That(json.RootElement.GetProperty("frozen")[0].GetProperty("status").GetString(), Is.EqualTo("matched"));
            Assert.That(json.RootElement.GetProperty("resolved")[0].GetProperty("status").GetString(), Is.EqualTo("stale"));
            Assert.That(json.RootElement.GetProperty("configurationErrors")[0].GetProperty("status").GetString(), Is.EqualTo("configuration_error"));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselineDiffCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "audit", "ci", "human", Array.Empty<string>(), false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("New (unbaselined) violations: 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("group-b/contract-b: Source.E -> Forbidden.E"));
    }

    [Test]
    public void BaselineDiff_ConfigurationViolations_ReportDetailedError()
    {
        var runtime = new StubRuntime
        {
            DiffOutcome = new BaselineDiffOutcome(false, Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), [CreateViolation("Source.F", "Forbidden.F")])
        };
        var console = new RecordingConsole();

        int result = new BaselineDiffCommandHandler(runtime, console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "human", Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be diffed"));
            Assert.That(console.ErrorText, Does.Contain("Source.F: Forbidden.F"));
        });
    }

    [Test]
    public void BaselineDiff_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "invalid", null, "json", Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", null, "strict", null, "json", Array.Empty<string>(), false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { DiffException = new InvalidOperationException("diff boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineDiffCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline diff error: diff boom"));
    }

    [Test]
    public void BaselineVerify_Success_FormatsJsonAndHumanOutput()
    {
        var newEntry = CreateEntry("group-c", "contract-c", "Source.G", "Forbidden.G", "reason-c");
        var frozenEntry = CreateEntry("group-c", "contract-c", "Source.Frozen", "Forbidden.Frozen", "reason-frozen");
        var resolvedEntry = CreateEntry("group-c", "contract-c", "Source.Resolved", "Forbidden.Resolved", "reason-resolved");
        var configErrorEntry = CreateEntry("group-c", "unknown-contract", "Source.Cfg", "Forbidden.Cfg", "reason-cfg");
        var runtime = new StubRuntime
        {
            VerifyOutcome = new BaselineVerifyOutcome(
                true,
                false,
                [newEntry],
                [frozenEntry],
                [resolvedEntry],
                [configErrorEntry],
                Array.Empty<ArchitectureViolation>())
        };

        var jsonConsole = new RecordingConsole();
        int jsonResult = new BaselineVerifyCommandHandler(runtime, jsonConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "json", _contractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(runtime.VerifyRequest, Is.Not.Null);
            Assert.That(runtime.VerifyRequest!.ContractIds, Is.EqualTo(_contractIds));
            Assert.That(json.RootElement.GetProperty("inSync").GetBoolean(), Is.False);
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("sourceType").GetString(), Is.EqualTo("Source.G"));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("status").GetString(), Is.EqualTo("new"));
            Assert.That(json.RootElement.GetProperty("frozen")[0].GetProperty("status").GetString(), Is.EqualTo("matched"));
            Assert.That(json.RootElement.GetProperty("resolved")[0].GetProperty("status").GetString(), Is.EqualTo("stale"));
            Assert.That(json.RootElement.GetProperty("configurationErrors")[0].GetProperty("status").GetString(), Is.EqualTo("configuration_error"));
        });

        runtime.VerifyOutcome = runtime.VerifyOutcome with { InSync = true };
        var humanConsole = new RecordingConsole();
        int humanResult = new BaselineVerifyCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "human", Array.Empty<string>(), false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("New (unbaselined) violations: 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("Baseline is in sync."));
    }

    [Test]
    public void BaselineVerify_ConfigurationViolations_ReportDetailedError()
    {
        var runtime = new StubRuntime
        {
            VerifyOutcome = new BaselineVerifyOutcome(
                false,
                false,
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Array.Empty<ArchitectureBaselineComparisonEntry>(),
                [CreateViolation("Source.H", "Forbidden.H")])
        };
        var console = new RecordingConsole();

        int result = new BaselineVerifyCommandHandler(runtime, console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("baseline cannot be verified"));
            Assert.That(console.ErrorText, Does.Contain("Source.H: Forbidden.H"));
        });
    }

    [Test]
    public void BaselineVerify_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "invalid", null, "json", Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", null, "strict", null, "json", Array.Empty<string>(), false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { VerifyException = new InvalidOperationException("verify boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineVerifyCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline verify error: verify boom"));
    }

    private static ArchitectureViolation CreateViolation(string sourceType, string forbiddenNamespace)
    {
        return new ArchitectureViolation("contract", "rule", sourceType, forbiddenNamespace, ["ref"]);
    }

    private static ArchitectureBaselineComparisonEntry CreateEntry(
        string contractGroup,
        string contractId,
        string sourceType,
        string forbiddenReference,
        string reason)
    {
        return new ArchitectureBaselineComparisonEntry(contractGroup, contractId, sourceType, forbiddenReference, reason);
    }

    private static void AssertGuardCase(
        Func<RecordingConsole, int> execute,
        string expectedError)
    {
        var console = new RecordingConsole();
        int result = execute(console);
        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.ErrorText, Does.Contain(expectedError));
    }

    private sealed class StubFileSystem(params string[] existingPaths) : IFileSystem
    {
        private readonly HashSet<string> _existingPaths = new(existingPaths, StringComparer.Ordinal);

        public string? LastWritePath { get; private set; }

        public string? LastWriteContents { get; private set; }

        public bool FileExists(string path) => _existingPaths.Contains(path);

        public void WriteAllText(string path, string contents)
        {
            LastWritePath = path;
            LastWriteContents = contents;
        }
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        private readonly TextWriter _out;
        private readonly TextWriter _errorWriter;

        public RecordingConsole()
        {
            _out = new StringWriter(_output);
            _errorWriter = new StringWriter(_error);
        }

        public TextWriter Out => _out;

        public TextWriter Error => _errorWriter;

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();
    }

    private sealed class StubRuntime : ICliRuntime
    {
        private static readonly ArchitectureDependencyGraph _emptyGraph =
            new(Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>());

        public string Version => "1.0.0";

        public BaselineGenerationOutcome GenerateOutcome { get; set; } =
            new(true, "generated", 0, Array.Empty<ArchitectureViolation>());

        public BaselineUpdateOutcome UpdateOutcome { get; set; } =
            new(true, "updated", 0, 0, Array.Empty<ArchitectureViolation>());

        public BaselinePruneOutcome PruneOutcome { get; set; } =
            new(true, "pruned", Array.Empty<BaselineRemovedEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineDiffOutcome DiffOutcome { get; set; } =
            new(true, Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineVerifyOutcome VerifyOutcome { get; set; } =
            new(true, true, Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureBaselineComparisonEntry>(), Array.Empty<ArchitectureViolation>());

        public BaselineMigrateOutcome MigrateOutcome { get; set; } =
            new(true, "migrated", 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>());

        public Exception? GenerateException { get; set; }

        public Exception? UpdateException { get; set; }

        public Exception? PruneException { get; set; }

        public Exception? DiffException { get; set; }

        public Exception? VerifyException { get; set; }

        public Exception? MigrateException { get; set; }

        public BaselineGenerationRequest? GenerateRequest { get; private set; }

        public BaselineUpdateRequest? UpdateRequest { get; private set; }

        public BaselinePruneRequest? PruneRequest { get; private set; }

        public BaselineDiffRequest? DiffRequest { get; private set; }

        public BaselineVerifyRequest? VerifyRequest { get; private set; }

        public BaselineMigrateRequest? MigrateRequest { get; private set; }

        public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level) => Enum.TryParse(value, true, out level);

        public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing) => throw new NotSupportedException();

        public string FormatResultForCiArtifacts(string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings, IReadOnlyCollection<ArchitectureViolation> coverageFindings, IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations, IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings, IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries, IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures, IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred, IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics) => throw new NotSupportedException();
        public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics) => throw new NotSupportedException();

        public string FormatResultAsSarif(string mode, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => throw new NotSupportedException();

        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles, IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings) => throw new NotSupportedException();

        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics) => throw new NotSupportedException();

        public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations) => throw new NotSupportedException();

        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings) => throw new NotSupportedException();

        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) => throw new NotSupportedException();

        public string FormatClassificationFactsForHumans(IReadOnlyCollection<ArchitectureClassificationConflict> conflicts, IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures, ArchitectureClassificationPathDeferredNotice? classificationPathDeferred) => throw new NotSupportedException();

        public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request)
        {
            GenerateRequest = request;
            return GenerateException == null ? GenerateOutcome : throw GenerateException;
        }

        public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request)
        {
            UpdateRequest = request;
            return UpdateException == null ? UpdateOutcome : throw UpdateException;
        }

        public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request)
        {
            PruneRequest = request;
            return PruneException == null ? PruneOutcome : throw PruneException;
        }

        public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
        {
            DiffRequest = request;
            return DiffException == null ? DiffOutcome : throw DiffException;
        }

        public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request)
        {
            VerifyRequest = request;
            return VerifyException == null ? VerifyOutcome : throw VerifyException;
        }

        public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request)
        {
            MigrateRequest = request;
            return MigrateException == null ? MigrateOutcome : throw MigrateException;
        }

        public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request) => new(_emptyGraph);

        public string FormatGraphAsJson(ArchitectureDependencyGraph graph) => "{}";

        public string FormatGraphAsDot(ArchitectureDependencyGraph graph) => "digraph G {}";

        public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph) => "graph TD";

        public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request) => new("Source", "Target", null, Array.Empty<string>());
    }
}
