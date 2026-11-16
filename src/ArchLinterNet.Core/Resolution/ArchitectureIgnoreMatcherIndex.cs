using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

/// <summary>
/// Immutable, contract-scoped lookup state for architecture ignore entries.
/// </summary>
internal sealed class ArchitectureIgnoreMatcherIndex
{
    private const long MissingIdentityIndexScanMultiplier = 4;

    private readonly IReadOnlyList<ArchitectureIgnoredViolation> _ignoredViolations;
    private Lazy<LiveIdentityIndex>? _liveIdentityIndex;
    private Lazy<MissingIdentityIndex>? _missingIdentityIndex;
    private readonly CancellationToken _cancellationToken;
    private readonly ArchitectureIgnoreMatcherWorkCounter? _workCounter;
    private long _missingIdentityScanWork;
    private int _missingIdentityIndexRequested;

    private ArchitectureIgnoreMatcherIndex(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        _ignoredViolations = ignoredViolations;
        _cancellationToken = cancellationToken;
        _workCounter = workCounter;
    }

    internal static ArchitectureIgnoreMatcherIndex Build(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken = default,
        ArchitectureIgnoreMatcherWorkCounter? workCounter = null)
    {
        ArgumentNullException.ThrowIfNull(ignoredViolations);
        cancellationToken.ThrowIfCancellationRequested();
        return new ArchitectureIgnoreMatcherIndex(
            ignoredViolations,
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
        if (liveIdentity is null)
        {
            return null;
        }

        LiveIdentityIndex liveIdentityIndex = GetLiveIdentityIndex();
        if (liveIdentityIndex.WaiverFingerprints.Count == 0)
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
        if (liveIdentity is null && Volatile.Read(ref _missingIdentityIndexRequested) == 0)
        {
            return IsIgnoredWithoutIdentityOrdered(
                sourceType,
                forbiddenReference,
                usageTracker,
                out matchedByLoadedBaseline);
        }

        LiveIdentityIndex? liveIndex = liveIdentity is null ? null : GetLiveIdentityIndex();
        MissingIdentityIndex? missingIdentityIndex = liveIdentity is null ? GetMissingIdentityIndex() : null;
        Dictionary<IdentityKey, IgnoreEntry[]>? identities = liveIndex?.Identities;
        IgnoreEntry[]? identityCandidates = null;
        if (liveIdentity is not null && identities is { Count: > 0 })
        {
            RecordExactLookupProbe();
            identities.TryGetValue(IdentityKey.From(liveIdentity), out identityCandidates);
        }

        Dictionary<string, IgnoreEntry[]>? waiverFingerprints = liveIndex?.WaiverFingerprints;
        IgnoreEntry[]? waiverCandidates = null;
        if (liveIdentity is not null && liveWaiverFingerprint is not null && waiverFingerprints is { Count: > 0 })
        {
            RecordExactLookupProbe();
            waiverFingerprints.TryGetValue(liveWaiverFingerprint, out waiverCandidates);
        }

        Dictionary<PatternPair, IgnoreEntry[]> literalPairs = liveIndex?.LiteralPairs
            ?? missingIdentityIndex!.LiteralPairs;
        IgnoreEntry[]? literalCandidates = null;
        if (literalPairs.Count > 0)
        {
            RecordExactLookupProbe();
            var pair = new PatternPair(
                ArchitectureIgnoreMatcher.Normalize(sourceType),
                ArchitectureIgnoreMatcher.Normalize(forbiddenReference));
            literalPairs.TryGetValue(pair, out literalCandidates);
        }

        IgnoreEntry[] wildcardEntries = liveIndex?.Wildcards ?? missingIdentityIndex!.Wildcards;
        int identityPosition = 0;
        int waiverPosition = 0;
        int literalPosition = 0;
        int wildcardPosition = 0;
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
                out IgnoreEntry exactEntry,
                out ExactSource exactSource);
            bool hasWildcard = TryPeekWildcard(
                wildcardEntries,
                wildcardPosition,
                out IgnoreEntry wildcardEntry);

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

                wildcardPosition++;
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
                ref literalPosition);
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

    private LiveIdentityIndex GetLiveIdentityIndex()
    {
        Lazy<LiveIdentityIndex>? lazyIndex = Volatile.Read(ref _liveIdentityIndex);
        if (lazyIndex is null)
        {
            var createdIndex = new Lazy<LiveIdentityIndex>(
                () => BuildLiveIdentityIndex(_ignoredViolations, _cancellationToken, _workCounter),
                LazyThreadSafetyMode.ExecutionAndPublication);
            lazyIndex = Interlocked.CompareExchange(ref _liveIdentityIndex, createdIndex, null) ?? createdIndex;
        }

        return lazyIndex.Value;
    }

    private MissingIdentityIndex GetMissingIdentityIndex()
    {
        Lazy<MissingIdentityIndex>? lazyIndex = Volatile.Read(ref _missingIdentityIndex);
        if (lazyIndex is null)
        {
            var createdIndex = new Lazy<MissingIdentityIndex>(
                () => BuildMissingIdentityIndex(_ignoredViolations, _cancellationToken, _workCounter),
                LazyThreadSafetyMode.ExecutionAndPublication);
            lazyIndex = Interlocked.CompareExchange(ref _missingIdentityIndex, createdIndex, null) ?? createdIndex;
        }

        return lazyIndex.Value;
    }

