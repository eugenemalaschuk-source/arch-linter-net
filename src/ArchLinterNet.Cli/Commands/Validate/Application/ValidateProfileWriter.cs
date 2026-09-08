using System.Diagnostics;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Owns opt-in profile measurements and publication. It is the only owner of the invocation
// allocation baseline; profile writes do not stage or mutate validation/report output.
internal sealed class ValidateProfileWriter
{
    private const string ProfileDestinationStdout = "stdout";
    private const string ProfileDestinationStderr = "stderr";
    private const string CancellationCountersDataKey = "ArchLinterNet.AnalysisProfile.Counters";
    private const string CancellationInputPathsDataKey = "ArchLinterNet.AnalysisProfile.InputPaths";

    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly long _allocatedBytesAtStart;

    public ValidateProfileWriter(ICliConsole console, IFileSystem fileSystem, long allocatedBytesAtStart)
    {
        _console = console;
        _fileSystem = fileSystem;
        _allocatedBytesAtStart = allocatedBytesAtStart;
    }

    internal void WriteCancelledProfile(ValidateCommandOptions options, ValidateProfileExecutionState state)
    {
        WriteProfile(options, state, AnalysisProfileCompletionStatus.Cancelled, cancellationObserved: true);
    }

    internal void WriteProfile(
        ValidateCommandOptions options,
        ValidateProfileExecutionState state,
        AnalysisProfileCompletionStatus completionStatus,
        bool cancellationObserved)
    {
        if (options.ProfileDestination is null)
        {
            return;
        }

        AnalysisProfileMeasurements measurements = CaptureMeasurements();
        if (IsProfileInputCollision(options, state.InputPaths))
        {
            return;
        }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            state.Counters ?? new ArchitectureAnalysisSnapshotCounters(),
            state.Timing,
            state.RenderedSinkCount,
            ResolveOutputSinkCount(options),
            completionStatus,
            cancellationObserved,
            new AnalysisProfileBuildOptions
            {
                Measurements = measurements,
                Output = state.Output,
                Cache = ValidateCacheCoordinator.BuildCacheProfileCounters(options, state.Cache, state.Counters),
            });

        WriteProfileToDestination(options.ProfileDestination, AnalysisProfileJsonWriter.Write(profile));
    }

    internal void CaptureCancelledProfileState(ValidateProfileExecutionState state, OperationCanceledException exception)
    {
        if (exception.Data[CancellationCountersDataKey] is not ArchitectureAnalysisSnapshotCounters counters
            || exception.Data[CancellationInputPathsDataKey] is not IReadOnlyList<string> inputPaths)
        {
            return;
        }

        state.Counters = counters;
        state.InputPaths = CreateProfileInputPaths(inputPaths);
    }

    internal static string[] CreateProfileInputPaths(IEnumerable<string> inputPaths)
    {
        return inputPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static AnalysisProfileOutput CreateOutputProfile(RouteResult result)
    {
        int committedSinkCount = result.CommittedPaths
            .Concat(result.DeliveredStreamPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return new AnalysisProfileOutput
        {
            CommittedSinkCount = committedSinkCount,
            FailedSinkCount = result.FailedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            StagedSinkCount = result.StagedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            UncommittedSinkCount = result.UncommittedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            OutputFailed = result.FailedPaths.Count > 0,
        };
    }

    internal static AnalysisProfileCompletionStatus ResolveCompletionStatus(
        ValidationOutcome outcome, bool cancelled)
    {
        return ResolveCompletionStatus(outcome.PreflightBlocked, outcome.Passed, cancelled);
    }

    internal static AnalysisProfileCompletionStatus ResolveCompletionStatus(
        bool preflightBlocked, bool passed, bool cancelled)
    {
        if (cancelled)
        {
            return AnalysisProfileCompletionStatus.Cancelled;
        }

        if (preflightBlocked)
        {
            return AnalysisProfileCompletionStatus.PreparationFailure;
        }

        return passed ? AnalysisProfileCompletionStatus.Success : AnalysisProfileCompletionStatus.ValidationFailure;
    }

    private bool IsProfileInputCollision(ValidateCommandOptions options, IReadOnlyList<string> inputPaths)
    {
        if (!ValidateCommandPreflight.TryGetProfileFilePath(options, out string? profilePath))
        {
            return false;
        }

        string? inputPath = inputPaths.FirstOrDefault(path =>
            string.Equals(profilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (inputPath is null)
        {
            return false;
        }

        _console.Error.WriteLine(
            $"--profile destination '{options.ProfileDestination}' matches input file '{inputPath}'; profile was not written");
        return true;
    }

    private AnalysisProfileMeasurements CaptureMeasurements()
    {
        long peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
        return new AnalysisProfileMeasurements
        {
            PeakWorkingSetBytes = peakWorkingSetBytes > 0 ? peakWorkingSetBytes : null,
            AllocatedBytesTotal = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - _allocatedBytesAtStart),
        };
    }

    private void WriteProfileToDestination(string destination, string json)
    {
        switch (destination)
        {
            case ProfileDestinationStdout:
                _console.Out.WriteLine(json);
                break;
            case ProfileDestinationStderr:
                _console.Error.WriteLine(json);
                break;
            default:
                _fileSystem.WriteAllText(destination, json);
                break;
        }
    }

    private static int ResolveOutputSinkCount(ValidateCommandOptions options)
    {
        return options.AdditionalSinks.Count > 0 ? options.AdditionalSinks.Count : 1;
    }
}
