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
    IReadOnlyList<BaselineLifecycleEntry> LifecycleEntries);

/// <summary>
/// Turns one comparison result into the proposed document and its lifecycle report. Kept separate
/// from the application service so `update` and `prune` provably agree on how a given classification
/// maps to a disposition — the same entry must not be `stale` in one command's report and something
/// else in the other's for the same underlying condition.
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
            lifecycle.Add(new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.Added));
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
            // Identity still matches, so the entry stays — but if the live candidate now renders a
            // different display string, the emitted entry adopts it and says so, rather than keeping
            // stale display text that no longer describes the violation it suppresses.
            bool displayDrifted = entry.CurrentForbiddenReference != null
                && !string.Equals(entry.CurrentForbiddenReference, entry.ForbiddenReference, StringComparison.Ordinal);

            ArchitectureBaselineComparisonEntry emitted = displayDrifted
                ? entry with { ForbiddenReference = entry.CurrentForbiddenReference! }
                : entry;

            output.Add(emitted);
            lifecycle.Add(new BaselineLifecycleEntry(
                emitted, displayDrifted ? BaselineEntryLifecycle.Changed : BaselineEntryLifecycle.Kept));
        }

        // Update never removes: entries whose violation is gone stay as `stale` (that is `prune`'s
        // job), entries that correlate to several candidates stay untouched rather than being
        // rewritten into one guessed identity, and unknown-contract entries stay as configuration
        // findings rather than being silently discarded.
        AddAll(output, lifecycle, comparison.Resolved, BaselineEntryLifecycle.Stale);
        AddAll(output, lifecycle, comparison.Ambiguous, BaselineEntryLifecycle.Ambiguous);
        AddAll(output, lifecycle, comparison.ConfigurationErrors, BaselineEntryLifecycle.Configuration);

        foreach (ArchitectureBaselineComparisonEntry entry in comparison.New)
        {
            ArchitectureBaselineComparisonEntry added = entry with
            {
                Reason = reasonMap.Resolve(entry.ContractId, entry.ContractGroup),
            };

            output.Add(added);
            lifecycle.Add(new BaselineLifecycleEntry(added, BaselineEntryLifecycle.Added));
        }

        output.AddRange(comparison.OutOfScope);

        return new BaselineWritePlan(output, lifecycle);
    }

    public static BaselineWritePlan PlanPrune(ArchitectureBaselineComparisonResult comparison)
    {
        var output = new List<ArchitectureBaselineComparisonEntry>();
        var lifecycle = new List<BaselineLifecycleEntry>();

        // Prune only removes, so a still-matching entry is emitted exactly as it was read — display
        // text included. A pruned file with nothing to remove is byte-identical to its input.
        AddAll(output, lifecycle, comparison.Frozen, BaselineEntryLifecycle.Kept);
        AddAll(output, lifecycle, comparison.Ambiguous, BaselineEntryLifecycle.Ambiguous);

        // Removals are reported but not emitted.
        AppendReportOnly(lifecycle, comparison.Resolved, BaselineEntryLifecycle.Resolved);
        AppendReportOnly(lifecycle, comparison.ConfigurationErrors, BaselineEntryLifecycle.Configuration);

        output.AddRange(comparison.OutOfScope);

        return new BaselineWritePlan(output, lifecycle);
    }

    private static void AddAll(
        List<ArchitectureBaselineComparisonEntry> output,
        List<BaselineLifecycleEntry> lifecycle,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> entries,
        BaselineEntryLifecycle value)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            output.Add(entry);
            lifecycle.Add(new BaselineLifecycleEntry(entry, value));
        }
    }

    private static void AppendReportOnly(
        List<BaselineLifecycleEntry> lifecycle,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> entries,
        BaselineEntryLifecycle value)
    {
        foreach (ArchitectureBaselineComparisonEntry entry in entries)
        {
            lifecycle.Add(new BaselineLifecycleEntry(entry, value));
        }
    }
}
