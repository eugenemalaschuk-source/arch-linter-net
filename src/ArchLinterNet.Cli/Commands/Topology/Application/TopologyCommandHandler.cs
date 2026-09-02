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
                options.OutputPath, options.PolicyPath, outcome, fileSystem);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            string document = TopologyCaptureRenderer.FormatHuman(outcome);
            if (options.Format == JsonFormat)
            {
                document = TopologyCaptureRenderer.FormatJson(outcome);
            }
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

    public int Diff(TopologyValidationCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(TopologyCommandHelpTexts.Diff);
            return CliExitCodes.Success;
        }

        if (!TopologyCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TopologyCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict)
            || !TryValidateExecutionOptions(options, options.Format))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ValidationOutcome nativeOutcome = runtime.Validate(
                ValidationExecutionSemantics.CreateRequest(options, options.Mode, cacheLocation: null, cancellationToken), null);

            string? collision = TopologyCommandGuards.FindValidationOutputCollision(
                options.OutputPath,
                options.PolicyPath,
                nativeOutcome,
                options.BaselinePath,
                ValidationExecutionSemantics.ResolveExternalEvidencePaths(options, nativeOutcome.RepositoryRoot),
                fileSystem);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            ValidationOutcome outcome = ValidationExecutionSemantics.AttachExternalEvidence(
                options, nativeOutcome, options.Mode, cancellationToken);

            if (outcome.PreflightBlocked)
            {
                CliErrorOutputWriter.WritePreflightFailure(
                    console, options.Format,
                    "Topology diff could not run because build-state preflight was blocked.",
                    outcome.PreflightDiagnostics);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitectureApplicabilityRecord? applicability = FindTopologyApplicabilityRecord(outcome);
            if (applicability?.TopologyEvidence is null)
            {
                return WriteError(options.Format, "no-declared-topology",
                    "Topology diff requires a declared topology in the policy.");
            }

            TopologyDiffReport report = CreateDiffReport(
                options.Mode,
                applicability,
                FindTopologyMembership(outcome, applicability));
            string document = options.Format == JsonFormat
                ? TopologyDiffRenderer.FormatJson(report)
                : TopologyDiffRenderer.FormatHuman(report);
            int publishResult = Publish(document, options.OutputPath, options.Format, "topology diff");
            if (publishResult != CliExitCodes.Success)
            {
                return publishResult;
            }

            // Diff remains a review projection when native evidence can be rendered as a review
            // category. An empty mandatory topology input is different: ordinary validation could
            // not assess the control, so returning success would disguise an incomplete review.
            return report.IsNonReviewableUnassessability
                ? CliExitCodes.InvalidArgumentsOrRuntimeError
                : CliExitCodes.Success;
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

    public int Verify(TopologyValidationCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(TopologyCommandHelpTexts.Verify);
            return CliExitCodes.Success;
        }

        if (!TopologyCommandGuards.TryValidateMode(console, options.Format, options.Mode)
            || !TopologyCommandGuards.TryValidateFormat(console, options.Format, options.HasFormatConflict)
            || !TryValidateExecutionOptions(options, options.Format))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            // This is the only validation call in verify. The coordinator only renders the
            // returned outcome; it never re-evaluates it or creates a topology result envelope.
            ValidationOutcome nativeOutcome = runtime.Validate(
                ValidationExecutionSemantics.CreateRequest(options, options.Mode, cacheLocation: null, cancellationToken), null);

            string? collision = TopologyCommandGuards.FindValidationOutputCollision(
                options.OutputPath,
                options.PolicyPath,
                nativeOutcome,
                options.BaselinePath,
                ValidationExecutionSemantics.ResolveExternalEvidencePaths(options, nativeOutcome.RepositoryRoot),
                fileSystem);
            if (collision is not null)
            {
                return WriteError(options.Format, "output-collision", collision);
            }

            ValidationOutcome outcome = ValidationExecutionSemantics.AttachExternalEvidence(
                options, nativeOutcome, options.Mode, cancellationToken);

            if (!outcome.PreflightBlocked && FindTopologyEvidence(outcome) is null)
            {
                return WriteError(options.Format, "no-declared-topology",
                    "Topology verify requires a declared topology in the policy.");
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

    internal static ArchitectureTopologyMappingEvidence? FindTopologyEvidence(ValidationOutcome outcome) =>
        FindTopologyApplicabilityRecord(outcome)?.TopologyEvidence;

    internal static ArchitectureApplicabilityRecord? FindTopologyApplicabilityRecord(ValidationOutcome outcome)
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

        return records.SingleOrDefault();
    }

    internal static TopologyDiffReport CreateDiffReport(
        string mode,
        ArchitectureApplicabilityRecord applicability,
        ArchitectureApplicabilityMembership? membership)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        ArchitectureTopologyMappingEvidence evidence = applicability.TopologyEvidence
            ?? throw new ArgumentException("Declared-topology applicability requires topology evidence.", nameof(applicability));
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
        return new TopologyDiffReport(mode, applicability, membership, evidence, structural, relational, unmapped, reviewed);
    }

    private static ArchitectureApplicabilityMembership? FindTopologyMembership(
        ValidationOutcome outcome,
        ArchitectureApplicabilityRecord applicability)
    {
        ArchitectureApplicabilityExpectedEntry[] entries = outcome.ApplicabilityExpectedEntries
            .Where(entry => string.Equals(entry.ControlIdentity, applicability.ControlIdentity, StringComparison.Ordinal)
                && string.Equals(entry.Family, applicability.Family, StringComparison.Ordinal))
            .ToArray();
        return entries.Length switch
        {
            0 => null,
            1 => entries[0].Membership,
            _ => throw new InvalidOperationException(
                "Validation produced more than one declared-topology applicability membership entry."),
        };
    }

    private bool TryValidateExecutionOptions(
        TopologyValidationCommandOptions options,
        string format)
    {
        if (options.ExternalEvidenceParseError is { } externalEvidenceParseError)
        {
            WriteError(format, "invalid-arguments", externalEvidenceParseError);
            return false;
        }

        if (!ValidationExecutionSemantics.TryGetWaiverEvaluationDate(
                options.WaiverEvaluationDate, out _, out string? error))
        {
            return WriteError(format, "invalid-arguments", error!) == CliExitCodes.Success;
        }

        return true;
    }

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
                string? temporaryPath = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    temporaryPath = fileSystem.WriteAllTextToTemp(outputPath, document);
                    cancellationToken.ThrowIfCancellationRequested();
                    fileSystem.RenameTempToTarget(temporaryPath, outputPath);
                }
                catch
                {
                    if (temporaryPath is not null)
                    {
                        TryDeleteTemporaryFile(temporaryPath);
                    }

                    throw;
                }
            }

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return WriteError(format, "output-write-failed", $"Could not write {operation} output: {exception.Message}");
        }
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            fileSystem.DeleteFile(temporaryPath);
        }
        catch
        {
            // Cleanup cannot make a failed publication successful and must not hide its cause.
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
