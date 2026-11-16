using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

/// <summary>
/// Immutable, contract-scoped lookup state for architecture ignore entries.
/// </summary>
internal sealed class ArchitectureIgnoreMatcherIndex
{
    private readonly Dictionary<IdentityKey, IgnoreEntry[]> _versionedIdentities;
    private readonly Dictionary<string, IgnoreEntry[]> _waiverFingerprints;
    private readonly Dictionary<PatternPair, IgnoreEntry[]> _literalPairs;
    private readonly Lazy<VersionedFallbackIndex>? _versionedFallbackIndex;
    private readonly IgnoreEntry[] _wildcardFallbacks;
    private readonly CancellationToken _cancellationToken;
    private readonly ArchitectureIgnoreMatcherWorkCounter? _workCounter;

    private ArchitectureIgnoreMatcherIndex(
        Dictionary<IdentityKey, IgnoreEntry[]> versionedIdentities,
        Dictionary<string, IgnoreEntry[]> waiverFingerprints,
        Dictionary<PatternPair, IgnoreEntry[]> literalPairs,
        Lazy<VersionedFallbackIndex>? versionedFallbackIndex,
        IgnoreEntry[] wildcardFallbacks,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        _versionedIdentities = versionedIdentities;
        _waiverFingerprints = waiverFingerprints;
        _literalPairs = literalPairs;
        _versionedFallbackIndex = versionedFallbackIndex;
        _wildcardFallbacks = wildcardFallbacks;
        _cancellationToken = cancellationToken;
        _workCounter = workCounter;
    }

    internal static ArchitectureIgnoreMatcherIndex Build(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken = default,
        ArchitectureIgnoreMatcherWorkCounter? workCounter = null)
    {
        ArgumentNullException.ThrowIfNull(ignoredViolations);

        var versionedIdentities = new Dictionary<IdentityKey, List<IgnoreEntry>>();
        var waiverFingerprints = new Dictionary<string, List<IgnoreEntry>>(StringComparer.Ordinal);
        var literalPairs = new Dictionary<PatternPair, List<IgnoreEntry>>();
        var versionedFallbackEntries = new List<IgnoreEntry>();
        var wildcardFallbacks = new List<IgnoreEntry>();

        for (int index = 0; index < ignoredViolations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workCounter is not null)
            {
                workCounter.IndexEntriesProcessed++;
            }

            ArchitectureIgnoredViolation ignore = ignoredViolations[index];
            if (ignore.WaiverValidationError is not null)
            {
                // Invalid or incomplete structured waiver declarations are lifecycle evidence,
                // never pattern fallbacks. The old ordered matcher failed these entries closed.
                continue;
            }

            var entry = new IgnoreEntry(
                index,
                ignore.IsFromLoadedBaseline,
                ignore.SourceType,
                ignore.ForbiddenReference);

            if (ignore.HasStructuredWaiverFields)
            {
                if (ignore.Target is not null)
                {
                    Add(
                        waiverFingerprints,
                        ignore.Target.Fingerprint,
                        entry);
                }

                // Structured manual waivers never fall through to their text/glob fields.
                continue;
            }

            if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
            {
                Add(versionedIdentities, IdentityKey.From(ignore), entry);
                versionedFallbackEntries.Add(entry);
                continue;
            }

            AddFallbackPattern(literalPairs, wildcardFallbacks, entry);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Lazy<VersionedFallbackIndex>? versionedFallbackIndex = versionedFallbackEntries.Count == 0
            ? null
            : new Lazy<VersionedFallbackIndex>(
                () => BuildVersionedFallbackIndex(versionedFallbackEntries.ToArray(), cancellationToken, workCounter),
                LazyThreadSafetyMode.ExecutionAndPublication);

        return new ArchitectureIgnoreMatcherIndex(
            Freeze(versionedIdentities),
            Freeze(waiverFingerprints, StringComparer.Ordinal),
            Freeze(literalPairs),
            versionedFallbackIndex,
            wildcardFallbacks.ToArray(),
            cancellationToken,
            workCounter);
    }

    /// <summary>
    /// Computes a structured waiver fingerprint only when this contract has a valid structured
    /// waiver target and the finding has a live identity. Call once per finding and reuse the value
    /// for each alias.
    /// </summary>
    internal string? CreateLiveWaiverFingerprint(ArchitectureViolationIdentity? liveIdentity)
    {
        if (liveIdentity is null || _waiverFingerprints.Count == 0)
        {
            return null;
        }

        if (_workCounter is not null)
        {
            _workCounter.LiveWaiverFingerprintsBuilt++;
        }

        return ArchitectureWaiverTargetFingerprint.Create(liveIdentity);
    }

