using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
internal sealed class BaselineCommandHandlerTests : BaselineCommandHandlerTestBase
{
    [Test]
    public void BaselineCommandOptions_RetainSuppliedValues()
    {
        var generate = new BaselineGenerateCommandOptions(
            "policy.yml", "generate.yml", Reasons, "all", "ci", "human", WriteOptions, ContractIds, false);
        var update = new BaselineUpdateCommandOptions(
            "policy.yml", "baseline.yml", "update.yml", Reasons, "strict", "ci", "human", WriteOptions, ContractIds, true);
        var prune = new BaselinePruneCommandOptions(
            "policy.yml", "baseline.yml", "prune.yml", "audit", "ci", "json", WriteOptions, ContractIds, false);
        var diff = new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", "ci", "human", ContractIds, true);
        var verify = new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "json", ContractIds, false);

        Assert.Multiple(() =>
        {
            Assert.That(generate.OutputPath, Is.EqualTo("generate.yml"));
            Assert.That(generate.Reasons.Reason, Is.EqualTo("reason"));
            Assert.That(generate.ContractIds, Is.EqualTo(ContractIds));
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
    public void BaselineGenerate_Success_WritesYamlAndForwardsRequest()
    {
        var runtime = new StubRuntime
        {
            GenerateOutcome = new BaselineGenerationOutcome(true, "generated: yaml", 2, Array.Empty<ArchitectureViolation>())
        };
        var console = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml");

        int result = new BaselineGenerateCommandHandler(runtime, console, fileSystem).Execute(
            new BaselineGenerateCommandOptions(
                "policy.yml", "generated.yml", Reasons with { Reason = "generated reason" }, "all", "ci", "human", WriteOptions, ContractIds, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("generated.yml"));
            Assert.That(fileSystem.LastWriteContents, Is.EqualTo("generated: yaml"));
            Assert.That(runtime.GenerateRequest, Is.Not.Null);
            Assert.That(runtime.GenerateRequest!.ConditionSetName, Is.EqualTo("ci"));
            Assert.That(runtime.GenerateRequest.ContractIds, Is.EqualTo(ContractIds));
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
            new BaselineGenerateCommandOptions("policy.yml", "generated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false));

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
                new BaselineGenerateCommandOptions("policy.yml", "generated.yml", Reasons, "invalid", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineGenerateCommandHandler(new StubRuntime(), console, new StubFileSystem()).Execute(
                new BaselineGenerateCommandOptions("policy.yml", "generated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "Policy file not found");

        var throwingRuntime = new StubRuntime { GenerateException = new InvalidOperationException("generate boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineGenerateCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml")).Execute(
            new BaselineGenerateCommandOptions("policy.yml", "generated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false));

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
            new BaselineUpdateCommandOptions(
                "policy.yml", "baseline.yml", "updated.yml", Reasons with { Reason = "update reason" }, "audit", "ci", "human", WriteOptions,
                ContractIds, false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("updated.yml"));
            Assert.That(runtime.UpdateRequest, Is.Not.Null);
            Assert.That(runtime.UpdateRequest!.BaselinePath, Is.EqualTo("baseline.yml"));
            Assert.That(runtime.UpdateRequest.ContractIds, Is.EqualTo(ContractIds));
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
            new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false));

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
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "invalid", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "Invalid mode");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", null, "updated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "--baseline is required");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "Policy file not found");

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false)),
            "Baseline file not found");

        var throwingRuntime = new StubRuntime { UpdateException = new InvalidOperationException("update boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineUpdateCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "strict", null, "human", WriteOptions, Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.ErrorText, Does.Contain("Baseline update error: update boom"));
    }

    [Test]
    public void BaselinePrune_Success_FormatsJsonAndHumanOutput()
    {
        ArchitectureBaselineComparisonEntry resolved = CreateEntry("group-a", "contract-a", "Source.C", "Forbidden.C", "reason-a");
        var removedEntry = new BaselineRemovedEntry(resolved, BaselineEntryLifecycleNames.Resolved);
        var runtime = new StubRuntime
        {
            PruneOutcome = new BaselinePruneOutcome(true, "pruned: yaml", [removedEntry], Array.Empty<ArchitectureViolation>())
            {
                Entries = [new BaselineLifecycleEntry(resolved, BaselineEntryLifecycle.Resolved)],
            }
        };
        var jsonConsole = new RecordingConsole();
        var fileSystem = new StubFileSystem("policy.yml", "baseline.yml");

        int jsonResult = new BaselinePruneCommandHandler(runtime, jsonConsole, fileSystem).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "all", "ci", "json", WriteOptions, ContractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.LastWritePath, Is.EqualTo("pruned.yml"));
            Assert.That(json.RootElement.GetProperty("output").GetString(), Is.EqualTo("pruned.yml"));
            Assert.That(json.RootElement.GetProperty("removed")[0].GetProperty("removalReason").GetString(), Is.EqualTo("resolved"));
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("resolved").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("entries")[0].GetProperty("status").GetString(), Is.EqualTo("resolved"));
            Assert.That(json.RootElement.GetProperty("status").GetString(), Is.EqualTo("pruned"));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselinePruneCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "all", "ci", "human", WriteOptions, ContractIds, false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("Pruned baseline: removed 1 entries."));
        Assert.That(humanConsole.OutputText, Does.Contain("resolved: 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("group-a/contract-a: Source.C -> Forbidden.C"));
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
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", WriteOptions, Array.Empty<string>(), false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Is.Empty);
            using JsonDocument document = JsonDocument.Parse(console.OutputText);
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("error"));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("category").GetString(),
                Is.EqualTo("configuration-error"));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("details").GetProperty("violations")[0]
                .GetProperty("source_type").GetString(), Is.EqualTo("Source.D"));
        });
    }

    [Test]
    public void BaselinePrune_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "invalid", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "Invalid mode", true);

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", null, "pruned.yml", "strict", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "--baseline is required", true);

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "Policy file not found", true);

        AssertGuardCase(console => new BaselinePruneCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "Baseline file not found", true);

        var throwingRuntime = new StubRuntime { PruneException = new InvalidOperationException("prune boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselinePruneCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselinePruneCommandOptions("policy.yml", "baseline.yml", "pruned.yml", "strict", null, "json", WriteOptions, Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.OutputText, Does.Contain("Baseline prune error: prune boom"));
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
            {
                Entries = BuildLifecycleReport(newEntry, frozenEntry, resolvedEntry, configErrorEntry),
            }
        };

        var jsonConsole = new RecordingConsole();
        int jsonResult = new BaselineDiffCommandHandler(runtime, jsonConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "audit", "ci", "json", ContractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.DiffRequest, Is.Not.Null);
            Assert.That(runtime.DiffRequest!.ContractIds, Is.EqualTo(ContractIds));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("contractId").GetString(), Is.EqualTo("contract-b"));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("status").GetString(), Is.EqualTo("new"));
            Assert.That(json.RootElement.GetProperty("frozen")[0].GetProperty("status").GetString(), Is.EqualTo("matched"));
            Assert.That(json.RootElement.GetProperty("resolved")[0].GetProperty("status").GetString(), Is.EqualTo("resolved"));
            Assert.That(json.RootElement.GetProperty("configurationErrors")[0].GetProperty("status").GetString(), Is.EqualTo("stale"));
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("matched").GetInt32(), Is.EqualTo(1));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselineDiffCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "audit", "ci", "human", Array.Empty<string>(), false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.Success));
        Assert.That(humanConsole.OutputText, Does.Contain("New (unbaselined) violations: 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("group-b/contract-b: Source.E -> Forbidden.E"));
    }

    [Test]
    public void BaselineDiff_Sarif_ExposesLifecycleStatus()
    {
        var entry = CreateEntry("strict", "rule", "Source.Type", "Forbidden.Type", "debt");
        var runtime = new StubRuntime
        {
            DiffOutcome = new BaselineDiffOutcome(true, [entry], [], [], [], Array.Empty<ArchitectureViolation>())
            {
                Entries = [new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.New)],
            },
        };
        var console = new RecordingConsole();

        int result = new BaselineDiffCommandHandler(runtime, console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "all", null, "sarif", [], false));

        using JsonDocument document = JsonDocument.Parse(console.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
                .GetProperty("properties").GetProperty("baseline_status").GetString(), Is.EqualTo("new"));
        });
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
            "Invalid mode", true);

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", null, "strict", null, "json", Array.Empty<string>(), false)),
            "--baseline is required", true);

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Policy file not found", true);

        AssertGuardCase(console => new BaselineDiffCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Baseline file not found", true);

        var throwingRuntime = new StubRuntime { DiffException = new InvalidOperationException("diff boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineDiffCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineDiffCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.OutputText, Does.Contain("Baseline diff error: diff boom"));
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
            {
                Entries = BuildLifecycleReport(newEntry, frozenEntry, resolvedEntry, configErrorEntry),
            }
        };

        var jsonConsole = new RecordingConsole();
        int jsonResult = new BaselineVerifyCommandHandler(runtime, jsonConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "json", ContractIds, false));

        using JsonDocument json = JsonDocument.Parse(jsonConsole.OutputText);
        Assert.Multiple(() =>
        {
            Assert.That(jsonResult, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(runtime.VerifyRequest, Is.Not.Null);
            Assert.That(runtime.VerifyRequest!.ContractIds, Is.EqualTo(ContractIds));
            Assert.That(json.RootElement.GetProperty("inSync").GetBoolean(), Is.False);
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("sourceType").GetString(), Is.EqualTo("Source.G"));
            Assert.That(json.RootElement.GetProperty("new")[0].GetProperty("status").GetString(), Is.EqualTo("new"));
            Assert.That(json.RootElement.GetProperty("frozen")[0].GetProperty("status").GetString(), Is.EqualTo("matched"));
            Assert.That(json.RootElement.GetProperty("resolved")[0].GetProperty("status").GetString(), Is.EqualTo("resolved"));
            Assert.That(json.RootElement.GetProperty("configurationErrors")[0].GetProperty("status").GetString(), Is.EqualTo("stale"));
            Assert.That(json.RootElement.GetProperty("counts").GetProperty("matched").GetInt32(), Is.EqualTo(1));
        });

        var humanConsole = new RecordingConsole();
        int humanResult = new BaselineVerifyCommandHandler(runtime, humanConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "all", "ci", "human", Array.Empty<string>(), false));

        Assert.That(humanResult, Is.EqualTo(CliExitCodes.ValidationFailure));
        Assert.That(humanConsole.OutputText, Does.Contain("New (unbaselined) violations: 1"));
        Assert.That(humanConsole.OutputText, Does.Contain("Baseline is out of sync."));
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
            Assert.That(console.ErrorText, Is.Empty);
            using JsonDocument document = JsonDocument.Parse(console.OutputText);
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("command_error"));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("message").GetString(),
                Does.Contain("baseline cannot be verified"));
            Assert.That(document.RootElement.GetProperty("error").GetProperty("details").GetProperty("violations")[0]
                .GetProperty("forbidden_namespace").GetString(), Is.EqualTo("Forbidden.H"));
        });
    }

    [Test]
    public void BaselineVerify_GuardsAndException_ReportErrors()
    {
        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "invalid", null, "json", Array.Empty<string>(), false)),
            "Invalid mode", true);

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", null, "strict", null, "json", Array.Empty<string>(), false)),
            "--baseline is required", true);

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Policy file not found", true);

        AssertGuardCase(console => new BaselineVerifyCommandHandler(new StubRuntime(), console, new StubFileSystem("policy.yml")).Execute(
                new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false)),
            "Baseline file not found", true);

        AssertGuardCase(console => new BaselineGenerateCommandHandler(new StubRuntime(), console, new StubFileSystem()).Execute(
                new BaselineGenerateCommandOptions("policy.yml", "generated.yml", Reasons, "strict", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "Policy file not found", true);

        AssertGuardCase(console => new BaselineUpdateCommandHandler(new StubRuntime(), console, new StubFileSystem("baseline.yml")).Execute(
                new BaselineUpdateCommandOptions("policy.yml", "baseline.yml", "updated.yml", Reasons, "strict", null, "json", WriteOptions, Array.Empty<string>(), false)),
            "Policy file not found", true);

        var throwingRuntime = new StubRuntime { VerifyException = new InvalidOperationException("verify boom") };
        var exceptionConsole = new RecordingConsole();
        int exceptionResult = new BaselineVerifyCommandHandler(throwingRuntime, exceptionConsole, new StubFileSystem("policy.yml", "baseline.yml")).Execute(
            new BaselineVerifyCommandOptions("policy.yml", "baseline.yml", "strict", null, "json", Array.Empty<string>(), false));

        Assert.That(exceptionResult, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(exceptionConsole.OutputText, Does.Contain("Baseline verify error: verify boom"));
    }

}
