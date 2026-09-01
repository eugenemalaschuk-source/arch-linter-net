using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate.Application;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal sealed class TopologyCommandHandler(
    ICliRuntime runtime,
    ICliConsole console,
    IFileSystem fileSystem,
    CancellationToken cancellationToken = default)
{
    private const string HumanFormat = "human";
    private const string JsonFormat = "json";

    public int Capture(TopologyCaptureCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(TopologyCommandHelpTexts.Capture);
            return CliExitCodes.Success;
        }

        if (!TopologyCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict)
            || !TopologyCommandGuards.TryValidateSubjectKind(console, options.Format, options.SubjectKind))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitectureTopologyCaptureOutcome outcome = runtime.CaptureTopology(new ArchitectureTopologyCaptureRequest
            {
                PolicyPath = options.PolicyPath,
                SubjectKind = options.SubjectKind,
                ConditionSetName = options.ConditionSetName,
                PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                NoRestore = options.NoRestore,
                RequestedConfiguration = options.Configuration,
                RequestedTargetFramework = options.TargetFramework,
                RequestedPlatform = options.Platform,
                RequestedRuntimeIdentifier = options.RuntimeIdentifier,
                MaxParallelism = options.MaxParallelism,
                CancellationToken = cancellationToken,
            });

            string? collision = TopologyCommandGuards.FindCaptureOutputCollision(
                options.OutputPath, options.PolicyPath, outcome);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            string document = options.Format == JsonFormat
                ? TopologyCaptureRenderer.FormatJson(outcome)
                : TopologyCaptureRenderer.FormatHuman(outcome);
            int writeResult = Publish(document, options.OutputPath, options.Format, "topology capture");
            return writeResult != CliExitCodes.Success
                ? writeResult
                : outcome.PreflightBlocked ? CliExitCodes.InvalidArgumentsOrRuntimeError : CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return WriteError(options.Format, "cancelled", "Topology capture was cancelled.");
        }
        catch (Exception exception)
        {
            return WriteException(options.Format, "Topology capture error", exception);
        }
    }

    public int Diff(TopologyDiffCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(TopologyCommandHelpTexts.Diff);
            return CliExitCodes.Success;
        }

        if (!TopologyCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TopologyCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ValidationOutcome outcome = runtime.Validate(BuildValidationRequest(
                options.PolicyPath, options.Mode, options.ConditionSetName, options.BaselinePath,
                options.ContractIds, options.EnsureBuilt, options.NoRestore, options.Configuration,
                options.TargetFramework, options.Platform, options.RuntimeIdentifier, options.MaxParallelism), null);

            if (outcome.PreflightBlocked)
            {
                CliErrorOutputWriter.WritePreflightFailure(
                    console, options.Format,
                    "Topology diff could not run because build-state preflight was blocked.",
                    outcome.PreflightDiagnostics);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitectureTopologyMappingEvidence? evidence = FindTopologyEvidence(outcome);
            if (evidence is null)
            {
                return WriteError(options.Format, "no-declared-topology",
                    "Topology diff requires a declared topology in the policy.");
            }

            TopologyDiffReport report = CreateDiffReport(options.Mode, evidence);
            string? collision = TopologyCommandGuards.FindValidationOutputCollision(
                options.OutputPath, options.PolicyPath, outcome, options.BaselinePath);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            string document = options.Format == JsonFormat
                ? TopologyDiffRenderer.FormatJson(report)
                : TopologyDiffRenderer.FormatHuman(report);
            // Diff is intentionally a review projection: observed drift does not become a second
            // success/failure criterion. Only input/runtime/output errors return code 2.
            return Publish(document, options.OutputPath, options.Format, "topology diff");
        }
        catch (OperationCanceledException)
        {
            return WriteError(options.Format, "cancelled", "Topology diff was cancelled.");
        }
        catch (Exception exception)
        {
            return WriteException(options.Format, "Topology diff error", exception);
        }
    }

    public int Verify(TopologyVerifyCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(TopologyCommandHelpTexts.Verify);
            return CliExitCodes.Success;
        }

        if (!TopologyCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TopologyCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            // This is the only validation call in verify. The coordinator only renders the
            // returned outcome; it never re-evaluates it or creates a topology result envelope.
            ValidationOutcome outcome = runtime.Validate(BuildValidationRequest(
                options.PolicyPath, options.Mode, options.ConditionSetName, options.BaselinePath,
                options.ContractIds, options.EnsureBuilt, options.NoRestore, options.Configuration,
                options.TargetFramework, options.Platform, options.RuntimeIdentifier, options.MaxParallelism), null);

            if (!outcome.PreflightBlocked && FindTopologyEvidence(outcome) is null)
            {
                return WriteError(options.Format, "no-declared-topology",
                    "Topology verify requires a declared topology in the policy.");
            }

            string? collision = TopologyCommandGuards.FindValidationOutputCollision(
                options.OutputPath, options.PolicyPath, outcome, options.BaselinePath);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            string document = new ReportCoordinator(runtime, console, fileSystem)
                .RenderReportContent(options.Format, isSingleMode: true, new[] { (options.Mode, outcome) });
            int publishResult = Publish(document, options.OutputPath, options.Format, "topology verify");
            return publishResult != CliExitCodes.Success
                ? publishResult
                : ValidateCommandHandler.ResolveValidationExitCode(outcome);
        }
        catch (OperationCanceledException)
        {
            return WriteError(options.Format, "cancelled", "Topology verify was cancelled.");
        }
        catch (Exception exception)
        {
            return WriteException(options.Format, "Topology verify error", exception);
        }
    }

    internal static ArchitectureTopologyMappingEvidence? FindTopologyEvidence(ValidationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArchitectureApplicabilityRecord[] records = outcome.ApplicabilityRecords
            .Where(record => string.Equals(record.Family, "declared_topology", StringComparison.Ordinal)
                || string.Equals(record.ControlIdentity, "declared-topology", StringComparison.Ordinal))
            .ToArray();
        if (records.Length > 1)
        {
            throw new InvalidOperationException("Validation produced more than one declared-topology applicability record.");
        }

        return records.SingleOrDefault()?.TopologyEvidence;
    }

    internal static TopologyDiffReport CreateDiffReport(
        string mode,
        ArchitectureTopologyMappingEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArchitectureTopologySubjectEvidence[] structural = evidence.Subjects
            .Where(subject => string.Equals(subject.Disposition, "ambiguous", StringComparison.Ordinal))
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .ToArray();
        ArchitectureTopologySubjectEvidence[] unmapped = evidence.Subjects
            .Where(subject => string.Equals(subject.Disposition, "unmapped", StringComparison.Ordinal)
                && string.Equals(evidence.Mode, "exhaustive", StringComparison.Ordinal))
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .ToArray();
        ArchitectureTopologySubjectEvidence[] reviewed = evidence.Subjects
            .Where(subject => string.Equals(subject.Disposition, "reviewed_out_of_scope", StringComparison.Ordinal))
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .ToArray();
        ArchitectureTopologyRelationEvidence[] relational = evidence.Relationships
            .Where(relationship => !relationship.IsAllowed)
            .OrderBy(relationship => relationship.SourceNode, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetNode, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Witness, StringComparer.Ordinal)
            .ToArray();
        return new TopologyDiffReport(mode, evidence, structural, relational, unmapped, reviewed);
    }

    private ValidationRequest BuildValidationRequest(
        string policyPath,
        string mode,
        string? conditionSetName,
        string? baselinePath,
        IReadOnlyList<string> contractIds,
        bool ensureBuilt,
        bool noRestore,
        string? configuration,
        string? targetFramework,
        string? platform,
        string? runtimeIdentifier,
        int? maxParallelism) => new()
        {
            PolicyPath = policyPath,
            Mode = mode,
            ConditionSetName = conditionSetName,
            BaselinePath = baselinePath,
            ContractIds = contractIds.ToList(),
            EnforceUnmatchedIgnoredViolationsPolicy = true,
            PreparationMode = ensureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = noRestore,
            RequestedConfiguration = configuration,
            RequestedTargetFramework = targetFramework,
            RequestedPlatform = platform,
            RequestedRuntimeIdentifier = runtimeIdentifier,
            MaxParallelism = maxParallelism,
            CancellationToken = cancellationToken,
        };

    private int Publish(string document, string? outputPath, string format, string operation)
    {
        try
        {
            if (outputPath is null)
            {
                console.Out.WriteLine(document);
            }
            else
            {
                fileSystem.WriteAllText(outputPath, document);
            }

            return CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            return WriteError(format, "output-write-failed", $"Could not write {operation} output: {exception.Message}");
        }
    }

    private int WriteException(string format, string prefix, Exception exception)
    {
        if (format == JsonFormat && PolicyDiagnosticOutputWriter.TryWriteJson(console, exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (format == HumanFormat && PolicyDiagnosticOutputWriter.TryWriteHuman(console, prefix, exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return WriteError(format, "unexpected-tool-failure", $"{prefix}: {exception.Message}");
    }

    private int WriteError(string format, string category, string message)
    {
        CliErrorOutputWriter.Write(console, format, category, message);
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
