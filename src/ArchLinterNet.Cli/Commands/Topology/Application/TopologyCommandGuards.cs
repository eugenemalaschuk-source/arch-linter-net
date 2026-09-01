using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal static class TopologyCommandGuards
{
    internal static bool TryValidateMode(ICliConsole console, string format, string mode)
    {
        if (mode is "strict" or "audit")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-arguments",
            $"Invalid mode: {mode}. Use 'strict' or 'audit'.");
        return false;
    }

    internal static bool TryValidateFormat(ICliConsole console, string format, bool hasFormatConflict = false)
    {
        if (hasFormatConflict)
        {
            CliErrorOutputWriter.Write(console, format, "invalid-arguments",
                "Specify either --format or --json, not both.");
            return false;
        }

        if (format is HumanFormat or JsonFormat)
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-format",
            $"Invalid format: {format}. Use 'human' or 'json'.");
        return false;
    }

    internal static bool TryValidateSubjectKind(ICliConsole console, string format, string subjectKind)
    {
        if (subjectKind is "type" or "namespace" or "project" or "assembly")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-arguments",
            $"Invalid subject kind: {subjectKind}. Use 'type', 'namespace', 'project', or 'assembly'.");
        return false;
    }

    internal static string? FindCaptureOutputCollision(
        string? outputPath,
        string policyPath,
        ArchitectureTopologyCaptureOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outputPath is null)
        {
            return null;
        }

        List<(string Name, string? Path)> inputs = [("--policy", policyPath)];
        inputs.AddRange(outcome.PolicyImportPaths.Select(path => ("an imported policy file", (string?)path)));
        inputs.AddRange(outcome.ResolvedAssemblyPaths.SelectMany(path => new[]
        {
            ("a build artifact loaded during this run", (string?)path),
            ("a build receipt loaded during this run", (string?)BuildReceiptStore.ReceiptPathFor(path)),
        }));
        inputs.AddRange(outcome.DiscoveredProjectPaths.Select(path => ("a project file loaded during this run", (string?)path)));
        return FindOutputCollision(outputPath, inputs.ToArray());
    }

    internal static string? FindValidationOutputCollision(
        string? outputPath,
        ValidationOutcome outcome,
        string? baselinePath) => FindValidationOutputCollision(outputPath, null, outcome, baselinePath);

    internal static string? FindValidationOutputCollision(
        string? outputPath,
        string? policyPath,
        ValidationOutcome outcome,
        string? baselinePath)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outputPath is null)
        {
            return null;
        }

        List<(string Name, string? Path)> inputs = new();
        if (policyPath is not null)
        {
            inputs.Add(("--policy", policyPath));
        }

        inputs.AddRange(outcome.PolicyImportPaths.Select(path => ("an imported policy file", (string?)path)));
        inputs.AddRange(outcome.ResolvedAssemblyPaths.SelectMany(path => new[]
        {
            ("a build artifact loaded during this run", (string?)path),
            ("a build receipt loaded during this run", (string?)BuildReceiptStore.ReceiptPathFor(path)),
        }));
        inputs.AddRange(outcome.DiscoveredProjectPaths.Select(path => ("a project file loaded during this run", (string?)path)));
        if (baselinePath is not null)
        {
            inputs.Add(("--baseline", baselinePath));
        }

        return FindOutputCollision(outputPath, inputs.ToArray());
    }

    private static string? FindOutputCollision(
        string outputPath,
        params (string Name, string? Path)[] inputPaths)
    {
        string output = Path.GetFullPath(outputPath);
        foreach ((string name, string? inputPath) in inputPaths)
        {
            if (inputPath is not null
                && string.Equals(output, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
            {
                return $"--output destination '{outputPath}' matches {name} input '{inputPath}'";
            }
        }

        return null;
    }

    private const string HumanFormat = "human";
    private const string JsonFormat = "json";
}