    private static LiveIdentityIndex BuildLiveIdentityIndex(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        var identities = new Dictionary<IdentityKey, List<IgnoreEntry>>();
        var waiverFingerprints = new Dictionary<string, List<IgnoreEntry>>(StringComparer.Ordinal);
        var literalPairs = new Dictionary<PatternPair, List<IgnoreEntry>>();
        var wildcardEntries = new List<IgnoreEntry>();

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
                continue;
            }

            var entry = new IgnoreEntry(index, ignore.IsFromLoadedBaseline, ignore.SourceType, ignore.ForbiddenReference);
            if (ignore.HasStructuredWaiverFields)
            {
                if (ignore.Target is { } target && !string.IsNullOrWhiteSpace(target.Fingerprint))
                {
                    Add(waiverFingerprints, target.Fingerprint, entry);
                }

                continue;
            }

            if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
            {
                if (workCounter is not null)
                {
                    workCounter.VersionedIdentityEntriesIndexed++;
                }

                Add(identities, IdentityKey.From(ignore), entry);
                continue;
            }

            AddFallbackPattern(literalPairs, wildcardEntries, entry);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new LiveIdentityIndex(
            Freeze(identities),
            Freeze(waiverFingerprints, StringComparer.Ordinal),
            Freeze(literalPairs),
            wildcardEntries.ToArray());
    }

    private static MissingIdentityIndex BuildMissingIdentityIndex(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        var literalPairs = new Dictionary<PatternPair, List<IgnoreEntry>>();
        var wildcardEntries = new List<IgnoreEntry>();
        for (int index = 0; index < ignoredViolations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArchitectureIgnoredViolation ignore = ignoredViolations[index];
            if (ignore.WaiverValidationError is not null || ignore.HasStructuredWaiverFields)
            {
                continue;
            }

            var entry = new IgnoreEntry(index, ignore.IsFromLoadedBaseline, ignore.SourceType, ignore.ForbiddenReference);
            if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion && workCounter is not null)
            {
                workCounter.VersionedFallbackEntriesProcessed++;
            }

            AddFallbackPattern(literalPairs, wildcardEntries, entry);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new MissingIdentityIndex(Freeze(literalPairs), wildcardEntries.ToArray());
    }

    private bool IsIgnoredWithoutIdentityOrdered(
        string sourceType,
        string forbiddenReference,
        ArchitectureIgnoreUsageTracker? usageTracker,
        out bool matchedByLoadedBaseline)
    {
        matchedByLoadedBaseline = false;
        bool anyMatched = false;
        long entriesScanned = 0;
        try
        {
            for (int index = 0; index < _ignoredViolations.Count; index++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                ArchitectureIgnoredViolation ignore = _ignoredViolations[index];
                entriesScanned++;
                if (ignore.WaiverValidationError is not null || ignore.HasStructuredWaiverFields)
                {
                    continue;
                }

                if (!ArchitectureIgnoreMatcher.MatchesPattern(sourceType, ignore.SourceType)
                    || !ArchitectureIgnoreMatcher.MatchesPattern(forbiddenReference, ignore.ForbiddenReference))
                {
                    continue;
                }

                if (usageTracker is null)
                {
                    matchedByLoadedBaseline = ignore.IsFromLoadedBaseline;
                    return true;
                }

                usageTracker.MarkMatched(index);
                matchedByLoadedBaseline |= ignore.IsFromLoadedBaseline;
                anyMatched = true;
            }

            return anyMatched;
        }
        finally
        {
            RecordMissingIdentityScanWork(entriesScanned);
        }
    }

    private void RecordMissingIdentityScanWork(long entriesScanned)
    {
        if (entriesScanned == 0)
        {
            return;
        }

        if (_workCounter is not null)
        {
            _workCounter.MissingIdentityEntriesScanned += entriesScanned;
        }

        long totalEntriesScanned = Interlocked.Add(ref _missingIdentityScanWork, entriesScanned);
        long indexThreshold = _ignoredViolations.Count * MissingIdentityIndexScanMultiplier;
        if (totalEntriesScanned >= indexThreshold)
        {
            Interlocked.Exchange(ref _missingIdentityIndexRequested, 1);
        }
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

    private static bool TryPeekWildcard(
        IgnoreEntry[] wildcardEntries,
        int wildcardPosition,
        out IgnoreEntry entry)
    {
        if (wildcardPosition >= wildcardEntries.Length)
        {
            entry = default;
            return false;
        }

        entry = wildcardEntries[wildcardPosition];
        return true;
    }

    private static void AdvanceExact(
        ExactSource source,
        ref int identityPosition,
        ref int waiverPosition,
        ref int literalPosition)
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
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
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

    private sealed record LiveIdentityIndex(
        Dictionary<IdentityKey, IgnoreEntry[]> Identities,
        Dictionary<string, IgnoreEntry[]> WaiverFingerprints,
        Dictionary<PatternPair, IgnoreEntry[]> LiteralPairs,
        IgnoreEntry[] Wildcards);

    private sealed record MissingIdentityIndex(
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
    }
}

/// <summary>Optional deterministic work counters used by internal tests and the focused benchmark.</summary>
internal sealed class ArchitectureIgnoreMatcherWorkCounter
{
    internal long IndexEntriesProcessed { get; set; }

    internal long VersionedFallbackEntriesProcessed { get; set; }

    internal long MissingIdentityEntriesScanned { get; set; }

    internal long VersionedIdentityEntriesIndexed { get; set; }

    internal long ExactLookupProbes { get; set; }

    internal long ExactCandidateMatches { get; set; }

    internal long LiveWaiverFingerprintsBuilt { get; set; }

    internal long WildcardEntriesEvaluated { get; set; }

    internal long MatchedOriginalIndices { get; set; }
}
