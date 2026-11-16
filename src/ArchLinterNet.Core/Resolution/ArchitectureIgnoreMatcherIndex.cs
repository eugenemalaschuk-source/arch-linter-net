using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

/// <summary>
/// Immutable, contract-scoped lookup state for architecture ignore entries.
/// </summary>
internal sealed class ArchitectureIgnoreMatcherIndex
{
    private const long MissingIdentityIndexScanMultiplier = 4;
    private const int MaximumInitialDictionaryCapacity = 64;
    private const int SmallLiveIdentityListThreshold = 8;

    private readonly IReadOnlyList<ArchitectureIgnoredViolation> _ignoredViolations;
    private readonly bool _useOrderedLiveIdentityMatcher;
    private readonly bool _hasValidStructuredWaiverFingerprint;
    private Lazy<LiveIdentityIndex>? _liveIdentityIndex;
    private Lazy<MissingIdentityIndex>? _missingIdentityIndex;
    private readonly CancellationToken _cancellationToken;
    private readonly ArchitectureIgnoreMatcherWorkCounter? _workCounter;
    private long _missingIdentityScanWork;
    private int _missingIdentityIndexRequested;

    private ArchitectureIgnoreMatcherIndex(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter,
        bool useOrderedLiveIdentityMatcher,
        bool hasValidStructuredWaiverFingerprint)
    {
        _ignoredViolations = ignoredViolations;
        _cancellationToken = cancellationToken;
        _workCounter = workCounter;
        _useOrderedLiveIdentityMatcher = useOrderedLiveIdentityMatcher;
        _hasValidStructuredWaiverFingerprint = hasValidStructuredWaiverFingerprint;
    }

