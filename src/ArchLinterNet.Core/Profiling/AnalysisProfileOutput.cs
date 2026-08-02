namespace ArchLinterNet.Core.Profiling;

// Publication evidence for the profile's host. Counters.OutputSinkCount is the requested
// destination count; this record states what actually happened to those destinations.
public sealed record AnalysisProfileOutput
{
    public required int CommittedSinkCount { get; init; }

    public required int FailedSinkCount { get; init; }

    public required int StagedSinkCount { get; init; }

    public required int UncommittedSinkCount { get; init; }

    public required bool OutputFailed { get; init; }
}
