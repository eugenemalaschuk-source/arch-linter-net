using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// What a baseline write proposes: the entries that make up the new document, and the lifecycle
/// report describing what happened to each one.
/// </summary>
/// <param name="OutputEntries">Entries that compose the proposed document.</param>
/// <param name="LifecycleEntries">
/// The reviewable report. Out-of-scope entries (wrong mode, or outside the selected `--contract`
/// ids) are carried into <paramref name="OutputEntries"/> but deliberately omitted here: a scoped
/// run must not drop them, and reporting unrelated debt it never examined would be noise.
/// </param>
internal sealed record BaselineWritePlan(
    IReadOnlyList<ArchitectureBaselineComparisonEntry> OutputEntries,
    IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries)
{
    /// <summary>
    /// True when the plan leaves the document's entry set exactly as it was read. A prune with nothing
    /// to remove must reproduce its input byte-for-byte, which means not reserializing at all.
    /// </summary>
    public bool RemovesNothing => LifecycleEntries.All(e => e.Disposition != BaselineEntryDisposition.Removed);

    public bool AddsNothing => LifecycleEntries.All(e => e.Disposition != BaselineEntryDisposition.Added);
}

/// <summary>
/// Maps one comparison result onto the shared lifecycle vocabulary plus a per-entry disposition, and
/// onto the entries composing the proposed document. Kept in one place so `update` and `prune` cannot
/// drift into classifying the same condition differently — they differ only in disposition.
/// </summary>
internal static class BaselineWritePlanner
{
    public static BaselineWritePlan PlanGenerate(
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        BaselineReasonMap reasonMap)
    {
        var output = new List<ArchitectureBaselineComparisonEntry>();
        var lifecycle = new List<BaselineLifecycleEntry>();

        foreach (ArchitectureBaselineCandidate candidate in candidates)
        {
            if (candidate.ContractId == null)
            {
                continue;
            }

            ArchitectureBaselineComparisonEntry entry = new(
                candidate.ContractGroup,
                candidate.ContractId,
                candidate.SourceType,
                candidate.ForbiddenReference,
                reasonMap.Resolve(candidate.ContractId, candidate.ContractGroup),
                candidate.Identity);

            output.Add(entry);

            // Every finding a fresh generate records had no prior entry, so each is `new` — recorded
            // by this run, which is the disposition, not a different classification.
            lifecycle.Add(new BaselineLifecycleEntry(
                entry, BaselineEntryLifecycle.New, BaselineEntryDisposition.Added));
        }

        return new BaselineWritePlan(output, lifecycle);
    }

    public static BaselineWritePlan PlanUpdate(
        ArchitectureBaselineComparisonResult comparison,
        BaselineReasonMap reasonMap)
    {
        var output = new List<ArchitectureBaselineComparisonEntry>();
        var lifecycle = new List<BaselineLifecycleEntry>();

        foreach (ArchitectureBaselineComparisonEntry entry in comparison.Frozen)
        {
            // Canonical identity is equal, so this is `matched` — including when only the display text
            // regenerated. Display text is not identity, so a drifting `forbidden_reference` string is
            // not the `changed` classification, which is reserved for an entry whose canonical identity
            // actually differs from its successor's and which therefore must not suppress.
            ArchitectureBaselineComparisonEntry emitted = entry.CurrentForbiddenReference != null
                ? entry with { ForbiddenReference = entry.CurrentForbiddenReference }
                : entry;

            output.Add(emitted);
            lifecycle.Add(new BaselineLifecycleEntry(
                emitted, BaselineEntryLifecycle.Matched, BaselineEntryDisposition.Retained));
        }

        // Update never removes: an entry whose debt is fixed (`resolved`), one that can no longer be
        // evaluated against its contract (`stale`), and one correlating to several candidates
        // (`ambiguous`) are all carried through untouched. Removal is `prune`'s job, and rewriting an
        // ambiguous entry would mean guessing which identity the author meant.
        Append(output, lifecycle, comparison.Resolved, BaselineEntryLifecycle.Resolved, BaselineEntryDisposition.Retained);
        Append(output, lifecycle, comparison.Ambiguous, BaselineEntryLifecycle.Ambiguous, BaselineEntryDisposition.Retained);
        Append(output, lifecycle, comparison.ConfigurationErrors, BaselineEntryLifecycle.Stale, BaselineEntryDisposition.Retained);

        foreach (ArchitectureBaselineComparisonEntry entry in comparison.New)
        {
            ArchitectureBaselineComparisonEntry added = entry with
            {
                Reason = reasonMap.Resolve(entry.ContractId, entry.ContractGroup),
            };

            output.Add(added);
            lifecycle.Add(new BaselineLifecycleEntry(
                added, BaselineEntryLifecycle.New, BaselineEntryDisposition.Added));
        }

        output.AddRange(comparison.OutOfScope);

        return new BaselineWritePlan(output, lifecycle);
    }