    internal static ArchitectureIgnoreMatcherIndex Build(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken = default,
        ArchitectureIgnoreMatcherWorkCounter? workCounter = null)
    {
        ArgumentNullException.ThrowIfNull(ignoredViolations);
        cancellationToken.ThrowIfCancellationRequested();
        bool useOrderedLiveIdentityMatcher = ignoredViolations.Count <= SmallLiveIdentityListThreshold;
        bool hasValidStructuredWaiverFingerprint = useOrderedLiveIdentityMatcher
            && ArchitectureIgnoreMatcherOrdered.HasValidStructuredWaiverFingerprint(ignoredViolations, cancellationToken);
        return new ArchitectureIgnoreMatcherIndex(
            ignoredViolations,
            cancellationToken,
            workCounter,
            useOrderedLiveIdentityMatcher,
            hasValidStructuredWaiverFingerprint);
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

        if (_useOrderedLiveIdentityMatcher)
        {
            if (!_hasValidStructuredWaiverFingerprint)
            {
                return null;
            }

            if (_workCounter is not null)
            {
                _workCounter.LiveWaiverFingerprintsBuilt++;
            }

            return ArchitectureWaiverTargetFingerprint.Create(liveIdentity);
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
        if (liveIdentity is not null && _useOrderedLiveIdentityMatcher)
        {
            return ArchitectureIgnoreMatcherOrdered.IsIgnoredWithLiveIdentityOrdered(
                sourceType,
                forbiddenReference,
                usageTracker,
                liveIdentity,
                liveWaiverFingerprint,
                _ignoredViolations,
                _cancellationToken,
                _workCounter,
                out matchedByLoadedBaseline);
        }

        if (liveIdentity is null && Volatile.Read(ref _missingIdentityIndexRequested) == 0)
        {
            return ArchitectureIgnoreMatcherOrdered.IsIgnoredWithoutIdentityOrdered(
                sourceType,
                forbiddenReference,
                usageTracker,
                _ignoredViolations,
                _cancellationToken,
                _workCounter,
                ref _missingIdentityScanWork,
                ref _missingIdentityIndexRequested,
                MissingIdentityIndexScanMultiplier,
                out matchedByLoadedBaseline);
        }

        LiveIdentityIndex? liveIndex = liveIdentity is null ? null : GetLiveIdentityIndex();
        MissingIdentityIndex? missingIdentityIndex = liveIdentity is null ? GetMissingIdentityIndex() : null;
        Dictionary<IdentityKey, CandidateGroup>? identities = liveIndex?.Identities;
        CandidateGroup identityCandidates = default;
        if (liveIdentity is not null && identities is { Count: > 0 })
        {
            RecordExactLookupProbe();
            identities.TryGetValue(IdentityKey.From(liveIdentity), out identityCandidates);
        }

        Dictionary<string, CandidateGroup>? waiverFingerprints = liveIndex?.WaiverFingerprints;
        CandidateGroup waiverCandidates = default;
        if (liveIdentity is not null && liveWaiverFingerprint is not null && waiverFingerprints is { Count: > 0 })
        {
            RecordExactLookupProbe();
            waiverFingerprints.TryGetValue(liveWaiverFingerprint, out waiverCandidates);
        }

        Dictionary<PatternPair, CandidateGroup> literalPairs = liveIndex?.LiteralPairs
            ?? missingIdentityIndex!.LiteralPairs;
        CandidateGroup literalCandidates = default;
        if (literalPairs.Count > 0)
        {
            RecordExactLookupProbe();
            var pair = new PatternPair(
                ArchitectureIgnoreMatcher.Normalize(sourceType),
                ArchitectureIgnoreMatcher.Normalize(forbiddenReference));
            literalPairs.TryGetValue(pair, out literalCandidates);
        }

        WildcardEntry[] wildcardEntries = liveIndex?.Wildcards ?? missingIdentityIndex!.Wildcards;
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
                out WildcardEntry wildcardEntry);

            if (!hasExact && !hasWildcard)
            {
                return anyMatched;
            }

            if (hasWildcard && (!hasExact || wildcardEntry.Entry.Index < exactEntry.Index))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (_workCounter is not null)
                {
                    _workCounter.WildcardEntriesEvaluated++;
                }

                wildcardPosition++;
                if (!wildcardEntry.SourceMatcher.IsMatch(sourceType)
                    || !wildcardEntry.ForbiddenMatcher.IsMatch(forbiddenReference))
                {
                    continue;
                }

                RecordMatch(wildcardEntry.Entry, usageTracker, ref matchedByLoadedBaseline);
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
        Dictionary<PatternPair, CandidateGroup> literalPairs,
        List<WildcardEntry> wildcardEntries,
        Dictionary<string, ArchitectureIgnoreMatcher.CompiledPattern> patternMatchers,
        IgnoreEntry entry,
        int ignoredEntryCapacity)
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
            Add(literalPairs, key, entry, ignoredEntryCapacity);
            return;
        }

        wildcardEntries.Add(new WildcardEntry(
            entry,
            GetPatternMatcher(patternMatchers, entry.SourcePattern),
            GetPatternMatcher(patternMatchers, entry.ForbiddenPattern)));
    }

    private static ArchitectureIgnoreMatcher.CompiledPattern GetPatternMatcher(
        Dictionary<string, ArchitectureIgnoreMatcher.CompiledPattern> patternMatchers,
        string pattern)
    {
        string normalizedPattern = ArchitectureIgnoreMatcher.Normalize(pattern);
        if (!patternMatchers.TryGetValue(normalizedPattern, out ArchitectureIgnoreMatcher.CompiledPattern? matcher))
        {
            matcher = ArchitectureIgnoreMatcher.CompilePattern(normalizedPattern);
            patternMatchers.Add(normalizedPattern, matcher);
        }

        return matcher;
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
        var identities = new Dictionary<IdentityKey, CandidateGroup>();
        var waiverFingerprints = new Dictionary<string, CandidateGroup>(StringComparer.Ordinal);
        var literalPairs = new Dictionary<PatternPair, CandidateGroup>();
        var wildcardEntries = new List<WildcardEntry>();
        var patternMatchers = new Dictionary<string, ArchitectureIgnoreMatcher.CompiledPattern>(StringComparer.Ordinal);

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
                    Add(waiverFingerprints, target.Fingerprint, entry, ignoredViolations.Count);
                }

                continue;
            }

            if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
            {
                if (workCounter is not null)
                {
                    workCounter.VersionedIdentityEntriesIndexed++;
                }

                Add(identities, IdentityKey.From(ignore), entry, ignoredViolations.Count);
                continue;
            }

            AddFallbackPattern(literalPairs, wildcardEntries, patternMatchers, entry, ignoredViolations.Count);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new LiveIdentityIndex(identities, waiverFingerprints, literalPairs, wildcardEntries.ToArray());
    }

    private static MissingIdentityIndex BuildMissingIdentityIndex(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        var literalPairs = new Dictionary<PatternPair, CandidateGroup>();
        var wildcardEntries = new List<WildcardEntry>();
        var patternMatchers = new Dictionary<string, ArchitectureIgnoreMatcher.CompiledPattern>(StringComparer.Ordinal);
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

            AddFallbackPattern(literalPairs, wildcardEntries, patternMatchers, entry, ignoredViolations.Count);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new MissingIdentityIndex(literalPairs, wildcardEntries.ToArray());
    }

    private static void Add<TKey>(
        Dictionary<TKey, CandidateGroup> table,
        TKey key,
        IgnoreEntry entry,
        int capacityHint)
        where TKey : notnull
    {
        if (table.Count == 0 && capacityHint > 0)
        {
            table.EnsureCapacity(Math.Min(capacityHint, MaximumInitialDictionaryCapacity));
        }

        if (table.TryGetValue(key, out CandidateGroup group))
        {
            group.Add(entry);
            table[key] = group;
            return;
        }

        group = default;
        group.Add(entry);
        table.Add(key, group);
    }

    private bool TryPeekExact(
        CandidateGroup identityCandidates,
        int identityPosition,
        CandidateGroup waiverCandidates,
        int waiverPosition,
        CandidateGroup literalCandidates,
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
        CandidateGroup candidates,
        int position,
        ExactSource candidateSource,
        ref int minimumIndex,
        ref IgnoreEntry entry,
        ref ExactSource source,
        ref bool found)
    {
        if (position >= candidates.Count)
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
        WildcardEntry[] wildcardEntries,
        int wildcardPosition,
        out WildcardEntry entry)
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
        Dictionary<IdentityKey, CandidateGroup> Identities,
        Dictionary<string, CandidateGroup> WaiverFingerprints,
        Dictionary<PatternPair, CandidateGroup> LiteralPairs,
        WildcardEntry[] Wildcards);

    private sealed record MissingIdentityIndex(
        Dictionary<PatternPair, CandidateGroup> LiteralPairs,
        WildcardEntry[] Wildcards);

    private struct CandidateGroup
    {
        private IgnoreEntry _first;
        private IgnoreEntry[]? _remaining;

        internal int Count { get; private set; }

        internal IgnoreEntry this[int position] => position == 0 ? _first : _remaining![position - 1];

        internal void Add(IgnoreEntry entry)
        {
            if (Count == 0)
            {
                _first = entry;
                Count = 1;
                return;
            }

            int remainingCount = Count - 1;
            if (_remaining is null)
            {
                _remaining = new IgnoreEntry[4];
            }
            else if (remainingCount == _remaining.Length)
            {
                Array.Resize(ref _remaining, _remaining.Length * 2);
            }

            _remaining[remainingCount] = entry;
            Count++;
        }
    }

    private readonly record struct WildcardEntry(
        IgnoreEntry Entry,
        ArchitectureIgnoreMatcher.CompiledPattern SourceMatcher,
        ArchitectureIgnoreMatcher.CompiledPattern ForbiddenMatcher);

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

    internal long OrderedLiveEntriesScanned { get; set; }

    internal long OrderedLiveQueries { get; set; }

    internal long OrderedLiveMatches { get; set; }

    internal long VersionedIdentityEntriesIndexed { get; set; }

    internal long ExactLookupProbes { get; set; }

    internal long ExactCandidateMatches { get; set; }

    internal long LiveWaiverFingerprintsBuilt { get; set; }

    internal long WildcardEntriesEvaluated { get; set; }

    internal long MatchedOriginalIndices { get; set; }
}
