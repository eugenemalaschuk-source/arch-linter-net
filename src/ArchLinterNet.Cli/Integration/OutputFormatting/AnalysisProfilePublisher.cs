using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Integration.OutputFormatting;

// Publishes the deterministic counter portion of analysis-profile/v1 for commands whose
// analysis is owned by a Core snapshot but whose output is not the validate command's report
// pipeline. The counters always come from the command's own Core execution.
internal static class AnalysisProfilePublisher
{
    public static void Write(
        string? destination,
        ICliConsole console,
        IFileSystem fileSystem,
        ArchitectureAnalysisSnapshotCounters counters,
        AnalysisProfileCompletionStatus completionStatus)
    {
        if (destination is null)
        {
            return;
        }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters,
            timing: null,
            renderedSinkCount: 1,
            outputSinkCount: 1,
            completionStatus,
            cancellationObserved: false);
        string json = AnalysisProfileJsonWriter.Write(profile);
        switch (destination)
        {
            case "stdout":
                console.Out.WriteLine(json);
                break;
            case "stderr":
                console.Error.WriteLine(json);
                break;
            default:
                fileSystem.WriteAllText(destination, json);
                break;
        }
    }
}
