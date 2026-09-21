using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Baseline.Application;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Gate.Application;

internal sealed class GateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    private const string HelpText =
        """
        arch-linter-net gate — fail CI on new architecture debt and error-severity policy weakening

        Usage:
          arch-linter-net gate --policy <path> --baseline <path> [options]

        Options:
          -p, --policy <path>          Architecture policy (default: architecture/dependencies.arch.yml)
              --baseline <path>        Reviewed exact persistent-debt baseline (required)
          -m, --mode <mode>            strict, audit, or all (default: all)
              --contract <id>          Restrict complete analysis to a contract (repeatable)
              --condition-set <name>   Select policy condition set
              --base-context <path>    Base effective-policy context JSON (requires --current-context)
              --current-context <path> Current effective-policy context JSON (requires --base-context)
              --public-api-approval <path> Exact reviewed public API addition approval artifact
              --ensure-built           Build and receipt-verify before complete candidate collection
              --no-restore             Do not restore while ensuring build state
              --configuration <name>   Requested build configuration
              --framework <tfm>        Requested target framework
              --platform <platform>    Requested platform
              --runtime <rid>          Requested runtime identifier
              --profile <path>         Write analysis-profile/v1 counters to a file, stdout, or stderr
          -f, --format <fmt>           human, json, or sarif (default: human)
          -h, --help                   Show this help message

        The gate is read-only. It does not update, prune, generate, or approve a baseline,
        and it does not introduce another validation mode. Current architecture candidates are
        compared completely against the explicit baseline; policy weakening is a separate optional
        change-time guardrail section.

        Exit codes:
          0   Gate passed
          1   New/resolved/stale/ambiguous persistent debt or error-severity policy weakening
          2   Invalid arguments, unreadable input, or failed comparison/preflight
        """;

    public int Execute(ArchitectureAnalysisCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (!BaselineCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TryValidateFormat(options.Format)
            || !BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "gate", options.BaselinePath)
            || !BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            || !BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath ?? string.Empty)
            || !ArchitectureAnalysisCommandSupport.TryValidateContexts(console, fileSystem, options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        (string Name, string? Path)[] profileDeclaredInputs =
            AnalysisProfilePublisher.CreateTrustedInputManifest(
                ArchitectureAnalysisInputPaths.Empty,
                ("--policy", options.PolicyPath),
                ("--baseline", options.BaselinePath),
                ("--base-context", options.BaseContextPath),
                ("--current-context", options.CurrentContextPath),
                ("--public-api-approval", options.PublicApiApprovalPath));
        if (!AnalysisProfilePublisher.TryValidateDestination(
                options.ProfileDestination,
                console,
                profileDeclaredInputs))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            (ArchitectureDebtGateOutcome outcome, ArchitectureAnalysisSnapshotCounters counters) = runtime.EvaluateDebtGateWithCounters(
                ArchitectureAnalysisCommandSupport.CreateDebtGateRequest(options, fileSystem, cancellationToken));
            console.Out.WriteLine(options.Format switch
            {
                "json" => runtime.FormatDebtGateAsJson(outcome),
                "sarif" => runtime.FormatDebtGateAsSarif(outcome),
                _ => runtime.FormatDebtGateAsHuman(outcome),
            });
            (string Name, string? Path)[] profileTrustedInputs =
                AnalysisProfilePublisher.CreateTrustedInputManifest(outcome.AnalysisInputs, profileDeclaredInputs);
            if (!AnalysisProfilePublisher.TryValidateDestination(options.ProfileDestination, console, profileTrustedInputs))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            AnalysisProfilePublisher.Write(
                options.ProfileDestination,
                console,
                fileSystem,
                counters,
                !outcome.Succeeded
                    ? AnalysisProfileCompletionStatus.PreparationFailure
                    : outcome.Passed
                        ? AnalysisProfileCompletionStatus.Success
                        : AnalysisProfileCompletionStatus.ValidationFailure,
                profileTrustedInputs);
            if (!outcome.Succeeded)
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (OperationCanceledException)
        {
            CliErrorOutputWriter.Write(console, options.Format, "cancelled", "Architecture debt gate was cancelled.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception exception)
        {
            CliErrorOutputWriter.Write(console, options.Format, "architecture-debt-gate-error", $"Architecture debt gate error: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private bool TryValidateFormat(string format)
    {
        if (format is "human" or "json" or "sarif")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-format", "Invalid format. Use 'human', 'json', or 'sarif'.");
        return false;
    }

}
