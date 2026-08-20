using System.Globalization;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Change.Application;

internal sealed class ChangeCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int CreateSnapshot(ChangeSnapshotCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net change snapshot --policy <path> --output <path> [--mode strict|audit] [--baseline <path>] [--condition-set <name>]");
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
            }, null);
            ArchitectureGraphOutcome namespaces = runtime.BuildGraph(Request(options, ArchitectureGraphLevel.Namespace));
            ArchitectureGraphOutcome assemblies = runtime.BuildGraph(Request(options, ArchitectureGraphLevel.Assembly));
            IReadOnlyList<string> baselineDebt = options.BaselinePath is null
                ? Array.Empty<string>()
                : runtime.DiffBaseline(new BaselineDiffRequest
                {
                    PolicyPath = options.PolicyPath,
                    BaselinePath = options.BaselinePath,
                    Mode = options.Mode,
                    ConditionSetName = options.ConditionSetName,
                }).Frozen.Select(BaselineIdentity).OrderBy(static value => value, StringComparer.Ordinal).ToArray();
            string? consumedInputCollision = FindSnapshotConsumedInputCollision(options.OutputPath, validation);
            if (consumedInputCollision is not null)
            {
                console.Error.WriteLine(consumedInputCollision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitectureChangeSnapshot snapshot = BuildSnapshot(
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
            console.Out.WriteLine("arch-linter-net change report --base <snapshot> --current <snapshot> [--format human|json] [--output <path>]");
            return CliExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(options.BasePath) || string.IsNullOrWhiteSpace(options.CurrentPath) || options.Format is not ("human" or "json"))
        {
            console.Error.WriteLine("Change report requires --base, --current, and a human or json --format.");
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
            ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);
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

    private static ArchitectureGraphRequest Request(ChangeSnapshotCommandOptions options, ArchitectureGraphLevel level) => new()
    {
        PolicyPath = options.PolicyPath,
        Mode = options.Mode,
        Level = level,
        ConditionSetName = options.ConditionSetName,
    };

    internal static ArchitectureChangeSnapshot BuildSnapshot(
        string mode, ValidationOutcome validation, ArchitectureGraphOutcome namespaceGraph, ArchitectureGraphOutcome assemblyGraph,
        IReadOnlyList<string> baselineDebt, string? conditionSetName = null)
    {
        List<ArchitectureChangeEntry> entries = new();
        entries.AddRange(namespaceGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Namespace)
            .Select(static node => new ArchitectureChangeEntry("namespace", node.Id, node.Id)));
        entries.AddRange(assemblyGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Assembly)
            .Select(static node => new ArchitectureChangeEntry("assembly", node.Id, node.Id)));
        entries.AddRange(validation.DiscoveredProjectPaths.Select(path => Project(validation.RepositoryRoot, path)));
        entries.AddRange(namespaceGraph.Graph.Edges.Select(static edge => Edge("namespace", edge)));
        entries.AddRange(assemblyGraph.Graph.Edges.Select(static edge => Edge("assembly", edge)));
        entries.AddRange(validation.ClassificationRoles.Select(Role));
        entries.AddRange(validation.ClassificationRoles.SelectMany(ContextEntries));
        entries.AddRange(CoverageBlindSpots(validation));

        List<ArchitectureChangeFinding> findings = ArchitectureFindingMapper
            .FromViolations(validation.Violations.Concat(validation.CoverageFindings), mode)
            .Select(static finding => new ArchitectureChangeFinding(
                finding.CanonicalIdentity,
                finding.Kind,
                finding.ContractName))
            .ToList();
        return new ArchitectureChangeSnapshot(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            mode,
            conditionSetName ?? string.Empty,
            entries,
            findings,
            baselineDebt);
    }

    private static ArchitectureChangeEntry Edge(string level, ArchitectureGraphEdge edge) => new(
        "dependency_edge", level + ":" + edge.SourceId + "->" + edge.TargetId,
        level + ": " + edge.SourceId + " -> " + edge.TargetId);

    internal static string CanonicalProjectIdentity(string repositoryRoot, string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        string root = NormalizePath(repositoryRoot).TrimEnd('/');
        string project = NormalizePath(projectPath);
        string rootWithSeparator = root + "/";
        if (!project.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Discovered project path is outside the authoritative repository root.", nameof(projectPath));
        }

        return project[rootWithSeparator.Length..];
    }

    private static ArchitectureChangeEntry Project(string repositoryRoot, string projectPath)
    {
        string identity = CanonicalProjectIdentity(repositoryRoot, projectPath);
        return new ArchitectureChangeEntry("project", identity, identity);
    }

    private static ArchitectureChangeEntry Role(ArchitectureClassificationRoleFact role) => new(
        "semantic_role", role.Subject + "|" + role.Role + "|" + Metadata(role.Metadata),
        role.Subject + " = " + role.Role);

    private static IEnumerable<ArchitectureChangeEntry> ContextEntries(ArchitectureClassificationRoleFact role) => role.Metadata
        .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => new ArchitectureChangeEntry(
            "semantic_context", role.Subject + "|" + entry.Key + "|" + Value(entry.Value),
            role.Subject + ": " + entry.Key + "=" + Value(entry.Value)));

    private static IEnumerable<ArchitectureChangeEntry> CoverageBlindSpots(ValidationOutcome validation) => validation.CoverageSummaries
        .SelectMany(summary => summary.UncoveredItems.Select(item => Coverage("uncovered", summary, item.Item)))
        .Concat(validation.CoverageSummaries.SelectMany(summary => summary.StaleItems.Select(item => Coverage("stale", summary, item.Item))))
        .Concat(validation.CoverageSummaries.SelectMany(summary => summary.UnknownItems.Select(item => Coverage("unknown", summary, item.Item))));

    private static ArchitectureChangeEntry Coverage(string state, ArchitectureCoverageSummary summary, string item) => new(
        "coverage_blind_spot", (summary.ContractId ?? summary.ContractName) + "|" + summary.Scope + "|" + state + "|" + item,
        state + " " + summary.Scope + ": " + item);

    private static string BaselineIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        ArchitectureViolationIdentity? identity = entry.Identity;
        return identity is null
            ? throw new ArgumentException("Frozen baseline debt must have an authoritative identity.", nameof(entry))
            : ArchitectureViolationIdentityJson.Serialize(identity);
    }

    private static string Metadata(IReadOnlyDictionary<string, object> metadata) => string.Join(";", metadata
        .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => entry.Key + "=" + Value(entry.Value)));

    private static string Value(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

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
