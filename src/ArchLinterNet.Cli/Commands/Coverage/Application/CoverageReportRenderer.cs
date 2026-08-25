using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Coverage.Application;

internal static class CoverageReportRenderer
{
    private const string CoveredState = "covered";
    private const string UncoveredState = "uncovered";
    private const string StaleState = "stale";
    private const string UnknownState = "unknown";
    private const string StateKey = "state";
    private const string ContractKey = "contract";
    private const string EvidenceKey = "evidence";

    private static readonly string[] _states = [CoveredState, "excluded", UncoveredState, StaleState, UnknownState];

    public static string Render(JsonElement report, IReadOnlyList<string>? changedFiles, string repositoryRoot, bool diffFailed, int? maxFailures)
    {
        var lines = new List<string> { "## Architecture coverage", "", $"**Status:** {(Passed(report) ? "✅ pass" : "❌ fail")}", "" };
        IReadOnlyList<FailureRule> failures = Passed(report) ? [] : CollectFailures(report);
        if (!Passed(report))
        {
            RenderFailures(lines, failures, maxFailures);
            lines.Add(string.Empty);
        }

        Dictionary<string, int> totals = _states.ToDictionary(static state => state, static _ => 0, StringComparer.Ordinal);
        JsonElement coverage = ArrayElement(report, "coverage_summary");
        foreach (JsonElement entry in coverage.EnumerateArray())
        {
            JsonElement counts = Object(entry, "counts");
            foreach (string state in _states)
            {
                totals[state] += Int(counts, state);
            }
        }

        lines.AddRange(["| Metric | Count |", "| --- | --- |", $"| Failed rules | {failures.Count} |", $"| Failed diagnostics | {failures.Sum(static rule => rule.Diagnostics.Count)} |",
            $"| Covered | {totals[CoveredState]} |", $"| Excluded | {totals["excluded"]} |", $"| Uncovered | {totals[UncoveredState]} |", $"| Stale | {totals[StaleState]} |", $"| Unknown | {totals[UnknownState]} |"]);
        if (coverage.GetArrayLength() == 0)
        {
            lines.AddRange([string.Empty, "> **Note:** the policy defines no coverage contracts (`strict_coverage`/`audit_coverage`). These zeros mean coverage is unconfigured, not that everything is covered."]);
        }

        if (diffFailed)
        {
            lines.AddRange([string.Empty, "### New-code coverage", string.Empty, "> **Unavailable:** the changed-files diff could not be computed for this run (e.g. a `git diff`/fetch failure). This is reported explicitly rather than as zero changed files, since a diff failure is not the same as an empty diff."]);
        }
        else if (changedFiles is not null)
        {
            RenderChangedFiles(lines, report, changedFiles, repositoryRoot);
        }

        return string.Join('\n', lines) + "\n";
    }

    private static void RenderFailures(List<string> lines, IReadOnlyList<FailureRule> rules, int? max)
    {
        lines.Add($"### Failed rules ({rules.Count})");
        lines.Add(string.Empty);
        if (rules.Count == 0)
        {
            lines.Add("> **Unavailable:** strict validation failed without structured diagnostics; download the `architecture-strict` artifact for the raw result.");
            return;
        }

        foreach (FailureRule rule in rules)
        {
            string suffix = rule.Name == rule.Identifier ? string.Empty : $" — {Compact(rule.Name)}";
            string noun = rule.Diagnostics.Count == 1 ? "diagnostic" : "diagnostics";
            lines.Add($"- **`{Code(rule.Identifier)}`{suffix}** — {rule.Diagnostics.Count} failed {noun}");
            foreach (FailureDiagnostic diagnostic in rule.Diagnostics.Take(max ?? int.MaxValue))
            {
                lines.Add($"  - **{diagnostic.Category}:** {diagnostic.Message}");
            }

            int omitted = rule.Diagnostics.Count - (max ?? rule.Diagnostics.Count);
            if (omitted > 0)
            {
                lines.Add($"  - _{omitted} additional {(omitted == 1 ? "diagnostic" : "diagnostics")} omitted._");
            }
        }
    }

