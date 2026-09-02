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
        ArchitectureTopologyCaptureOutcome outcome,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outputPath is null)
        {
            return null;
        }

        return FindOutputCollision(outputPath, fileSystem,
            CreateTrustedInputManifest(policyPath, outcome.PolicyImportPaths, outcome.ResolvedAssemblyPaths,
                outcome.DiscoveredProjectPaths, outcome.ConsumedInputPaths, baselinePath: null));
    }

    internal static string? FindValidationOutputCollision(
        string? outputPath,
        ValidationOutcome outcome,
        string? baselinePath,
        IFileSystem fileSystem) => FindValidationOutputCollision(
            outputPath, null, outcome, baselinePath, Array.Empty<string>(), fileSystem);

    internal static string? FindValidationOutputCollision(
        string? outputPath,
        string? policyPath,
        ValidationOutcome outcome,
        string? baselinePath,
        IReadOnlyList<string> externalEvidencePaths,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outputPath is null)
        {
            return null;
        }

        return FindOutputCollision(outputPath, fileSystem,
            CreateTrustedInputManifest(policyPath, outcome.PolicyImportPaths, outcome.ResolvedAssemblyPaths,
                outcome.DiscoveredProjectPaths, outcome.ConsumedInputPaths, baselinePath, externalEvidencePaths));
    }

    private static string? FindOutputCollision(
        string outputPath,
        IFileSystem fileSystem,
        params (string Name, string? Path)[] inputPaths)
    {
        // A hard link must name an existing filesystem entry. Avoid both costly identity probes
        // and any source discovery when publication is going to create a new destination.
        if (!fileSystem.FileExists(outputPath))
        {
            return null;
        }

        foreach ((string name, string? inputPath) in inputPaths)
        {
            if (inputPath is not null && fileSystem.AreSameExistingFile(outputPath, inputPath))
            {
                return $"--output destination '{outputPath}' matches {name} input '{inputPath}'";
            }
        }

        return null;
    }

    // One manifest feeds every topology output guard. Keeping all trust-read inputs here prevents
    // command-specific additions from silently escaping alias protection.
    private static (string Name, string? Path)[] CreateTrustedInputManifest(
        string? policyPath,
        IReadOnlyList<string> policyImportPaths,
        IReadOnlyList<string> resolvedAssemblyPaths,
        IReadOnlyList<string> discoveredProjectPaths,
        IReadOnlyList<string> consumedInputPaths,
        string? baselinePath,
        IReadOnlyList<string>? externalEvidencePaths = null)
    {
        List<(string Name, string? Path)> inputs = new();
        if (policyPath is not null)
        {
            inputs.Add(("--policy", policyPath));
        }

        inputs.AddRange(policyImportPaths.Select(path => ("an imported policy file", (string?)path)));
        inputs.AddRange(resolvedAssemblyPaths.SelectMany(path => new[]
        {
            ("a build artifact loaded during this run", (string?)path),
            ("a build receipt loaded during this run", (string?)BuildReceiptStore.ReceiptPathFor(path)),
        }));
        inputs.AddRange(discoveredProjectPaths.Select(path => ("a project file loaded during this run", (string?)path)));
        inputs.AddRange(consumedInputPaths.Select(path => ("a source input consumed during this run", (string?)path)));
        if (baselinePath is not null)
        {
            inputs.Add(("--baseline", baselinePath));
        }

        inputs.AddRange((externalEvidencePaths ?? Array.Empty<string>())
            .Select(path => ("an --external-evidence artifact", (string?)path)));

        return inputs.ToArray();
    }
    private const string HumanFormat = "human";
    private const string JsonFormat = "json";
}
