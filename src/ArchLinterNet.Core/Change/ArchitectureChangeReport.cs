using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Change;

/// <summary>Versioned, persisted result of one complete architecture analysis.</summary>
public sealed record ArchitectureChangeSnapshot(
    int SchemaVersion,
    string Mode,
    IReadOnlyList<ArchitectureChangeEntry> Entries,
    IReadOnlyList<ArchitectureChangeFinding> Findings,
    IReadOnlyList<string> BaselineDebt)
{
    public const int CurrentSchemaVersion = 1;
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
    IReadOnlyList<string> BaselineDebt);

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

        ArchitectureChangeSnapshot snapshot = new(
            document.SchemaVersion,
            document.Mode ?? string.Empty,
            document.Entries ?? Array.Empty<ArchitectureChangeEntry>(),
            document.Findings ?? Array.Empty<ArchitectureChangeFinding>(),
            document.BaselineDebt ?? Array.Empty<string>());
        Validate(snapshot);
        return snapshot with
        {
            Entries = Order(snapshot.Entries),
            Findings = Order(snapshot.Findings),
            BaselineDebt = snapshot.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
        };
    }

    public static ArchitectureChangeReport Compare(ArchitectureChangeSnapshot baseline, ArchitectureChangeSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        Validate(baseline);
        Validate(current);
        if (!string.Equals(baseline.Mode, current.Mode, StringComparison.Ordinal))
        {
            throw new ArgumentException("Base and current snapshots must use the same analysis mode.");
        }

        Dictionary<string, ArchitectureChangeEntry> baseEntries = baseline.Entries.ToDictionary(Key, StringComparer.Ordinal);
        Dictionary<string, ArchitectureChangeEntry> currentEntries = current.Entries.ToDictionary(Key, StringComparer.Ordinal);
        HashSet<string> knownBaseIdentities = baseline.Findings
            .Select(static finding => finding.Identity)
            .Concat(baseline.BaselineDebt)
            .ToHashSet(StringComparer.Ordinal);

        return new ArchitectureChangeReport(
            Order(currentEntries.Where(pair => !baseEntries.ContainsKey(pair.Key)).Select(static pair => pair.Value)),
            Order(baseEntries.Where(pair => !currentEntries.ContainsKey(pair.Key)).Select(static pair => pair.Value)),
            Order(current.Findings.Where(finding => !knownBaseIdentities.Contains(finding.Identity))),
            Order(current.Findings.Where(finding => knownBaseIdentities.Contains(finding.Identity))),
            current.BaselineDebt.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    }

    public static string FormatJson(ArchitectureChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, _jsonOptions);
    }

    public static string FormatHuman(ArchitectureChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        StringBuilder builder = new();
        builder.AppendLine("Architecture change report");
        AppendEntries(builder, "Added surfaces", report.Added);
        AppendEntries(builder, "Removed surfaces", report.Removed);
        AppendFindings(builder, "New findings", report.NewFindings);
        AppendFindings(builder, "Existing findings", report.ExistingFindings);
        builder.AppendLine($"Baseline debt: {report.BaselineDebt.Count}");
        foreach (string identity in report.BaselineDebt)
        {
            builder.AppendLine($"- {identity}");
        }

        return builder.ToString();
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

    private static IReadOnlyList<ArchitectureChangeEntry> Order(IEnumerable<ArchitectureChangeEntry> entries) => entries
        .OrderBy(static entry => entry.Kind, StringComparer.Ordinal)
        .ThenBy(static entry => entry.Identity, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<ArchitectureChangeFinding> Order(IEnumerable<ArchitectureChangeFinding> findings) => findings
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
        IReadOnlyList<ArchitectureChangeEntry>? Entries,
        IReadOnlyList<ArchitectureChangeFinding>? Findings,
        IReadOnlyList<string>? BaselineDebt);
}
