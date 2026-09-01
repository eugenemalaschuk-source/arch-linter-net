using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Change;

/// <summary>Versioned, persisted result of one complete architecture analysis.</summary>
public sealed record ArchitectureChangeSnapshot(
    int SchemaVersion,
    string Mode,
    string ConditionSetName,
    IReadOnlyList<ArchitectureChangeEntry> Entries,
    IReadOnlyList<ArchitectureChangeFinding> Findings,
    IReadOnlyList<string> BaselineDebt)
{
    public const int CurrentSchemaVersion = 2;
    public const string Kind = "architecture-change-snapshot";
}

/// <summary>A stable architecture surface observed by a complete analysis.</summary>
public sealed record ArchitectureChangeEntry(string Kind, string Identity, string Display);

/// <summary>A stable finding identity retained independently from structural surfaces.</summary>
public sealed record ArchitectureChangeFinding(string Identity, string Kind, string Display);

/// <summary>The deterministic delta between two complete architecture snapshots.</summary>
public sealed record ArchitectureChangeReport(
    IReadOnlyList<ArchitectureChangeEntry> Added,
    IReadOnlyList<ArchitectureChangeEntry> Removed,
    IReadOnlyList<ArchitectureChangeFinding> NewFindings,
    IReadOnlyList<ArchitectureChangeFinding> ExistingFindings,
    IReadOnlyList<string> BaselineDebt)
{
    /// <summary>Current version of the serialized architecture-change report artifact.</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Stable kind discriminator for the serialized architecture-change report.</summary>
    public const string ReportKind = "architecture-change-report";

    /// <summary>Serialized report schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Serialized report kind discriminator.</summary>
    public string Kind { get; init; } = ReportKind;

    /// <summary>Correlation context supplied by the workflow that created this report.</summary>
    public ArchitectureChangeReportContext? ExecutionContext { get; init; }

    // Keep resolved findings additive rather than adding a positional constructor parameter. This
    // preserves the public five-argument construction/deconstruction shape while extending the
    // canonical report document for downstream consumers.
    public IReadOnlyList<ArchitectureChangeFinding> ResolvedFindings { get; init; } =
        Array.Empty<ArchitectureChangeFinding>();
}

/// <summary>Mode, condition set, and workflow identity proven by one change report.</summary>
public sealed record ArchitectureChangeReportContext(
    string ExecutionId,
    string Mode,
    string ConditionSet);

