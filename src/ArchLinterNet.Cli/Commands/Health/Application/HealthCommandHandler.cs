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
              --execution-context <id> Correlate a persisted JSON artifact with its change report
              --external-evidence <binding>
                                      Bind a declared external_evidence requirement to a
                                      repository-local SARIF artifact. Repeatable: id=<id>,
                                      path=<path>[,repository=<value>][,revision=<value>][,scope=<value>]
              --evidence-repository <value>
                                      Current repository identity for external-evidence context
              --evidence-revision <value>
                                      Current source revision for external-evidence context
              --evidence-scope <value> Current assessment scope for external-evidence context
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

    public int Execute(
        ArchitectureAnalysisCommandOptions options,
        string? executionContext = null,
        IReadOnlyList<SarifEvidenceArtifactReference>? externalEvidenceArtifacts = null,
        SarifEvidenceAssessmentContext? externalEvidenceAssessmentContext = null,
        string? externalEvidenceParseError = null)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (!BaselineCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TryValidateFormat(options.Format)
            || !TryValidateExecutionContext(executionContext, options.Format)
            || !TryValidateExternalEvidenceParseError(externalEvidenceParseError, options.Format)
            || !BaselineCommandGuards.TryRequireBaselinePath(console, options.Format, "health", options.BaselinePath)
            || !BaselineCommandGuards.TryValidatePolicyFile(console, fileSystem, options.Format, options.PolicyPath)
            || !BaselineCommandGuards.TryValidateBaselineFile(console, fileSystem, options.Format, options.BaselinePath ?? string.Empty)
            || !ArchitectureAnalysisCommandSupport.TryValidateContexts(console, fileSystem, options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitectureHealthOutcome outcome = runtime.EvaluateHealth(new ArchitectureHealthRequest
            {
                DebtGate = ArchitectureAnalysisCommandSupport.CreateDebtGateRequest(options, fileSystem, cancellationToken),
                ExecutionContext = executionContext,
                ExternalEvidenceArtifacts = externalEvidenceArtifacts ?? Array.Empty<SarifEvidenceArtifactReference>(),
                ExternalEvidenceAssessmentContext = externalEvidenceAssessmentContext,
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

    private bool TryValidateExecutionContext(string? executionContext, string format)
    {
        if (executionContext is null || !string.IsNullOrWhiteSpace(executionContext))
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-execution-context", "--execution-context must not be blank.");
        return false;
    }

    private bool TryValidateExternalEvidenceParseError(string? parseError, string format)
    {
        if (parseError is null)
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-external-evidence", parseError);
        return false;
    }

}
