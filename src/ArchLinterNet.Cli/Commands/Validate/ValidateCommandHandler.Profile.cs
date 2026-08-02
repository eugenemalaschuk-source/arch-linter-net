using System.Diagnostics;
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
        bool cancellationObserved)
    {
        if (options.ProfileDestination is null)
        {
            return;
        }

        AnalysisProfileMeasurements measurements = new()
        {
            // .NET's Process.PeakWorkingSet64 is a documented no-op returning 0 on some platforms
            // (observed on macOS) rather than throwing — a real process's peak working set is
            // never actually 0, so treat that as "unavailable" and degrade to null explicitly
            // instead of publishing a misleadingly precise zero (issue #374's "unavailable
            // platform metrics must degrade explicitly, not disappear ambiguously").
            PeakWorkingSetBytes = PositiveOrNull(Process.GetCurrentProcess().PeakWorkingSet64),
            AllocatedBytesTotal = GC.GetTotalAllocatedBytes(precise: false) - _allocatedBytesAtStart,
        };

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters,
            timing,
            ResolveRenderedSinkCount(options),
            ResolveOutputSinkCount(options),
            completionStatus,
            cancellationObserved,
            measurements);

        WriteProfileToDestination(options.ProfileDestination, AnalysisProfileJsonWriter.Write(profile));
    }

    private static long? PositiveOrNull(long value) => value > 0 ? value : null;

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

    // Distinct formats actually needing rendering — matches exactly what ReportCoordinator itself
    // dedupes internally (see StdoutOrAnySinkNeeds/ResolveHumanContent et al.): the same format
    // requested for multiple destinations still renders once. Legacy (no --report) always renders
    // exactly one format.
    private static int ResolveRenderedSinkCount(ValidateCommandOptions options)
    {
        return options.AdditionalSinks.Count > 0
            ? options.AdditionalSinks.Select(sink => sink.Format).Distinct().Count()
            : 1;
    }

    private static int ResolveOutputSinkCount(ValidateCommandOptions options)
    {
        return options.AdditionalSinks.Count > 0 ? options.AdditionalSinks.Count : 1;
    }

    private static AnalysisProfileCompletionStatus ResolveCompletionStatus(ValidationOutcome outcome, bool cancelled)
    {
        return ResolveCompletionStatus(outcome.PreflightBlocked, outcome.Passed, cancelled);
    }

    private static AnalysisProfileCompletionStatus ResolveCompletionStatus(bool preflightBlocked, bool passed, bool cancelled)
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
