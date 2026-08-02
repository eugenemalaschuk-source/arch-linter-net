using System.Diagnostics;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

// Split out of ValidateCommandHandler.cs (which is already at the file-size lint threshold) —
// issue #374's --profile option. See openspec/specs/analysis-profile/spec.md.
internal sealed partial class ValidateCommandHandler
{
    private const string ProfileDestinationStdout = "stdout";
    private const string ProfileDestinationStderr = "stderr";

    // Snapshot at handler construction so AllocatedBytesTotal measures this command invocation's
    // own allocations, not the process's lifetime total. Process-wide (not thread-local) because
    // validation work is not guaranteed to stay on one thread.
    private readonly long _allocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);

    private sealed class ValidationProfileExecutionState
    {
        public ValidationTiming? Timing { get; set; }

        public ArchitectureAnalysisSnapshotCounters? Counters { get; set; }

        public IReadOnlyList<string> InputPaths { get; set; } = Array.Empty<string>();

        public AnalysisProfileOutput? Output { get; set; }

        public int RenderedSinkCount { get; set; }
    }

    private void WriteCancelledProfile(ValidateCommandOptions options, ValidationProfileExecutionState state)
    {
        WriteProfile(
            options,
            state.Counters ?? new ArchitectureAnalysisSnapshotCounters(),
            state.Timing,
            AnalysisProfileCompletionStatus.Cancelled,
            cancellationObserved: true,
            state.RenderedSinkCount,
            state.Output,
            state.InputPaths);
    }

    // No-op unless --profile was requested — omitting it leaves command output completely
    // unchanged, per openspec/specs/analysis-profile/spec.md, "CLI exposes the profile via a
    // dedicated opt-in option". Writes directly (open/write/close), not staged-then-renamed like
    // ReportCoordinator's file sinks — cancellation-safe atomic publication of the profile artifact
    // itself is issue #418's explicit scope, not #374's (see design.md).
    private void WriteProfile(
        ValidateCommandOptions options,
        ArchitectureAnalysisSnapshotCounters counters,
        ValidationTiming? timing,
        AnalysisProfileCompletionStatus completionStatus,
        bool cancellationObserved,
        int renderedSinkCount,
        AnalysisProfileOutput? output = null,
        IReadOnlyList<string>? inputPaths = null)
    {
        if (options.ProfileDestination is null)
        {
            return;
        }

        AnalysisProfileMeasurements measurements = CaptureMeasurements();

        if (IsProfileInputCollision(options, inputPaths ?? Array.Empty<string>()))
        {
            return;
        }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters,
            timing,
            renderedSinkCount,
            ResolveOutputSinkCount(options),
            completionStatus,
            cancellationObserved,
            new AnalysisProfileBuildOptions
            {
                Measurements = measurements,
                Output = output,
            });

        WriteProfileToDestination(options.ProfileDestination, AnalysisProfileJsonWriter.Write(profile));
    }

    private bool IsProfileInputCollision(ValidateCommandOptions options, IReadOnlyList<string> inputPaths)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath))
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

    private static string[] CreateProfileInputPaths(IEnumerable<string> inputPaths)
    {
        return inputPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AnalysisProfileOutput CreateOutputProfile(RouteResult result)
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

    private static AnalysisProfileCompletionStatus ResolveCompletionStatus(
        ValidationOutcome outcome, bool cancelled)
    {
        return ResolveCompletionStatus(outcome.PreflightBlocked, outcome.Passed, cancelled);
    }

    private static AnalysisProfileCompletionStatus ResolveCompletionStatus(
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
}
