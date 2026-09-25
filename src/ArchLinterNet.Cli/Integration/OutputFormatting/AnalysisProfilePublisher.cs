using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Integration.OutputFormatting;

// Publishes the deterministic counter portion of analysis-profile/v1 for commands whose
// analysis is owned by a Core snapshot but whose output is not the validate command's report
// pipeline. The counters always come from the command's own Core execution.
internal static class AnalysisProfilePublisher
{
    internal static (string Name, string? Path)[] CreateTrustedInputManifest(
        ArchitectureAnalysisInputPaths analysisInputs,
        params (string Name, string? Path)[] declaredInputs)
    {
        ArgumentNullException.ThrowIfNull(analysisInputs);

        List<(string Name, string? Path)> inputs = [.. declaredInputs];
        inputs.AddRange(analysisInputs.PolicyImportPaths.Select(path => ("an imported policy file", (string?)path)));
        inputs.AddRange(analysisInputs.ResolvedAssemblyPaths.SelectMany(path => new[]
        {
            ("a build artifact loaded during this run", (string?)path),
            ("a build receipt loaded during this run", (string?)BuildReceiptStore.ReceiptPathFor(path)),
        }));
        inputs.AddRange(analysisInputs.DiscoveredProjectPaths.Select(path =>
            ("a project file loaded during this run", (string?)path)));
        inputs.AddRange(analysisInputs.ConsumedInputPaths.Select(path =>
            ("a source input consumed during this run", (string?)path)));
        return inputs.ToArray();
    }

    public static void Write(
        string? destination,
        ICliConsole console,
        IFileSystem fileSystem,
        ArchitectureAnalysisSnapshotCounters counters,
        AnalysisProfileCompletionStatus completionStatus,
        params (string Name, string? Path)[] protectedPaths)
    {
        Write(
            destination,
            console,
            fileSystem,
            counters,
            completionStatus,
            timing: null,
            measurements: null,
            protectedPaths: protectedPaths);
    }

    internal static void Write(
        string? destination,
        ICliConsole console,
        IFileSystem fileSystem,
        ArchitectureAnalysisSnapshotCounters counters,
        AnalysisProfileCompletionStatus completionStatus,
        ValidationTiming? timing,
        AnalysisProfileMeasurements? measurements,
        params (string Name, string? Path)[] protectedPaths)
    {
        if (destination is null)
        {
            return;
        }

        if (!TryValidateDestination(destination, console, protectedPaths))
        {
            return;
        }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters,
            timing,
            renderedSinkCount: 1,
            outputSinkCount: 1,
            completionStatus,
            cancellationObserved: false,
            new AnalysisProfileBuildOptions
            {
                Measurements = measurements,
            });
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

    internal static bool TryValidateDestination(
        string? destination,
        ICliConsole console,
        params (string Name, string? Path)[] protectedPaths)
    {
        if (destination is null || destination is "stdout" or "stderr")
        {
            return true;
        }

        string destinationPath;
        try
        {
            destinationPath = System.IO.Path.GetFullPath(destination);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            console.Error.WriteLine($"--profile destination '{destination}' is not a valid file path");
            return false;
        }

        foreach ((string name, string? path) in protectedPaths)
        {
            if (path is null)
            {
                continue;
            }

            string protectedPath;
            try
            {
                protectedPath = System.IO.Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
            {
                continue;
            }

            if (string.Equals(destinationPath, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                console.Error.WriteLine(
                    $"--profile destination '{destination}' matches {name} path '{path}'; profile was not written");
                return false;
            }
        }

        return true;
    }
}
