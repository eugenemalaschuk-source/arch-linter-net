using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Baseline;

/// <summary>
/// Renders the shared baseline entry lifecycle for humans and for JSON consumers. Both projections
/// live here so a `counts` object, a status string, and a section label can never drift apart between
/// the six baseline subcommands.
/// </summary>
internal static class BaselineLifecycleFormatter
{
    /// <summary>
    /// Builds the `counts` object. Every lifecycle name is always present — including the ones the
    /// invoked operation cannot produce, reported as zero — so one shape reads back from every
    /// subcommand instead of consumers probing for optional keys.
    /// </summary>
    public static IDictionary<string, int> Counts(IEnumerable<BaselineLifecycleEntry> entries)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (string name in BaselineEntryLifecycleNames.All)
        {
            counts[name] = 0;
        }

        foreach (BaselineLifecycleEntry entry in entries)
        {
            counts[BaselineEntryLifecycleNames.WireName(entry.Lifecycle)]++;
        }

        return counts;
    }

    public static object EntryForJson(ArchitectureBaselineComparisonEntry entry, BaselineEntryLifecycle lifecycle)
    {
        return EntryForJson(entry, BaselineEntryLifecycleNames.WireName(lifecycle));
    }

    public static object EntryForJson(ArchitectureBaselineComparisonEntry entry, string status)
    {
        return new
        {
            contractGroup = entry.ContractGroup,
            contractId = entry.ContractId,
            sourceType = entry.SourceType,
            forbiddenReference = entry.ForbiddenReference,
            reason = entry.Reason,
            issue = entry.Issue,
            status,
            identity = IdentityForJson(entry.Identity),
        };
    }

    /// <summary>
    /// The canonical structured identity: every field, plus the exact string baseline comparison keys
    /// on, so a consumer can correlate an entry across commands without re-deriving anything. Null for
    /// a version-1 document, which has no structured identity.
    /// </summary>
    public static object? IdentityForJson(ArchitectureViolationIdentity? identity)
    {
        if (identity == null)
        {
            return null;
        }

        return new
        {
            identityVersion = identity.IdentityVersion,
            contractFamily = identity.ContractFamily,
            kind = identity.Kind,
            contractId = identity.ContractId,
            sourceAssembly = identity.SourceAssembly,
            sourceType = identity.SourceType,
            sourceMember = identity.SourceMember,
            targetAssembly = identity.TargetAssembly,
            targetType = identity.TargetType,
            targetMember = identity.TargetMember,
            occurrence = identity.Occurrence,
            configuration = identity.Configuration,
            canonical = identity.ToString(),
        };
    }

    public static IEnumerable<object> EntriesForJson(IEnumerable<BaselineLifecycleEntry> entries)
    {
        return entries.Select(e => EntryForJson(e.Entry, e.Lifecycle));
    }

    /// <summary>
    /// Human-readable lifecycle report: one count line per lifecycle value that occurred, each
    /// followed by its entries. Values with no entries are omitted so a short report stays short.
    /// </summary>
    public static string FormatForHumans(IReadOnlyList<BaselineLifecycleEntry> entries)
    {
        List<string> lines = new();

        foreach (string name in BaselineEntryLifecycleNames.All)
        {
            List<BaselineLifecycleEntry> matching = entries
                .Where(e => BaselineEntryLifecycleNames.WireName(e.Lifecycle) == name)
                .ToList();

            if (matching.Count == 0)
            {
                continue;
            }

            lines.Add($"{name}: {matching.Count}");
            foreach (BaselineLifecycleEntry entry in matching)
            {
                lines.Add(Describe(entry.Entry));
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("No baseline entries affected.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string Describe(ArchitectureBaselineComparisonEntry entry)
    {
        return $"  {entry.ContractGroup}/{entry.ContractId}: {entry.SourceType} -> {entry.ForbiddenReference}";
    }
}
