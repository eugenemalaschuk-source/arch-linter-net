using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Change.Application;

internal sealed class ChangeCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int CreateSnapshot(ChangeSnapshotCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net change snapshot --policy <path> --output <path> [--mode strict|audit] [--baseline <path>] [--condition-set <name>] [--ensure-built] [--no-restore] [--configuration <name>] [--framework <tfm>] [--platform <platform>] [--runtime <rid>]");
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit") || string.IsNullOrWhiteSpace(options.OutputPath))
        {
            console.Error.WriteLine("Change snapshot requires --output and a strict or audit --mode.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            string? outputCollision = FindSnapshotOutputCollision(options);
            if (outputCollision is not null)
            {
                console.Error.WriteLine(outputCollision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ValidationOutcome validation = runtime.Validate(new ValidationRequest
            {
                PolicyPath = options.PolicyPath,
                Mode = options.Mode,
                ConditionSetName = options.ConditionSetName,
                BaselinePath = options.BaselinePath,
                PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                NoRestore = options.NoRestore,
                RequestedConfiguration = options.Configuration,
                RequestedTargetFramework = options.TargetFramework,
                RequestedPlatform = options.Platform,
                RequestedRuntimeIdentifier = options.RuntimeIdentifier,
            }, null);
            if (validation.PreflightBlocked)
            {
                return FailIncompleteSnapshot("validation", validation.PreflightDiagnostics);
            }

            if (options.EnsureBuilt && validation.PreparedPostBuildRunner is null)
            {
                return FailIncompleteSnapshot("validation", validation.PreflightDiagnostics);
            }

            ArchitectureGraphOutcome namespaces = runtime.BuildGraph(Request(options, validation, ArchitectureGraphLevel.Namespace));
            ArchitectureGraphOutcome assemblies = runtime.BuildGraph(Request(options, validation, ArchitectureGraphLevel.Assembly));
            IReadOnlyList<ArchitectureBaselineComparisonEntry> baselineDebt = Array.Empty<ArchitectureBaselineComparisonEntry>();
            if (options.BaselinePath is not null)
            {
                BaselineDiffOutcome baseline = runtime.DiffBaseline(new BaselineDiffRequest
                {
                    PolicyPath = options.PolicyPath,
                    BaselinePath = options.BaselinePath,
                    Mode = options.Mode,
                    ConditionSetName = options.ConditionSetName,
                    PreparationMode = BuildPreparationMode.Ordinary,
                    NoRestore = options.NoRestore,
                    RequestedConfiguration = options.Configuration,
                    RequestedTargetFramework = options.TargetFramework,
                    RequestedPlatform = options.Platform,
                    RequestedRuntimeIdentifier = options.RuntimeIdentifier,
                    UsePreparedPostBuildState = options.EnsureBuilt,
                    PreparedPostBuildRunner = validation.PreparedPostBuildRunner,
                });
                if (!baseline.Succeeded)
                {
                    return FailIncompleteSnapshot("baseline debt", baseline.PreflightDiagnostics);
                }

                baselineDebt = baseline.Frozen;
            }
            string? consumedInputCollision = FindSnapshotConsumedInputCollision(options.OutputPath, validation);
            if (consumedInputCollision is not null)
            {
                console.Error.WriteLine(consumedInputCollision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
                options.Mode, validation, namespaces, assemblies, baselineDebt, options.ConditionSetName);
            fileSystem.WriteAllText(options.OutputPath, ArchitectureChangeReports.SerializeSnapshot(snapshot));
            return CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            console.Error.WriteLine($"Could not create architecture change snapshot: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    public int CreateReport(ChangeReportCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net change report --base <snapshot> --current <snapshot> --execution-context <id> [--format human|json] [--output <path>]");
            return CliExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(options.BasePath)
            || string.IsNullOrWhiteSpace(options.CurrentPath)
            || string.IsNullOrWhiteSpace(options.ExecutionContext)
            || options.Format is not ("human" or "json"))
        {
            console.Error.WriteLine("Change report requires --base, --current, --execution-context, and a human or json --format.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            string? outputCollision = FindReportOutputCollision(options);
            if (outputCollision is not null)
            {
                console.Error.WriteLine(outputCollision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitectureChangeSnapshot baseline = ArchitectureChangeReports.DeserializeSnapshot(fileSystem.ReadAllText(options.BasePath));
            ArchitectureChangeSnapshot current = ArchitectureChangeReports.DeserializeSnapshot(fileSystem.ReadAllText(options.CurrentPath));
            ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current, options.ExecutionContext);
            string output = options.Format == "json" ? ArchitectureChangeReports.FormatJson(report) : ArchitectureChangeReports.FormatHuman(report);
            if (options.OutputPath is null)
            {
                console.Out.Write(output);
            }
            else
            {
                fileSystem.WriteAllText(options.OutputPath, output);
            }

            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            console.Error.WriteLine($"Could not create architecture change report: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static ArchitectureGraphRequest Request(
        ChangeSnapshotCommandOptions options,
        ValidationOutcome validation,
        ArchitectureGraphLevel level) => new()
        {
            PolicyPath = options.PolicyPath,
            Mode = options.Mode,
            Level = level,
            ConditionSetName = options.ConditionSetName,
            PreparationMode = BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
            UsePreparedPostBuildState = options.EnsureBuilt,
            PreparedPostBuildRunner = validation.PreparedPostBuildRunner,
        };

    private int FailIncompleteSnapshot(
        string contributor,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics)
    {
        console.Error.WriteLine($"Could not create architecture change snapshot: {contributor} did not produce complete analysis facts.");
        if (diagnostics.Count > 0)
        {
            console.Error.Write(runtime.FormatBuildStatePreflightForHumans(diagnostics));
        }

        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    internal static string? FindSnapshotOutputCollision(ChangeSnapshotCommandOptions options) =>
        FindOutputCollision(options.OutputPath,
            ("--policy", options.PolicyPath),
            ("--baseline", options.BaselinePath));

    internal static string? FindReportOutputCollision(ChangeReportCommandOptions options) => options.OutputPath is null
        ? null
        : FindOutputCollision(options.OutputPath,
            ("--base", options.BasePath),
            ("--current", options.CurrentPath));

    internal static string? FindSnapshotConsumedInputCollision(string outputPath, ValidationOutcome validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return FindOutputCollision(outputPath,
            validation.PolicyImportPaths.Select(static path => ("imported policy file", (string?)path))
                .Concat(validation.ResolvedAssemblyPaths.SelectMany(static path => new[]
                {
                    ("a build artifact loaded during this run", (string?)path),
                    ("a build receipt loaded during this run", (string?)BuildReceiptStore.ReceiptPathFor(path)),
                }))
                .Concat(validation.DiscoveredProjectPaths.Select(static path => ("a project file loaded during this run", (string?)path)))
                .ToArray());
    }

    private static string? FindOutputCollision(string outputPath, params (string Name, string? Path)[] inputPaths)
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
}