    public static BaselineWritePlan PlanPrune(ArchitectureBaselineComparisonResult comparison)
    {
        var output = new List<ArchitectureBaselineComparisonEntry>();
        var lifecycle = new List<BaselineLifecycleEntry>();

        // Prune only removes, so a still-matching entry is emitted exactly as read — display text
        // included. That is what lets a prune with nothing to remove reproduce its input verbatim.
        Append(output, lifecycle, comparison.Frozen, BaselineEntryLifecycle.Matched, BaselineEntryDisposition.Retained);
        Append(output, lifecycle, comparison.Ambiguous, BaselineEntryLifecycle.Ambiguous, BaselineEntryDisposition.Retained);

        // Removed: reported but not emitted.
        AppendReportOnly(lifecycle, comparison.Resolved, BaselineEntryLifecycle.Resolved, BaselineEntryDisposition.Removed);
        AppendReportOnly(lifecycle, comparison.ConfigurationErrors, BaselineEntryLifecycle.Stale, BaselineEntryDisposition.Removed);

        output.AddRange(comparison.OutOfScope);

        return new BaselineWritePlan(output, lifecycle);
    }

    /// <summary>
    /// Read-only classification for `diff`/`verify`: same vocabulary, no disposition, since nothing is
    /// written.
    /// </summary>
    public static IReadOnlyList<BaselineLifecycleEntry> Report(ArchitectureBaselineComparisonResult comparison)
    {
        var lifecycle = new List<BaselineLifecycleEntry>();
        AppendReportOnly(lifecycle, comparison.New, BaselineEntryLifecycle.New, BaselineEntryDisposition.Reported);
        AppendReportOnly(lifecycle, comparison.Frozen, BaselineEntryLifecycle.Matched, BaselineEntryDisposition.Reported);
        AppendReportOnly(lifecycle, comparison.Resolved, BaselineEntryLifecycle.Resolved, BaselineEntryDisposition.Reported);
        AppendReportOnly(lifecycle, comparison.Ambiguous, BaselineEntryLifecycle.Ambiguous, BaselineEntryDisposition.Reported);
        AppendReportOnly(lifecycle, comparison.ConfigurationErrors, BaselineEntryLifecycle.Stale, BaselineEntryDisposition.Reported);
        return lifecycle;
    }

    private static void Append(
        List<ArchitectureBaselineComparisonEntry> output,
        List<BaselineLifecycleEntry> lifecycle,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> entries,
        BaselineEntryLifecycle value,
        BaselineEntryDisposition disposition)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            output.Add(entry);
            lifecycle.Add(new BaselineLifecycleEntry(entry, value, disposition));
        }
    }

    private static void AppendReportOnly(
        List<BaselineLifecycleEntry> lifecycle,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> entries,
        BaselineEntryLifecycle value,
        BaselineEntryDisposition disposition)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            lifecycle.Add(new BaselineLifecycleEntry(entry, value, disposition));
        }
    }
}
