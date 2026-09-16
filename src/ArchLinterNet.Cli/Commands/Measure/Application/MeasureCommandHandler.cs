using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Measure.Application;

internal sealed class MeasureCommandHandler(
    ICliRuntime runtime,
    ICliConsole console,
    CancellationToken cancellationToken = default)
{
    private const int DefaultMaxContributors = 20;
    private const string InvalidArgumentsReason = "invalid-arguments";

    public int Execute(MeasureCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(MeasureCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (!TryValidate(options))
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
            CliErrorOutputWriter.Write(console, options.Format, InvalidArgumentsReason,
                $"Invalid format: {options.Format}. Use 'human' or 'json'.");
            return false;
        }

        if (options.MaxContributors is <= 0)
        {
            CliErrorOutputWriter.Write(console, options.Format, InvalidArgumentsReason,
                "--max-contributors must be a positive integer.");
            return false;
        }

        if (options.AllContributors && options.MaxContributors is not null)
        {
            CliErrorOutputWriter.Write(console, options.Format, InvalidArgumentsReason,
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
            CancellationToken = cancellationToken,
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
        };
        ArchitectureMetricMeasurementOutcome outcome = runtime.Measure(request, timing: null);
        int maxContributors = options.MaxContributors ?? DefaultMaxContributors;

        console.Out.WriteLine(options.Format == "json"
            ? MeasureReportFormatter.FormatJson(outcome, maxContributors, options.AllContributors)
            : MeasureReportFormatter.FormatHuman(outcome, maxContributors, options.AllContributors));

        return outcome.Measurements.All(static measurement => measurement.IsEvaluable)
            ? CliExitCodes.Success
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int WriteArgumentError(MeasureCommandOptions options, ArgumentException exception)
    {
        CliErrorOutputWriter.Write(console, options.Format, InvalidArgumentsReason, exception.Message);
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int WriteUnexpectedError(MeasureCommandOptions options, Exception exception)
    {
        if (options.Format == "json" && PolicyDiagnosticOutputWriter.TryWriteJson(console, exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (PolicyDiagnosticOutputWriter.TryWriteHuman(console, "Measure error", exception))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Measure error: {exception.Message}");
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