    private static FailureRule[] CollectFailures(JsonElement report)
    {
        (string Property, string Category)[] collections = [("violations", "Violation"), ("coverage_findings", "Coverage"), ("cycle_diagnostics", "Cycle"), ("unmatched_ignored_violations", "Stale baseline ignore"), ("policy_consistency_findings", "Policy consistency"), ("preflight_diagnostics", "Build-state preflight"), ("classification_conflicts", "Classification conflict"), ("classification_metadata_failures", "Classification metadata")];
        var result = new Dictionary<string, FailureRuleBuilder>(StringComparer.Ordinal);
        foreach ((string property, string category) in collections)
        {
            foreach (JsonElement finding in ArrayElement(report, property).EnumerateArray())
            {
                if (property == "preflight_diagnostics" && String(finding, StateKey) == "current") continue;
                AddFailure(result, finding, category);
            }
        }
        if (ArrayElement(report, "coverage_findings").GetArrayLength() == 0)
        {
            foreach (JsonElement entry in ArrayElement(report, "coverage_summary").EnumerateArray())
                foreach ((string bucket, string state) in new[] { ("uncovered_items", UncoveredState), ("stale_items", StaleState), ("unknown_items", UnknownState) })
                    foreach (JsonElement item in ArrayElement(entry, bucket).EnumerateArray())
                    {
                        var fallback = new Dictionary<string, string?> { ["contract_id"] = String(entry, "contract_id"), [ContractKey] = String(entry, ContractKey), ["scope"] = String(entry, "scope"), [StateKey] = state, ["item"] = String(item, "item"), [EvidenceKey] = String(item, EvidenceKey) ?? String(item, "reason") };
                        AddFailure(result, fallback, "Coverage summary");
                    }
        }

        return result.Values.Select(static value => value.Build()).OrderBy(static rule => rule.Identifier, StringComparer.Ordinal).ThenBy(static rule => rule.Name, StringComparer.Ordinal).ToArray();
    }

    private static void AddFailure(Dictionary<string, FailureRuleBuilder> target, JsonElement finding, string category) => AddFailure(target, Properties(finding), category);

    private static void AddFailure(Dictionary<string, FailureRuleBuilder> target, IReadOnlyDictionary<string, string?> finding, string category)
    {
        string identifier = finding.GetValueOrDefault("contract_id") ?? finding.GetValueOrDefault(ContractKey) ?? category.ToLowerInvariant().Replace(' ', '-');
        string name = finding.GetValueOrDefault(ContractKey) ?? identifier;
        if (!target.TryGetValue(identifier, out FailureRuleBuilder? builder)) target[identifier] = builder = new(identifier, name);
        builder.Add(category, Summary(finding));
    }

    private static void RenderChangedFiles(List<string> lines, JsonElement report, IReadOnlyList<string> files, string root)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<(string Scope, string Item), string> index = BuildCoverageIndex(report, scopes);
        var units = new Dictionary<(string Scope, string Item), string>();
        var attention = new List<string>();
        foreach (string file in files)
        {
            ClassifyChangedFile(file, root, scopes, index, units, attention);
        }

