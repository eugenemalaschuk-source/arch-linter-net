namespace ArchLinterNet.Core.Model;

/// <summary>
/// One shared vocabulary for what happened to a baseline entry, so `generate`, `update`, `prune`,
/// `diff`, and `verify` describe the same entry with the same word. Values split into what
/// comparison found (<see cref="New"/>, <see cref="Existing"/>, <see cref="Stale"/>,
/// <see cref="Ambiguous"/>, <see cref="Configuration"/>) and what a write did with it
/// (<see cref="Added"/>, <see cref="Kept"/>, <see cref="Changed"/>, <see cref="Resolved"/>).
/// </summary>
public enum BaselineEntryLifecycle
{
    /// <summary>Current violation with no matching baseline entry.</summary>
    New,

    /// <summary>A <see cref="New"/> violation this operation materialized as a baseline entry.</summary>
    Added,

    /// <summary>Baseline entry matching exactly one current violation candidate.</summary>
    Existing,

    /// <summary>An <see cref="Existing"/> entry carried into the output unchanged.</summary>
    Kept,

    /// <summary>An <see cref="Existing"/> entry carried through with a display field regenerated.</summary>
    Changed,

    /// <summary>Entry matching no current candidate that this operation did not remove.</summary>
    Stale,

    /// <summary>Entry matching no current candidate that this operation removed from the output.</summary>
    Resolved,

    /// <summary>Entry correlating to more than one current candidate — never rewritten or removed.</summary>
    Ambiguous,

    /// <summary>Entry whose contract id does not exist in the current policy.</summary>
    Configuration,
}

/// <summary>
/// Canonical wire names for <see cref="BaselineEntryLifecycle"/>. These are the exact strings that
/// appear in `--json` output and in the `counts` object, so consumers can branch on them without
/// parsing display text.
/// </summary>
public static class BaselineEntryLifecycleNames
{
    public const string New = "new";
    public const string Added = "added";
    public const string Existing = "existing";
    public const string Kept = "kept";
    public const string Changed = "changed";
    public const string Stale = "stale";
    public const string Resolved = "resolved";
    public const string Ambiguous = "ambiguous";
    public const string Configuration = "configuration";

    /// <summary>
    /// Every lifecycle name, in lifecycle order. The `counts` object carries all of them — including
    /// the ones the invoked operation cannot produce, reported as zero — so one shape reads back from
    /// every baseline subcommand.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        New, Added, Existing, Kept, Changed, Stale, Resolved, Ambiguous, Configuration,
    };

    public static string WireName(BaselineEntryLifecycle lifecycle)
    {
        return lifecycle switch
        {
            BaselineEntryLifecycle.New => New,
            BaselineEntryLifecycle.Added => Added,
            BaselineEntryLifecycle.Existing => Existing,
            BaselineEntryLifecycle.Kept => Kept,
            BaselineEntryLifecycle.Changed => Changed,
            BaselineEntryLifecycle.Stale => Stale,
            BaselineEntryLifecycle.Resolved => Resolved,
            BaselineEntryLifecycle.Ambiguous => Ambiguous,
            BaselineEntryLifecycle.Configuration => Configuration,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycle), lifecycle, "Unknown baseline entry lifecycle."),
        };
    }
}

/// <summary>
/// One baseline entry paired with what this operation did to it.
/// </summary>
public sealed record BaselineLifecycleEntry(
    ArchitectureBaselineComparisonEntry Entry,
    BaselineEntryLifecycle Lifecycle);
