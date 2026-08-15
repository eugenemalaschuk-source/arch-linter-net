namespace ArchLinterNet.Core.Model;

/// <summary>
/// The one lifecycle vocabulary `generate`, `migrate`, `update`, `prune`, `diff`, and `verify` share,
/// as fixed by the `adoption-stabilization-compatibility` capability. These seven values are the whole
/// vocabulary: a command must classify an entry as one of them rather than introduce its own word.
/// </summary>
/// <remarks>
/// What a command <em>did</em> with an entry (kept it, added it, removed it) is a separate axis — see
/// <see cref="BaselineEntryDisposition"/>. Folding disposition into the status would fork the shared
/// vocabulary, which is exactly what callers branching on `status` cannot absorb.
/// <para>
/// Per the capability contract, <see cref="Changed"/>, <see cref="Stale"/>,
/// <see cref="Ambiguous"/>, and <see cref="ConfigurationError"/> never count as a suppressed finding:
/// only <see cref="Matched"/> means an entry and a current finding have equal canonical identity.
/// </para>
/// </remarks>
public enum BaselineEntryLifecycle
{
    /// <summary>A current finding has no exact baseline entry.</summary>
    New,

    /// <summary>An entry and a current finding have equal canonical identity.</summary>
    Matched,

    /// <summary>A valid, evaluable baseline identity has no current finding — the debt was fixed.</summary>
    Resolved,

    /// <summary>
    /// The entry references a contract, family, source instance, schema, or identity form that is no
    /// longer valid or evaluable. Distinct from <see cref="Resolved"/>: the debt may still exist, but
    /// the entry can no longer be evaluated against it.
    /// </summary>
    Stale,

    /// <summary>
    /// A deterministic predecessor/successor relationship can be shown but canonical identity differs,
    /// so the entry does not suppress until explicitly reviewed.
    /// </summary>
    Changed,

    /// <summary>More than one candidate could correspond to the entry, and the tool refuses to guess.</summary>
    Ambiguous,

    /// <summary>Malformed, unsupported, or inconsistent input prevents safe classification.</summary>
    ConfigurationError,
}

/// <summary>
/// What a command did with an entry, orthogonal to its <see cref="BaselineEntryLifecycle"/>. This is
/// how `update` and `prune` report differing outcomes for the same classification without either of
/// them renaming the classification.
/// </summary>
public enum BaselineEntryDisposition
{
    /// <summary>Read-only classification; nothing was written.</summary>
    Reported,

    /// <summary>Recorded in the proposed document as a new entry.</summary>
    Added,

    /// <summary>Carried into the proposed document unchanged.</summary>
    Retained,

    /// <summary>Left out of the proposed document.</summary>
    Removed,
}

/// <summary>
/// Canonical wire names. These exact strings appear in `--json` output and in the `counts` object, so
/// consumers can branch on them without parsing display text.
/// </summary>
public static class BaselineEntryLifecycleNames
{
    public const string New = "new";
    public const string Matched = "matched";
    public const string Resolved = "resolved";
    public const string Stale = "stale";
    public const string Changed = "changed";
    public const string Ambiguous = "ambiguous";
    public const string ConfigurationError = "configuration-error";

    /// <summary>
    /// Every lifecycle name, in lifecycle order. The `counts` object carries all of them — including
    /// the ones a given invocation cannot produce, reported as zero — so one shape reads back from
    /// every baseline subcommand.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        New, Matched, Resolved, Stale, Changed, Ambiguous, ConfigurationError,
    };

    public static string WireName(BaselineEntryLifecycle lifecycle)
    {
        return lifecycle switch
        {
            BaselineEntryLifecycle.New => New,
            BaselineEntryLifecycle.Matched => Matched,
            BaselineEntryLifecycle.Resolved => Resolved,
            BaselineEntryLifecycle.Stale => Stale,
            BaselineEntryLifecycle.Changed => Changed,
            BaselineEntryLifecycle.Ambiguous => Ambiguous,
            BaselineEntryLifecycle.ConfigurationError => ConfigurationError,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycle), lifecycle, "Unknown baseline entry lifecycle."),
        };
    }

    /// <summary>
    /// True when the value means "an entry and a current finding have equal canonical identity". Only
    /// <see cref="BaselineEntryLifecycle.Matched"/> qualifies — the guarantee that `changed`, `stale`,
    /// `ambiguous`, and `configuration-error` never silently suppress a current finding.
    /// </summary>
    public static bool Suppresses(BaselineEntryLifecycle lifecycle)
    {
        return lifecycle == BaselineEntryLifecycle.Matched;
    }
}

public static class BaselineEntryDispositionNames
{
    public const string Reported = "reported";
    public const string Added = "added";
    public const string Retained = "retained";
    public const string Removed = "removed";

    public static string WireName(BaselineEntryDisposition disposition)
    {
        return disposition switch
        {
            BaselineEntryDisposition.Reported => Reported,
            BaselineEntryDisposition.Added => Added,
            BaselineEntryDisposition.Retained => Retained,
            BaselineEntryDisposition.Removed => Removed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition), disposition, "Unknown baseline entry disposition."),
        };
    }
}

/// <summary>
/// One baseline entry with its shared-vocabulary classification and what this command did with it.
/// </summary>
public sealed record BaselineLifecycleEntry(
    ArchitectureBaselineComparisonEntry Entry,
    BaselineEntryLifecycle Lifecycle,
    BaselineEntryDisposition Disposition = BaselineEntryDisposition.Reported);