        int covered = units.Values.Count(static state => state == CoveredState);
        int uncovered = units.Values.Count(static state => state is UncoveredState or StaleState or "excluded");
        int unknown = units.Values.Count(static state => state == UnknownState);
        lines.AddRange([string.Empty, "### New-code coverage", string.Empty, "| Metric | Count |", "| --- | --- |", $"| Changed first-party files | {files.Count} |", $"| Changed namespaces/projects/assemblies covered | {covered} |", $"| Changed namespaces/projects/assemblies uncovered | {uncovered} |", $"| Requiring policy update | {(unknown == 0 ? "none" : unknown)} |"]);
        if (attention.Count > 0) { lines.AddRange([string.Empty, "Items needing attention:", string.Empty]); lines.AddRange(attention.Distinct(StringComparer.Ordinal)); }
    }

    private static Dictionary<(string Scope, string Item), string> BuildCoverageIndex(JsonElement report, HashSet<string> scopes)
    {
        var index = new Dictionary<(string Scope, string Item), string>();
        foreach (JsonElement entry in ArrayElement(report, "coverage_summary").EnumerateArray())
        {
            string? scope = String(entry, "scope");
            if (scope is not ("namespace" or "project" or "assembly"))
            {
                continue;
            }

            scopes.Add(scope);
            foreach (string state in _states)
            {
                foreach (JsonElement item in ArrayElement(entry, state + "_items").EnumerateArray())
                {
                    if (String(item, "item") is { } name)
                    {
                        index[(scope, name)] = state;
                    }
                }
            }
        }

        return index;
    }

    private static void ClassifyChangedFile(
        string file,
        string root,
        HashSet<string> scopes,
        Dictionary<(string Scope, string Item), string> index,
        Dictionary<(string Scope, string Item), string> units,
        List<string> attention)
    {
        if (file.Replace('\\', '/').Contains("AdoptionAcceptance/Fixtures", StringComparison.Ordinal))
        {
            return;
        }

        string? project = FindProject(file, root);
        if (project is not null && Path.GetFileNameWithoutExtension(project).EndsWith(".Tests", StringComparison.Ordinal))
        {
            return;
        }

        bool classified = false;
        foreach ((string scope, string? item) in DetectedUnits(file, project, root, scopes))
        {
            if (item is null)
            {
                continue;
            }

            classified = true;
            string state = index.GetValueOrDefault((scope, item), UnknownState);
            units[(scope, item)] = state;
            if (state != CoveredState)
            {
                attention.Add($"- `{file}` — `{item}` ({scope}): **{state}**");
            }
        }

        if (!classified)
        {
            units[(UnknownState, file)] = UnknownState;
            attention.Add($"- `{file}` — `{file}` (unknown): **unknown**");
        }
    }

    private static IEnumerable<(string Scope, string? Item)> DetectedUnits(string file, string? project, string root, HashSet<string> scopes)
    {
        if (scopes.Contains("namespace")) yield return ("namespace", Namespace(file, root));
        if (scopes.Contains("project")) yield return ("project", project is null ? null : Path.GetRelativePath(root, project).Replace('\\', '/'));
        if (scopes.Contains("assembly")) yield return ("assembly", project is null ? null : Assembly(project));
    }

    private static string? FindProject(string file, string root)
    {
        string current = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, file))) ?? root;
        string normalizedRoot = Path.GetFullPath(root);
        while (true)
        {
            string? project = Directory.EnumerateFiles(current, "*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return project;
            }

            if (current == normalizedRoot || current == Path.GetPathRoot(current))
            {
                return null;
            }

            current = Directory.GetParent(current)?.FullName ?? normalizedRoot;
        }
    }

    private static string? Namespace(string file, string root)
    {
        string path = Path.Combine(root, file); if (!File.Exists(path)) return null;
        string? line = File.ReadLines(path).FirstOrDefault(static value => value.TrimStart().StartsWith("namespace ", StringComparison.Ordinal));
        return line?.TrimStart()["namespace ".Length..].Split([' ', ';', '{'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    private static string Assembly(string project)
    {
        string text = File.ReadAllText(project); const string StartTag = "<AssemblyName>"; const string EndTag = "</AssemblyName>";
        int index = text.IndexOf(StartTag, StringComparison.Ordinal); if (index < 0) return Path.GetFileNameWithoutExtension(project);
        int close = text.IndexOf(EndTag, index, StringComparison.Ordinal); return close < 0 ? Path.GetFileNameWithoutExtension(project) : text[(index + StartTag.Length)..close].Trim();
    }

    private static bool Passed(JsonElement element) => element.TryGetProperty("passed", out JsonElement passed) && passed.ValueKind == JsonValueKind.True;
    private static JsonElement ArrayElement(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value : default;
    private static JsonElement Object(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value : default;
    private static int Int(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int number) ? number : 0;
    private static string? String(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null;
    private static Dictionary<string, string?> Properties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return [];
        Dictionary<string, string?> values = element.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? property.Value.ToString() : null, StringComparer.Ordinal);
        if (element.TryGetProperty("forbidden_references", out JsonElement references) && references.ValueKind == JsonValueKind.Array) values["forbidden_references"] = string.Join(", ", references.EnumerateArray().Select(static item => item.ToString()));
        foreach (string location in new[] { "source_location", "policy_origin", "policy_location" }) if (element.TryGetProperty(location, out JsonElement value)) values[location] = Location(value);
        return values;
    }
    private static string? Location(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.ToString();
        }

        if (String(value, "path") is not { } path)
        {
            return null;
        }

        string line = String(value, "line") is { } lineValue ? ":" + lineValue : string.Empty;
        string column = String(value, "column") is { } columnValue ? ":" + columnValue : string.Empty;
        return path + line + column;
    }
    private static string Summary(IReadOnlyDictionary<string, string?> values) => string.Join("; ", new[] { ("code", "message_code"), ("source", "source"), ("subject", "subject"), ("state", StateKey), ("item", "item"), ("forbidden namespace", "forbidden_namespace"), ("forbidden references", "forbidden_references"), ("evidence", EvidenceKey), ("reason", "reason"), ("detail", "detail"), ("source", "source_location"), ("policy", "policy_origin"), ("policy", "policy_location") }.Where(pair => values.GetValueOrDefault(pair.Item2) is not null).Select(pair => $"{pair.Item1} `{Code(values[pair.Item2]!)}`"));
    private static string Code(string value) => Compact(value).Replace("`", "'", StringComparison.Ordinal);
    private static string Compact(string value) { string text = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); return text.Length <= 240 ? text : text[..237] + "..."; }

    private sealed class FailureRuleBuilder(string identifier, string name) { private readonly SortedSet<FailureDiagnostic> _diagnostics = []; public void Add(string category, string summary) => _diagnostics.Add(new(category, string.IsNullOrEmpty(summary) ? "structured diagnostic emitted without detail fields" : summary)); public FailureRule Build() => new(identifier, name, _diagnostics.ToArray()); }
    private sealed record FailureRule(string Identifier, string Name, IReadOnlyList<FailureDiagnostic> Diagnostics);
    private sealed record FailureDiagnostic(string Category, string Message) : IComparable<FailureDiagnostic> { public int CompareTo(FailureDiagnostic? other) => other is null ? 1 : StringComparer.Ordinal.Compare($"{Category}\0{Message}", $"{other.Category}\0{other.Message}"); }
}
