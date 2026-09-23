using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Baseline.Application;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Profiling;
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
              --evidence-scope <value>
                                      Current assessment scope for external-evidence context
              --change-snapshot <path>
                                      Write the canonical current change snapshot from this same
                                      immutable analysis session
              --base-context <path>    Base effective-policy context JSON (requires --current-context)
              --current-context <path> Current effective-policy context JSON (requires --base-context)
              --ensure-built           Build and receipt-verify before complete candidate collection
              --use-prepared-receipts  Verify producer-published receipts without building or restoring
              --no-restore             Do not restore while ensuring build state
              --configuration <name>   Requested build configuration
              --framework <tfm>        Requested target framework
              --platform <platform>    Requested platform
              --runtime <rid>          Requested runtime identifier
              --profile <path>         Write analysis-profile/v1 counters to a file, stdout, or stderr
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
        string? externalEvidenceParseError = null,
        string? changeSnapshotOutput = null)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (changeSnapshotOutput is not null && string.IsNullOrWhiteSpace(changeSnapshotOutput))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "invalid-change-snapshot",
                "--change-snapshot must name a non-empty file path.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
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

        (string Name, string? Path)[] profileDeclaredInputs =
            AnalysisProfilePublisher.CreateTrustedInputManifest(
                ArchitectureAnalysisInputPaths.Empty,
                [
                    ("--policy", (string?)options.PolicyPath),
                    ("--baseline", options.BaselinePath),
                    ("--base-context", options.BaseContextPath),
                    ("--current-context", options.CurrentContextPath),
                    ("--public-api-approval", options.PublicApiApprovalPath),
                    .. (externalEvidenceArtifacts ?? Array.Empty<SarifEvidenceArtifactReference>())
                        .Select(artifact => ("--external-evidence", (string?)artifact.Path)),
                ]);
        if (!AnalysisProfilePublisher.TryValidateDestination(options.ProfileDestination, console, profileDeclaredInputs))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (changeSnapshotOutput is not null)
        {
            (string Name, string? Path)[] changeInputs =
            [
                ("--policy", options.PolicyPath),
                ("--baseline", options.BaselinePath),
                ("--base-context", options.BaseContextPath),
                ("--current-context", options.CurrentContextPath),
                ("--public-api-approval", options.PublicApiApprovalPath),
                .. (externalEvidenceArtifacts ?? Array.Empty<SarifEvidenceArtifactReference>())
                    .Select(artifact => ("--external-evidence", (string?)artifact.Path)),
            ];
            string? collision = FindOutputCollision(
                changeSnapshotOutput,
                changeInputs);
            if (collision is not null)
            {
                console.Error.WriteLine(collision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }
        }

        try
        {
            ArchitectureHealthRequest healthRequest = new()
            {
                DebtGate = ArchitectureAnalysisCommandSupport.CreateDebtGateRequest(options, fileSystem, cancellationToken),
                ExecutionContext = executionContext,
                ExternalEvidenceArtifacts = externalEvidenceArtifacts ?? Array.Empty<SarifEvidenceArtifactReference>(),
                ExternalEvidenceAssessmentContext = externalEvidenceAssessmentContext,
            };
            ArchitectureHealthOutcome? outcome = changeSnapshotOutput is null
                ? runtime.EvaluateHealth(healthRequest)
                : EvaluateWithChangeSnapshot(options, healthRequest, changeSnapshotOutput, fileSystem);
            if (outcome is null)
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            console.Out.WriteLine(options.Format == "json"
                ? runtime.FormatHealthAsJson(outcome)
                : runtime.FormatHealthAsHuman(outcome));

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
                outcome.AnalysisCounters,
                outcome.Gate switch
                {
                    ArchitectureHealthGate.Pass => AnalysisProfileCompletionStatus.Success,
                    ArchitectureHealthGate.Fail => AnalysisProfileCompletionStatus.ValidationFailure,
                    _ => AnalysisProfileCompletionStatus.PreparationFailure,
                },
                profileTrustedInputs);

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

    internal int ExecuteRevalidatePublication(HealthRevalidatePublicationCommandOptions options) =>
        new HealthRevalidatePublicationCommandHandler(console, fileSystem).Execute(options);

    private ArchitectureHealthOutcome? EvaluateWithChangeSnapshot(
        ArchitectureAnalysisCommandOptions options,
        ArchitectureHealthRequest healthRequest,
        string changeSnapshotOutput,
        IFileSystem outputFileSystem)
    {
        ArchitectureDebtGateRequest debtGateRequest = healthRequest.DebtGate;
        using ArchitectureAnalysisSnapshot snapshot = runtime.CreateSnapshot(
            new AnalysisSnapshotRequest
            {
                PolicyPath = debtGateRequest.PolicyPath,
                BaselinePath = debtGateRequest.BaselinePath,
                ConditionSetName = debtGateRequest.ConditionSetName,
                ContractIds = debtGateRequest.ContractIds,
                PreparationMode = debtGateRequest.PreparationMode,
                UsePreparedArtifacts = debtGateRequest.UsePreparedArtifacts,
                NoRestore = debtGateRequest.NoRestore,
                RequestedConfiguration = debtGateRequest.RequestedConfiguration,
                RequestedTargetFramework = debtGateRequest.RequestedTargetFramework,
                RequestedPlatform = debtGateRequest.RequestedPlatform,
                RequestedRuntimeIdentifier = debtGateRequest.RequestedRuntimeIdentifier,
                IncludeRepositoryMetrics = true,
                CancellationToken = debtGateRequest.CancellationToken,
            },
            null);

        ArchitectureHealthOutcome outcome = runtime.EvaluateHealth(healthRequest, snapshot);
        string changeMode = options.Mode == "audit" ? "audit" : "strict";
        ValidationOutcome validation = outcome.ValidationOutcomes
            .FirstOrDefault(candidate => candidate.Mode == changeMode)?.Outcome
            ?? throw new InvalidOperationException(
                $"Health did not produce the '{changeMode}' validation receipt required for the change snapshot.");

        BaselineVerifyOutcome baseline = runtime.VerifyBaseline(
            new BaselineVerifyRequest
            {
                PolicyPath = debtGateRequest.PolicyPath,
                BaselinePath = debtGateRequest.BaselinePath,
                Mode = changeMode,
                ConditionSetName = debtGateRequest.ConditionSetName,
                ContractIds = debtGateRequest.ContractIds,
                PreparationMode = debtGateRequest.PreparationMode,
                NoRestore = debtGateRequest.NoRestore,
                RequestedConfiguration = debtGateRequest.RequestedConfiguration,
                RequestedTargetFramework = debtGateRequest.RequestedTargetFramework,
                RequestedPlatform = debtGateRequest.RequestedPlatform,
                RequestedRuntimeIdentifier = debtGateRequest.RequestedRuntimeIdentifier,
                CancellationToken = debtGateRequest.CancellationToken,
            },
            snapshot);
        if (!baseline.Succeeded)
        {
            string detail = baseline.PreflightDiagnostics.Count == 0
                ? "baseline candidates were not complete"
                : "baseline build-state preflight was blocked";
            throw new InvalidOperationException($"Could not create architecture change snapshot: {detail}.");
        }

        ArchitectureChangeSnapshot changeSnapshot = runtime.CreateChangeSnapshot(
            snapshot,
            changeMode,
            validation,
            baseline,
            options.ConditionSetName);
        outputFileSystem.WriteAllText(
            changeSnapshotOutput,
            ArchitectureChangeReports.SerializeSnapshot(changeSnapshot));
        return outcome;
    }

    private static string? FindOutputCollision(
        string outputPath,
        params (string Name, string? Path)[] inputPaths)
    {
        string output;
        try
        {
            output = Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return $"--change-snapshot destination '{outputPath}' is not a valid file path";
        }

        foreach ((string name, string? inputPath) in inputPaths)
        {
            if (inputPath is null)
            {
                continue;
            }

            string protectedPath;
            try
            {
                protectedPath = Path.GetFullPath(inputPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
            {
                continue;
            }

            if (string.Equals(output, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return $"--change-snapshot destination '{outputPath}' matches {name} input '{inputPath}'";
            }
        }

        return null;
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
