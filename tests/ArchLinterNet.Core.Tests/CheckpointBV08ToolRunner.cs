using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

using CandidatePackageFeed = CheckpointBReleaseGateTests.CandidatePackageFeed;
using CheckpointScenarioResult = CheckpointBReleaseGateTests.CheckpointScenarioResult;
using CommandResult = CheckpointBReleaseGateTests.CommandResult;

/// <summary>
/// Owns the v0.8 full-cycle scenario's command-phase state: every packed CLI invocation the
/// scenario makes is traced through <see cref="CheckpointBPhaseTrace"/> and, for
/// <c>--ensure-built</c> commands, authorized to skip a redundant restore via
/// <see cref="CheckpointBRestoreReuse"/> once the same fixture root has already restored
/// successfully earlier in the same run. Ordinary Checkpoint B scenarios keep calling
/// <see cref="CandidatePackageFeed.RunTool"/> directly and are untraced.
/// </summary>
internal sealed class CheckpointBV08ToolRunner(CandidatePackageFeed candidate, CheckpointBPhaseTrace phaseTrace)
{
    private readonly CandidatePackageFeed _candidate = candidate;
    private readonly CheckpointBPhaseTrace _phaseTrace = phaseTrace;
    private readonly CheckpointBRestoreReuse _restoreReuse = new();

    internal static string DependenciesPath(string root) => Path.Combine(root, "dependencies.arch.yml");

    internal CommandResult RunToolWithReusedRestore(string workingDirectory, params string[] arguments)
    {
        string[] preparedArguments = _restoreReuse.PrepareArguments(workingDirectory, arguments);
        CommandResult result = RunTracedTool(workingDirectory, preparedArguments);
        _restoreReuse.RecordCompletedEnsureBuilt(workingDirectory, arguments, result.ExitCode);
        return result;
    }

    private CommandResult RunTracedTool(string workingDirectory, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = _candidate.CreateShellStartInfo(workingDirectory, arguments);
        startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
        using CheckpointBPhaseTrace.PhaseScope phase = _phaseTrace.Start(FormatToolCommand(arguments));
        CommandResult result = CheckpointBReleaseGateTests.Run(startInfo, TestContext.CurrentContext.CancellationToken);
        phase.Complete();
        return result;
    }

    private static string FormatToolCommand(IReadOnlyList<string> arguments) =>
        $"arch-linter-net {string.Join(' ', arguments)}";

    internal string[] RunTestingCanonicalIdentities(string policyPath) =>
        _candidate.RunTestingCanonicalIdentities(policyPath);

    internal CheckpointScenarioResult AssertExternalTestingPolicyContextConsumer(
        string consumerPolicyPath, string baselinePath, string baseContextPath, string currentContextPath) =>
        _candidate.AssertExternalTestingPolicyContextConsumer(
            consumerPolicyPath, baselinePath, baseContextPath, currentContextPath);

    internal void AssertPolicyContext(string root, string outputPath, string? policyPath = null)
    {
        CommandResult context = RunToolWithReusedRestore(root,
            "policy", "context",
            "--policy", policyPath ?? DependenciesPath(root),
            "--format", "json");
        Assert.That(context.ExitCode, Is.EqualTo(0), $"policy context ({root}): {context.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(context.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(5));
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture-policy-context"));
        });
        File.WriteAllText(outputPath, context.StandardOutput);
    }
}
