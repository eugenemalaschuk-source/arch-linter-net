using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Baseline.Application;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandHandler(
    ICliRuntime runtime,
    ICliConsole console,
    IFileSystem fileSystem,
    CancellationToken cancellationToken = default)
{
    private const string HelpText =
        """
        arch-linter-net health — project the canonical architecture-health/v1 summary

        Usage:
          arch-linter-net health --policy <path> --baseline <path> [options]

        Options:
          -p, --policy <path>          Architecture policy (default: architecture/dependencies.arch.yml)
              --baseline <path>        Reviewed exact persistent-debt baseline (required)
          -m, --mode <mode>            strict, audit, or all (default: all)
              --contract <id>          Restrict complete analysis to a contract (repeatable)
              --condition-set <name>   Select policy condition set
              --base-context <path>    Base effective-policy context JSON (requires --current-context)
              --current-context <path> Current effective-policy context JSON (requires --base-context)
              --ensure-built           Build and receipt-verify before complete candidate collection
              --no-restore             Do not restore while ensuring build state
              --configuration <name>   Requested build configuration
              --framework <tfm>        Requested target framework
              --platform <platform>    Requested platform
              --runtime <rid>          Requested runtime identifier
          -f, --format <fmt>           human or json (default: human)
          -h, --help                   Show this help message

        Health is a read-only, non-compensating projection of canonical architecture authorities.
        It does not calculate a score, publish badges, render pull requests, or emit SARIF.

        Exit codes:
          0   Gate passed
          1   Gate failed
          2   Gate is unassessable, or arguments/contexts/policy/runtime are invalid
        """;

    public int Execute(HealthCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (!BaselineCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TryValidateFormat(options.Format)
            || !BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "health", options.BaselinePath)
            || !BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            || !BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath!)
            || !TryValidateContexts(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitectureHealthOutcome outcome = runtime.EvaluateHealth(new ArchitectureHealthRequest
            {
                DebtGate = new ArchitectureDebtGateRequest
                {
                    PolicyPath = options.PolicyPath,
                    BaselinePath = options.BaselinePath!,
                    Mode = options.Mode,
                    ConditionSetName = options.ConditionSetName,
                    ContractIds = options.ContractIds,
                    BasePolicyContext = options.BaseContextPath is null
                        ? null
                        : ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.BaseContextPath)),
                    CurrentPolicyContext = options.CurrentContextPath is null
                        ? null
                        : ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.CurrentContextPath)),
                    PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                    NoRestore = options.NoRestore,
                    RequestedConfiguration = options.Configuration,
                    RequestedTargetFramework = options.TargetFramework,
                    RequestedPlatform = options.Platform,
                    RequestedRuntimeIdentifier = options.RuntimeIdentifier,
                    CancellationToken = cancellationToken,
                },
            });

            console.Out.WriteLine(options.Format == "json"
                ? runtime.FormatHealthAsJson(outcome)
                : runtime.FormatHealthAsHuman(outcome));

            return outcome.Gate switch
            {
                ArchitectureHealthGate.Pass => CliExitCodes.Success,
                ArchitectureHealthGate.Fail => CliExitCodes.ValidationFailure,
                ArchitectureHealthGate.Unassessable => CliExitCodes.InvalidArgumentsOrRuntimeError,
                _ => CliExitCodes.InvalidArgumentsOrRuntimeError,
            };
        }
        catch (OperationCanceledException)
        {
            CliErrorOutputWriter.Write(console, options.Format, "cancelled", "Architecture health evaluation was cancelled.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception exception)
        {
            CliErrorOutputWriter.Write(console, options.Format, "architecture-health-error", $"Architecture health error: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private bool TryValidateFormat(string format)
    {
        if (format is "human" or "json")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-format", "Invalid format. Use 'human' or 'json'.");
        return false;
    }

    private bool TryValidateContexts(HealthCommandOptions options)
    {
        bool hasBase = !string.IsNullOrWhiteSpace(options.BaseContextPath);
        bool hasCurrent = !string.IsNullOrWhiteSpace(options.CurrentContextPath);
        if (hasBase != hasCurrent)
        {
            CliErrorOutputWriter.Write(console, options.Format, "missing-policy-context", "Both --base-context and --current-context are required together.");
            return false;
        }

        if (hasBase && (!fileSystem.FileExists(options.BaseContextPath!) || !fileSystem.FileExists(options.CurrentContextPath!)))
        {
            CliErrorOutputWriter.Write(console, options.Format, "missing-policy-context", "Both policy-context artifact files must exist.");
            return false;
        }

        return true;
    }
}