    internal void ThrowIfCancellationRequested()
    {
        _cancellationToken.ThrowIfCancellationRequested();
    }

    internal bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        ArchitectureIgnoreUsageTracker? usageTracker,
        ArchitectureViolationIdentity? liveIdentity,
        string? liveWaiverFingerprint,
        out bool matchedByLoadedBaseline)
    {
        matchedByLoadedBaseline = false;
        bool tracking = usageTracker is not null;
        VersionedFallbackIndex? versionedFallback = liveIdentity is null
            ? _versionedFallbackIndex?.Value
            : null;

        IgnoreEntry[]? identityCandidates = null;
        if (liveIdentity is not null && _versionedIdentities.Count > 0)
        {
            RecordExactLookupProbe();
            _versionedIdentities.TryGetValue(IdentityKey.From(liveIdentity), out identityCandidates);
        }

        IgnoreEntry[]? waiverCandidates = null;
        if (liveIdentity is not null && liveWaiverFingerprint is not null && _waiverFingerprints.Count > 0)
        {
            RecordExactLookupProbe();
            _waiverFingerprints.TryGetValue(liveWaiverFingerprint, out waiverCandidates);
        }

        IgnoreEntry[]? literalCandidates = null;
        IgnoreEntry[]? versionedLiteralCandidates = null;
        bool lookupLiteralPairs = _literalPairs.Count > 0
            || (versionedFallback is not null && versionedFallback.LiteralPairs.Count > 0);
        if (lookupLiteralPairs)
        {
            RecordExactLookupProbe();
            var pair = new PatternPair(
                ArchitectureIgnoreMatcher.Normalize(sourceType),
                ArchitectureIgnoreMatcher.Normalize(forbiddenReference));
            _literalPairs.TryGetValue(pair, out literalCandidates);

            if (versionedFallback is not null && versionedFallback.LiteralPairs.Count > 0)
            {
                RecordExactLookupProbe();
                versionedFallback.LiteralPairs.TryGetValue(pair, out versionedLiteralCandidates);
            }
        }

        int identityPosition = 0;
        int waiverPosition = 0;
        int literalPosition = 0;
        int versionedLiteralPosition = 0;
        int wildcardPosition = 0;
        int versionedWildcardPosition = 0;
        bool anyMatched = false;

        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            bool hasExact = TryPeekExact(
                identityCandidates,
                identityPosition,
                waiverCandidates,
                waiverPosition,
                literalCandidates,
                literalPosition,
                versionedLiteralCandidates,
                versionedLiteralPosition,
                out IgnoreEntry exactEntry,
                out ExactSource exactSource);
            bool hasWildcard = TryPeekWildcard(
                versionedFallback?.Wildcards,
                wildcardPosition,
                versionedWildcardPosition,
                out IgnoreEntry wildcardEntry,
                out WildcardSource wildcardSource);

            if (!hasExact && !hasWildcard)
            {
                return anyMatched;
            }

            if (hasWildcard && (!hasExact || wildcardEntry.Index < exactEntry.Index))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (_workCounter is not null)
                {
                    _workCounter.WildcardEntriesEvaluated++;
                }

                AdvanceWildcard(wildcardSource, ref wildcardPosition, ref versionedWildcardPosition);
                if (!ArchitectureIgnoreMatcher.MatchesPattern(sourceType, wildcardEntry.SourcePattern)
                    || !ArchitectureIgnoreMatcher.MatchesPattern(forbiddenReference, wildcardEntry.ForbiddenPattern))
                {
                    continue;
                }

                RecordMatch(wildcardEntry, usageTracker, ref matchedByLoadedBaseline);
                anyMatched = true;
                if (!tracking)
                {
                    return true;
                }

                continue;
            }

            AdvanceExact(
                exactSource,
                ref identityPosition,
                ref waiverPosition,
                ref literalPosition,
                ref versionedLiteralPosition);
            if (_workCounter is not null)
            {
                _workCounter.ExactCandidateMatches++;
            }

            RecordMatch(exactEntry, usageTracker, ref matchedByLoadedBaseline);
            anyMatched = true;
            if (!tracking)
            {
                return true;
            }
        }
    }

    private static void AddFallbackPattern(
        Dictionary<PatternPair, List<IgnoreEntry>> literalPairs,
        List<IgnoreEntry> wildcardEntries,
        IgnoreEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.SourcePattern)
            || string.IsNullOrWhiteSpace(entry.ForbiddenPattern))
        {
            return;
        }

        bool sourceHasGlob = ArchitectureIgnoreMatcher.ContainsGlobSyntax(entry.SourcePattern);
        bool referenceHasGlob = ArchitectureIgnoreMatcher.ContainsGlobSyntax(entry.ForbiddenPattern);
        if (!sourceHasGlob && !referenceHasGlob)
        {
            var key = new PatternPair(
                ArchitectureIgnoreMatcher.Normalize(entry.SourcePattern),
                ArchitectureIgnoreMatcher.Normalize(entry.ForbiddenPattern));
            Add(literalPairs, key, entry);
            return;
        }

        wildcardEntries.Add(entry);
    }

    private static VersionedFallbackIndex BuildVersionedFallbackIndex(
        IReadOnlyList<IgnoreEntry> entries,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        var literalPairs = new Dictionary<PatternPair, List<IgnoreEntry>>();
        var wildcardEntries = new List<IgnoreEntry>();

        for (int index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workCounter is not null)
            {
                workCounter.VersionedFallbackEntriesProcessed++;
            }

            AddFallbackPattern(literalPairs, wildcardEntries, entries[index]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new VersionedFallbackIndex(Freeze(literalPairs), wildcardEntries.ToArray());
    }

    private static void Add<TKey>(Dictionary<TKey, List<IgnoreEntry>> table, TKey key, IgnoreEntry entry)
        where TKey : notnull
    {
        if (!table.TryGetValue(key, out List<IgnoreEntry>? entries))
        {
            entries = new List<IgnoreEntry>();
            table.Add(key, entries);
        }

        entries.Add(entry);
    }

    private static Dictionary<TKey, IgnoreEntry[]> Freeze<TKey>(
        Dictionary<TKey, List<IgnoreEntry>> table,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var frozen = new Dictionary<TKey, IgnoreEntry[]>(table.Count, comparer);
        foreach ((TKey key, List<IgnoreEntry> entries) in table)
        {
            frozen.Add(key, entries.ToArray());
        }

        return frozen;
    }

    private bool TryPeekExact(
        IgnoreEntry[]? identityCandidates,
        int identityPosition,
        IgnoreEntry[]? waiverCandidates,
        int waiverPosition,
        IgnoreEntry[]? literalCandidates,
        int literalPosition,
        IgnoreEntry[]? versionedLiteralCandidates,
        int versionedLiteralPosition,
        out IgnoreEntry entry,
        out ExactSource source)
    {
        int minimumIndex = int.MaxValue;
        entry = default;
        source = ExactSource.Identity;
        bool found = false;

        Consider(identityCandidates, identityPosition, ExactSource.Identity, ref minimumIndex, ref entry, ref source, ref found);
        Consider(waiverCandidates, waiverPosition, ExactSource.Waiver, ref minimumIndex, ref entry, ref source, ref found);
        Consider(literalCandidates, literalPosition, ExactSource.Literal, ref minimumIndex, ref entry, ref source, ref found);
        Consider(
            versionedLiteralCandidates,
            versionedLiteralPosition,
            ExactSource.VersionedLiteral,
            ref minimumIndex,
            ref entry,
            ref source,
            ref found);

        return found;
    }

    private static void Consider(
        IgnoreEntry[]? candidates,
        int position,
        ExactSource candidateSource,
        ref int minimumIndex,
        ref IgnoreEntry entry,
        ref ExactSource source,
        ref bool found)
    {
        if (candidates is null || position >= candidates.Length)
        {
            return;
        }

        IgnoreEntry candidate = candidates[position];
        if (candidate.Index >= minimumIndex)
        {
            return;
        }

        minimumIndex = candidate.Index;
        entry = candidate;
        source = candidateSource;
        found = true;
    }

    private bool TryPeekWildcard(
        IgnoreEntry[]? versionedFallbacks,
        int wildcardPosition,
        int versionedWildcardPosition,
        out IgnoreEntry entry,
        out WildcardSource source)
    {
        IgnoreEntry? normal = GetAt(_wildcardFallbacks, wildcardPosition);
        IgnoreEntry? versioned = versionedFallbacks is not null
            ? GetAt(versionedFallbacks, versionedWildcardPosition)
            : null;

        if (normal is null && versioned is null)
        {
            entry = default;
            source = WildcardSource.Normal;
            return false;
        }

        if (versioned is null || (normal is not null && normal.Value.Index < versioned.Value.Index))
        {
            entry = normal!.Value;
            source = WildcardSource.Normal;
            return true;
        }

        entry = versioned.Value;
        source = WildcardSource.Versioned;
        return true;
    }

    private static IgnoreEntry? GetAt(IgnoreEntry[] entries, int position)
    {
        return position < entries.Length ? entries[position] : null;
    }

    private static void AdvanceExact(
        ExactSource source,
        ref int identityPosition,
        ref int waiverPosition,
        ref int literalPosition,
        ref int versionedLiteralPosition)
    {
        switch (source)
        {
            case ExactSource.Identity:
                identityPosition++;
                break;
            case ExactSource.Waiver:
                waiverPosition++;
                break;
            case ExactSource.Literal:
                literalPosition++;
                break;
            case ExactSource.VersionedLiteral:
                versionedLiteralPosition++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static void AdvanceWildcard(
        WildcardSource source,
        ref int wildcardPosition,
        ref int versionedWildcardPosition)
    {
        if (source == WildcardSource.Normal)
        {
            wildcardPosition++;
        }
        else
        {
            versionedWildcardPosition++;
        }
    }

    private void RecordMatch(
        IgnoreEntry entry,
        ArchitectureIgnoreUsageTracker? usageTracker,
        ref bool matchedByLoadedBaseline)
    {
        usageTracker?.MarkMatched(entry.Index);
        matchedByLoadedBaseline |= entry.IsFromLoadedBaseline;

        if (_workCounter is not null)
        {
            _workCounter.MatchedOriginalIndices++;
        }
    }

    private void RecordExactLookupProbe()
    {
        if (_workCounter is not null)
        {
            _workCounter.ExactLookupProbes++;
        }
    }

    private readonly record struct IdentityKey(
        string? ContractFamily,
        string? Kind,
        string? SourceAssembly,
        string? SourceType,
        string? SourceMember,
        string? TargetAssembly,
        string? TargetType,
        string? TargetMember,
        int Occurrence,
        string? Configuration)
    {
        internal static IdentityKey From(ArchitectureViolationIdentity identity)
        {
            return new IdentityKey(
                identity.ContractFamily,
                identity.Kind,
                identity.SourceAssembly,
                identity.SourceType,
                identity.SourceMember,
                identity.TargetAssembly,
                identity.TargetType,
                identity.TargetMember,
                identity.Occurrence,
                identity.Configuration);
        }

        internal static IdentityKey From(ArchitectureIgnoredViolation ignore)
        {
            return new IdentityKey(
                ignore.ContractFamily,
                ignore.Kind,
                ignore.SourceAssembly,
                ignore.SourceType,
                ignore.SourceMember,
                ignore.TargetAssembly,
                ignore.TargetType,
                ignore.TargetMember,
                ignore.Occurrence ?? 0,
                ignore.Configuration);
        }
    }

    private readonly record struct PatternPair(string SourceType, string ForbiddenReference);

    private sealed record VersionedFallbackIndex(
        Dictionary<PatternPair, IgnoreEntry[]> LiteralPairs,
        IgnoreEntry[] Wildcards);

    private readonly record struct IgnoreEntry(
        int Index,
        bool IsFromLoadedBaseline,
        string SourcePattern,
        string ForbiddenPattern);

    private enum ExactSource
    {
        Identity,
        Waiver,
        Literal,
        VersionedLiteral,
    }

    private enum WildcardSource
    {
        Normal,
        Versioned,
    }
}

/// <summary>Optional deterministic work counters used by internal tests and the focused benchmark.</summary>
internal sealed class ArchitectureIgnoreMatcherWorkCounter
{
    internal long IndexEntriesProcessed { get; set; }

    internal long VersionedFallbackEntriesProcessed { get; set; }

    internal long ExactLookupProbes { get; set; }

    internal long ExactCandidateMatches { get; set; }

    internal long LiveWaiverFingerprintsBuilt { get; set; }

    internal long WildcardEntriesEvaluated { get; set; }

    internal long MatchedOriginalIndices { get; set; }
}
