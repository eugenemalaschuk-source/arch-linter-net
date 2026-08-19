using System.Globalization;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
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
            ArchitectureChangeSnapshot snapshot = BuildSnapshot(options.Mode, validation, namespaces, assemblies, baselineDebt);
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

    private static ArchitectureChangeSnapshot BuildSnapshot(
        string mode, ValidationOutcome validation, ArchitectureGraphOutcome namespaceGraph, ArchitectureGraphOutcome assemblyGraph,
        IReadOnlyList<string> baselineDebt)
    {
        List<ArchitectureChangeEntry> entries = new();
        entries.AddRange(namespaceGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Namespace)
            .Select(static node => new ArchitectureChangeEntry("namespace", node.Id, node.Id)));
        entries.AddRange(assemblyGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Assembly)
            .Select(static node => new ArchitectureChangeEntry("assembly", node.Id, node.Id)));
        entries.AddRange(validation.DiscoveredProjectPaths.Select(static path =>
            new ArchitectureChangeEntry("project", path, path)));
        entries.AddRange(namespaceGraph.Graph.Edges.Select(static edge => Edge("namespace", edge)));
        entries.AddRange(assemblyGraph.Graph.Edges.Select(static edge => Edge("assembly", edge)));
        entries.AddRange(validation.ClassificationRoles.Select(Role));
        entries.AddRange(validation.ClassificationRoles.SelectMany(ContextEntries));
        entries.AddRange(CoverageBlindSpots(validation));

        List<ArchitectureChangeFinding> findings = validation.Violations
            .Concat(validation.CoverageFindings)
            .Select(Finding)
            .ToList();
        return new ArchitectureChangeSnapshot(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            mode,
            entries,
            findings,
            baselineDebt);
    }

    private static ArchitectureChangeEntry Edge(string level, ArchitectureGraphEdge edge) => new(
        "dependency_edge", level + ":" + edge.SourceId + "->" + edge.TargetId,
        level + ": " + edge.SourceId + " -> " + edge.TargetId);

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

    private static ArchitectureChangeFinding Finding(ArchitectureViolation violation)
    {
        ArchitectureViolationIdentity? resolved = violation.Identity;
        string identity = resolved is null
            ? string.Join("|", violation.ContractId, violation.SourceType, violation.ForbiddenNamespace,
                string.Join(",", violation.ForbiddenReferences.OrderBy(static value => value, StringComparer.Ordinal)))
            : string.Join("|", resolved.ContractFamily, resolved.Kind, resolved.ContractId, resolved.SourceAssembly,
                resolved.SourceType, resolved.SourceMember, resolved.TargetAssembly, resolved.TargetType,
                resolved.TargetMember, resolved.Occurrence, resolved.Configuration);
        return new ArchitectureChangeFinding(identity, violation.ContractName, violation.SourceType + " -> " + violation.ForbiddenNamespace);
    }

    private static string BaselineIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        ArchitectureViolationIdentity? identity = entry.Identity;
        return identity is null
            ? string.Join("|", entry.ContractGroup, entry.ContractId, entry.SourceType, entry.ForbiddenReference)
            : string.Join("|", identity.ContractFamily, identity.Kind, identity.ContractId, identity.SourceAssembly,
                identity.SourceType, identity.SourceMember, identity.TargetAssembly, identity.TargetType,
                identity.TargetMember, identity.Occurrence, identity.Configuration);
    }

    private static string Metadata(IReadOnlyDictionary<string, object> metadata) => string.Join(";", metadata
        .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => entry.Key + "=" + Value(entry.Value)));

    private static string Value(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
