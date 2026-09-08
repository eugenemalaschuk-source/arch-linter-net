using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// One mutable profile projection for one command invocation. The execution and cache
// collaborators update this object; neither creates an independent lifecycle or fact set.
internal sealed class ValidateProfileExecutionState
{
    public ValidationTiming? Timing { get; set; }

    public ArchitectureAnalysisSnapshotCounters? Counters { get; set; }

    public IReadOnlyList<string> InputPaths { get; set; } = Array.Empty<string>();

    public AnalysisProfileOutput? Output { get; set; }

    public int RenderedSinkCount { get; set; }

    public ValidateCacheExecutionState Cache { get; } = new();
}