/// <summary>Serializes, validates, and compares architecture change snapshots.</summary>
public static class ArchitectureChangeReports
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static string SerializeSnapshot(ArchitectureChangeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        return JsonSerializer.Serialize(new SnapshotDocument(
            ArchitectureChangeSnapshot.Kind,
            snapshot.SchemaVersion,
            snapshot.Mode,
            snapshot.ConditionSetName,
            Order(snapshot.Entries),
            Order(snapshot.Findings),
            snapshot.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray()), _jsonOptions);
    }

    public static ArchitectureChangeSnapshot DeserializeSnapshot(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        SnapshotDocument document = JsonSerializer.Deserialize<SnapshotDocument>(json, _jsonOptions)
            ?? throw new ArgumentException("The architecture change snapshot is empty.", nameof(json));
        if (!string.Equals(document.SnapshotKind, ArchitectureChangeSnapshot.Kind, StringComparison.Ordinal))
        {
            throw new ArgumentException("The input is not an architecture-change-snapshot artifact.", nameof(json));
        }

        if (document.Mode is null
            || document.ConditionSetName is null
            || document.Entries is null
            || document.Findings is null
            || document.BaselineDebt is null)
        {
            throw new ArgumentException("The architecture change snapshot is incomplete.", nameof(json));
        }

        ArchitectureChangeSnapshot snapshot = new(
            document.SchemaVersion,
            document.Mode,
            document.ConditionSetName,
            document.Entries,
            document.Findings,
            document.BaselineDebt);
        Validate(snapshot);
        return snapshot with
        {
            Entries = Order(snapshot.Entries),
            Findings = Order(snapshot.Findings),
            BaselineDebt = snapshot.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        };
    }

    public static ArchitectureChangeReport Compare(
        ArchitectureChangeSnapshot baseline,
        ArchitectureChangeSnapshot current,
        string executionId)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        Validate(baseline);
        Validate(current);
        if (!string.Equals(baseline.Mode, current.Mode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Base and current snapshots must use the same analysis mode.");
        }

        if (!string.Equals(baseline.ConditionSetName, current.ConditionSetName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Base and current snapshots must use the same condition set.");
        }

        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("Architecture change reports require a non-empty execution context.", nameof(executionId));
        }

        Dictionary<string, ArchitectureChangeEntry> baseEntries = baseline.Entries.ToDictionary(Key, StringComparer.Ordinal);
        Dictionary<string, ArchitectureChangeEntry> currentEntries = current.Entries.ToDictionary(Key, StringComparer.Ordinal);
        HashSet<string> knownBaseIdentities = baseline.Findings
            .Select(static finding => finding.Identity)
            .Concat(baseline.BaselineDebt)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> currentFindingIdentities = current.Findings
            .Select(static finding => finding.Identity)
            .ToHashSet(StringComparer.Ordinal);
        return new ArchitectureChangeReport(
            Order(currentEntries.Where(pair => !baseEntries.ContainsKey(pair.Key)).Select(static pair => pair.Value)),
            Order(baseEntries.Where(pair => !currentEntries.ContainsKey(pair.Key)).Select(static pair => pair.Value)),
            Order(current.Findings.Where(finding => !knownBaseIdentities.Contains(finding.Identity))),
            Order(current.Findings.Where(finding => knownBaseIdentities.Contains(finding.Identity))),
            current.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray())
        {
            ExecutionContext = new ArchitectureChangeReportContext(
                executionId,
                current.Mode,
                current.ConditionSetName),
            ResolvedFindings = Order(baseline.Findings.Where(finding =>
                !currentFindingIdentities.Contains(finding.Identity)))
        };
    }

    public static string FormatJson(ArchitectureChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Validate(report);
        return JsonSerializer.Serialize(OrderReport(report), _jsonOptions);
    }

    public static string FormatHuman(ArchitectureChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Validate(report);
        report = OrderReport(report);
        StringBuilder builder = new();
        builder.AppendLine("Architecture change report");
        AppendEntries(builder, "Added surfaces", report.Added);
        AppendEntries(builder, "Removed surfaces", report.Removed);
        AppendFindings(builder, "New findings", report.NewFindings);
        AppendFindings(builder, "Existing findings", report.ExistingFindings);
        AppendFindings(builder, "Resolved findings", report.ResolvedFindings);
        builder.AppendLine($"Baseline debt: {report.BaselineDebt.Count}");
        foreach (string identity in report.BaselineDebt)
        {
            builder.AppendLine($"- {identity}");
        }

        return builder.ToString();
    }

    private static ArchitectureChangeReport OrderReport(ArchitectureChangeReport report) => report with
    {
        Added = Order(report.Added),
        Removed = Order(report.Removed),
        NewFindings = Order(report.NewFindings),
        ExistingFindings = Order(report.ExistingFindings),
        ResolvedFindings = Order(report.ResolvedFindings),
        BaselineDebt = report.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
    };

    /// <summary>Reads and validates one serialized architecture-change report artifact.</summary>
    public static ArchitectureChangeReport DeserializeReport(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ReportDocument document = JsonSerializer.Deserialize<ReportDocument>(json, _jsonOptions)
            ?? throw new ArgumentException("The architecture change report is empty.", nameof(json));
        if (!string.Equals(document.Kind, ArchitectureChangeReport.ReportKind, StringComparison.Ordinal))
        {
            throw new ArgumentException("The input is not an architecture-change-report artifact.", nameof(json));
        }

        if (document.SchemaVersion != ArchitectureChangeReport.CurrentSchemaVersion
            || document.Added is null
            || document.Removed is null
            || document.NewFindings is null
            || document.ExistingFindings is null
            || document.ResolvedFindings is null
            || document.BaselineDebt is null)
        {
            throw new ArgumentException("The architecture change report is incomplete or unsupported.", nameof(json));
        }

        ArchitectureChangeReport report = new(
            document.Added,
            document.Removed,
            document.NewFindings,
            document.ExistingFindings,
            document.BaselineDebt)
        {
            SchemaVersion = document.SchemaVersion,
            Kind = document.Kind!,
            ExecutionContext = document.ExecutionContext is null
                ? null
                : new ArchitectureChangeReportContext(
                    document.ExecutionContext.ExecutionId ?? string.Empty,
                    document.ExecutionContext.Mode ?? string.Empty,
                    document.ExecutionContext.ConditionSet ?? string.Empty),
            ResolvedFindings = document.ResolvedFindings,
        };
        Validate(report);
        return OrderReport(report);
    }

    private static void Validate(ArchitectureChangeReport report)
    {
        if (report.SchemaVersion != ArchitectureChangeReport.CurrentSchemaVersion
            || !string.Equals(report.Kind, ArchitectureChangeReport.ReportKind, StringComparison.Ordinal)
            || report.Added is null
            || report.Removed is null
            || report.NewFindings is null
            || report.ExistingFindings is null
            || report.ResolvedFindings is null
            || report.BaselineDebt is null
            || report.ExecutionContext is null
            || string.IsNullOrWhiteSpace(report.ExecutionContext.ExecutionId)
            || report.ExecutionContext.Mode is not ("strict" or "audit")
            || report.ExecutionContext.ConditionSet is null)
        {
            throw new ArgumentException("The architecture change report is incomplete or unsupported.", nameof(report));
        }

        HashSet<string> findingIdentities = new(StringComparer.Ordinal);
        foreach (ArchitectureChangeFinding finding in report.NewFindings
            .Concat(report.ExistingFindings)
            .Concat(report.ResolvedFindings))
        {
            if (string.IsNullOrWhiteSpace(finding.Identity)
                || !findingIdentities.Add(finding.Identity))
            {
                throw new ArgumentException(
                    "Architecture change report findings must have unique identities across new, existing, and resolved sections.",
                    nameof(report));
            }
        }
    }

    private static void Validate(ArchitectureChangeSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != ArchitectureChangeSnapshot.CurrentSchemaVersion)
        {
            throw new ArgumentException($"Unsupported architecture change snapshot version '{snapshot.SchemaVersion}'.");
        }

        if (snapshot.Mode is not ("strict" or "audit"))
        {
            throw new ArgumentException("Architecture change snapshots must use strict or audit mode.");
        }

        if (snapshot.ConditionSetName is null)
        {
            throw new ArgumentException("Architecture change snapshots must record their condition set.");
        }

        EnsureUnique(snapshot.Entries.Select(Key), "entry");
        EnsureUnique(snapshot.Findings.Select(static finding => finding.Identity), "finding");
    }

    private static void EnsureUnique(IEnumerable<string> values, string itemName)
    {
        if (values.Any(static value => string.IsNullOrWhiteSpace(value)) || values.Distinct(StringComparer.Ordinal).Count() != values.Count())
        {
            throw new ArgumentException($"Architecture change snapshot contains duplicate or empty {itemName} identities.");
        }
    }

    private static string Key(ArchitectureChangeEntry entry) => entry.Kind + "\u001f" + entry.Identity;

    private static ArchitectureChangeEntry[] Order(IEnumerable<ArchitectureChangeEntry> entries) => entries
        .OrderBy(static entry => entry.Kind, StringComparer.Ordinal)
        .ThenBy(static entry => entry.Identity, StringComparer.Ordinal)
        .ToArray();

    private static ArchitectureChangeFinding[] Order(IEnumerable<ArchitectureChangeFinding> findings) => findings
        .OrderBy(static finding => finding.Kind, StringComparer.Ordinal)
        .ThenBy(static finding => finding.Identity, StringComparer.Ordinal)
        .ToArray();

    private static void AppendEntries(StringBuilder builder, string title, IReadOnlyList<ArchitectureChangeEntry> entries)
    {
        builder.AppendLine($"{title}: {entries.Count}");
        foreach (ArchitectureChangeEntry entry in entries)
        {
            builder.AppendLine($"- [{entry.Kind}] {entry.Display}");
        }
    }

    private static void AppendFindings(StringBuilder builder, string title, IReadOnlyList<ArchitectureChangeFinding> findings)
    {
        builder.AppendLine($"{title}: {findings.Count}");
        foreach (ArchitectureChangeFinding finding in findings)
        {
            builder.AppendLine($"- [{finding.Kind}] {finding.Display}");
        }
    }

    private sealed record SnapshotDocument(
        string SnapshotKind,
        int SchemaVersion,
        string? Mode,
        string? ConditionSetName,
        IReadOnlyList<ArchitectureChangeEntry>? Entries,
        IReadOnlyList<ArchitectureChangeFinding>? Findings,
        IReadOnlyList<string>? BaselineDebt);

    private sealed record ReportDocument(
        string? Kind,
        int SchemaVersion,
        ReportContextDocument? ExecutionContext,
        IReadOnlyList<ArchitectureChangeEntry>? Added,
        IReadOnlyList<ArchitectureChangeEntry>? Removed,
        IReadOnlyList<ArchitectureChangeFinding>? NewFindings,
        IReadOnlyList<ArchitectureChangeFinding>? ExistingFindings,
        IReadOnlyList<ArchitectureChangeFinding>? ResolvedFindings,
        IReadOnlyList<string>? BaselineDebt);

    private sealed record ReportContextDocument(
        string? ExecutionId,
        string? Mode,
        string? ConditionSet);
}
