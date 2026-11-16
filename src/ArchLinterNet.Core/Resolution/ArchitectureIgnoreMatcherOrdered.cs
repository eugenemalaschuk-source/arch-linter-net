using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureIgnoreMatcherOrdered
{
    internal static bool IsIgnoredWithoutIdentityOrdered(
        string sourceType,
        string forbiddenReference,
        ArchitectureIgnoreUsageTracker? usageTracker,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter,
        ref long missingIdentityScanWork,
        ref int missingIdentityIndexRequested,
        long missingIdentityIndexScanMultiplier,
        out bool matchedByLoadedBaseline)
    {
        matchedByLoadedBaseline = false;
        bool anyMatched = false;
        long entriesScanned = 0;
        try
        {
            for (int index = 0; index < ignoredViolations.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchitectureIgnoredViolation ignore = ignoredViolations[index];
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
            RecordMissingIdentityScanWork(
                entriesScanned,
                ignoredViolations.Count,
                workCounter,
                ref missingIdentityScanWork,
                ref missingIdentityIndexRequested,
                missingIdentityIndexScanMultiplier);
        }
    }

    internal static bool IsIgnoredWithLiveIdentityOrdered(
        string sourceType,
        string forbiddenReference,
        ArchitectureIgnoreUsageTracker? usageTracker,
        ArchitectureViolationIdentity liveIdentity,
        string? liveWaiverFingerprint,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken,
        ArchitectureIgnoreMatcherWorkCounter? workCounter,
        out bool matchedByLoadedBaseline)
    {
        matchedByLoadedBaseline = false;
        bool anyMatched = false;
        long entriesScanned = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int index = 0; index < ignoredViolations.Count; index++)
            {
                if (index != 0 && (index & 63) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                ArchitectureIgnoredViolation ignore = ignoredViolations[index];
                entriesScanned++;
                if (ignore.WaiverValidationError is not null)
                {
                    continue;
                }

                if (ignore.HasStructuredWaiverFields)
                {
                    string? targetFingerprint = ignore.Target?.Fingerprint;
                    if (liveWaiverFingerprint is null
                        || string.IsNullOrWhiteSpace(targetFingerprint)
                        || !string.Equals(targetFingerprint, liveWaiverFingerprint, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else if (ignore.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion)
                {
                    if (!IdentityMatches(liveIdentity, ignore))
                    {
                        continue;
                    }
                }
                else if (!ArchitectureIgnoreMatcher.MatchesPattern(sourceType, ignore.SourceType)
                    || !ArchitectureIgnoreMatcher.MatchesPattern(forbiddenReference, ignore.ForbiddenReference))
                {
                    continue;
                }

                if (workCounter is not null)
                {
                    workCounter.MatchedOriginalIndices++;
                    workCounter.OrderedLiveMatches++;
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
            RecordLiveIdentityScanWork(entriesScanned, workCounter);
        }
    }

    internal static bool HasValidStructuredWaiverFingerprint(
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < ignoredViolations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArchitectureIgnoredViolation ignore = ignoredViolations[index];
            if (ignore.WaiverValidationError is not null)
            {
                continue;
            }

            if (ignore.HasStructuredWaiverFields
                && !string.IsNullOrWhiteSpace(ignore.Target?.Fingerprint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IdentityMatches(
        ArchitectureViolationIdentity liveIdentity,
        ArchitectureIgnoredViolation ignore)
    {
        return string.Equals(ignore.ContractFamily, liveIdentity.ContractFamily, StringComparison.Ordinal)
            && string.Equals(ignore.Kind, liveIdentity.Kind, StringComparison.Ordinal)
            && string.Equals(ignore.SourceAssembly, liveIdentity.SourceAssembly, StringComparison.Ordinal)
            && string.Equals(ignore.SourceType, liveIdentity.SourceType, StringComparison.Ordinal)
            && string.Equals(ignore.SourceMember, liveIdentity.SourceMember, StringComparison.Ordinal)
            && string.Equals(ignore.TargetAssembly, liveIdentity.TargetAssembly, StringComparison.Ordinal)
            && string.Equals(ignore.TargetType, liveIdentity.TargetType, StringComparison.Ordinal)
            && string.Equals(ignore.TargetMember, liveIdentity.TargetMember, StringComparison.Ordinal)
            && (ignore.Occurrence ?? 0) == liveIdentity.Occurrence
            && string.Equals(ignore.Configuration, liveIdentity.Configuration, StringComparison.Ordinal);
    }

    private static void RecordMissingIdentityScanWork(
        long entriesScanned,
        int ignoredViolationsCount,
        ArchitectureIgnoreMatcherWorkCounter? workCounter,
        ref long missingIdentityScanWork,
        ref int missingIdentityIndexRequested,
        long missingIdentityIndexScanMultiplier)
    {
        if (entriesScanned == 0)
        {
            return;
        }

        if (workCounter is not null)
        {
            workCounter.MissingIdentityEntriesScanned += entriesScanned;
        }

        long totalEntriesScanned = Interlocked.Add(ref missingIdentityScanWork, entriesScanned);
        long indexThreshold = ignoredViolationsCount * missingIdentityIndexScanMultiplier;
        if (totalEntriesScanned >= indexThreshold)
        {
            Interlocked.Exchange(ref missingIdentityIndexRequested, 1);
        }
    }

    private static void RecordLiveIdentityScanWork(
        long entriesScanned,
        ArchitectureIgnoreMatcherWorkCounter? workCounter)
    {
        if (entriesScanned == 0 || workCounter is null)
        {
            return;
        }

        workCounter.OrderedLiveQueries++;
        workCounter.OrderedLiveEntriesScanned += entriesScanned;
    }
}
