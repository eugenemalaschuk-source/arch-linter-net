using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Infrastructure;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Measure.Application;

internal sealed class MeasureCommandHandler
{
    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly CancellationToken _cancellationToken;

    public MeasureCommandHandler(ICliRuntime runtime, ICliConsole console, CancellationToken cancellationToken = default)
        : this(runtime, console, new FileSystem(), cancellationToken)
    {
    }

    public MeasureCommandHandler(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        _runtime = runtime;
        _console = console;
        _fileSystem = fileSystem;
        _cancellationToken = cancellationToken;
    }

    private const int DefaultMaxContributors = 20;
    private const string InvalidArgumentsReason = "invalid-arguments";

    public int Execute(MeasureCommandOptions options)
    {
        if (options.ShowHelp)
        {
            _console.Out.WriteLine(MeasureCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (!TryValidate(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!AnalysisProfilePublisher.TryValidateDestination(
                options.ProfileDestination,
                _console,
                ProfileDeclaredInputs(options)))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            return ExecuteCore(options);
        }
        catch (ArgumentException ex)
        {
            return WriteArgumentError(options, ex);
        }
        catch (Exception ex)
        {
            return WriteUnexpectedError(options, ex);
        }
    }

    private bool TryValidate(MeasureCommandOptions options)
    {
        if (options.Format is not ("human" or "json"))
        {
            CliErrorOutputWriter.Write(_console, options.Format, InvalidArgumentsReason,
                $"Invalid format: {options.Format}. Use 'human' or 'json'.");
            return false;
        }

        if (options.MaxContributors is <= 0)
        {
            CliErrorOutputWriter.Write(_console, options.Format, InvalidArgumentsReason,
                "--max-contributors must be a positive integer.");
            return false;
        }

        if (options.AllContributors && options.MaxContributors is not null)
        {
            CliErrorOutputWriter.Write(_console, options.Format, InvalidArgumentsReason,
                "--max-contributors and --all-contributors cannot be used together.");
            return false;
        }

        return true;
    }

    private int ExecuteCore(MeasureCommandOptions options)
    {
        ArchitectureMetricMeasurementRequest request = new()
        {
            PolicyPath = options.PolicyPath,
            MetricIds = options.MetricIds.Count == 0 ? null : options.MetricIds,
            ConditionSetName = options.ConditionSetName,
            CancellationToken = _cancellationToken,
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
        };
        (ArchitectureMetricMeasurementOutcome outcome, ArchitectureAnalysisSnapshotCounters counters) =
            _runtime.MeasureWithCounters(request, timing: null);
        int maxContributors = options.MaxContributors ?? DefaultMaxContributors;

        _console.Out.WriteLine(options.Format == "json"
            ? MeasureReportFormatter.FormatJson(outcome, maxContributors, options.AllContributors)
            : MeasureReportFormatter.FormatHuman(outcome, maxContributors, options.AllContributors));

        bool complete = outcome.Measurements.All(static measurement => measurement.IsEvaluable);
        (string Name, string? Path)[] profileTrustedInputs =
            AnalysisProfilePublisher.CreateTrustedInputManifest(outcome.AnalysisInputs, ProfileDeclaredInputs(options));
        if (!AnalysisProfilePublisher.TryValidateDestination(options.ProfileDestination, _console, profileTrustedInputs))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        AnalysisProfilePublisher.Write(
            options.ProfileDestination,
            _console,
            _fileSystem,
            counters,
            complete ? AnalysisProfileCompletionStatus.Success : AnalysisProfileCompletionStatus.ValidationFailure,
            profileTrustedInputs);
        return complete
            ? CliExitCodes.Success
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int WriteArgumentError(MeasureCommandOptions options, ArgumentException exception)
    {
        CliErrorOutputWriter.Write(_console, options.Format, InvalidArgumentsReason, exception.Message);
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private static (string Name, string? Path)[] ProfileDeclaredInputs(MeasureCommandOptions options) =>
        AnalysisProfilePublisher.CreateTrustedInputManifest(
            ArchitectureAnalysisInputPaths.Empty,
            ("--policy", options.PolicyPath));

    private int WriteUnexpectedError(MeasureCommandOptions options, Exception exception)
    {
        if (options.Format == "json" && PolicyDiagnosticOutputWriter.TryWriteJson(_console, exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (PolicyDiagnosticOutputWriter.TryWriteHuman(_console, "Measure error", exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        CliErrorOutputWriter.Write(_console, options.Format, "unexpected-tool-failure", $"Measure error: {exception.Message}");
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
