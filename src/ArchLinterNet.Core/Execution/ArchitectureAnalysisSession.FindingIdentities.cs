using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    internal int FindingIdentityCursor => _findingIdentityCandidates.Count;

    internal IReadOnlyList<ArchitectureViolation> AttachFindingIdentities(
        IReadOnlyCollection<ArchitectureViolation> violations,
        int cursor)
    {
        // Candidates are bucketed by the (contract id, source type) pair that candidate/violation
        // matching requires before it ever looks at references. A single flat list made both the
        // per-violation scan and the post-selection removal proportional to the whole candidate
        // set, which is quadratic for a family like audit_external that emits tens of thousands of
        // candidates for one contract (issue #419: ~1.8k violations against ~54k candidates took
        // ~85 s). Bucket order is the original candidate order and candidate equality implies an
        // identical bucket key, so scanning and removing inside one bucket is equivalent to the
        // flat-list behaviour this replaces.
        Dictionary<CandidateBucketKey, List<ArchitectureBaselineCandidate>> available = BuildCandidateBuckets(cursor);
        var attached = new List<ArchitectureViolation>(violations.Count);

        foreach (ArchitectureViolation violation in violations)
        {
            available.TryGetValue(
                new CandidateBucketKey(violation.ContractId, violation.SourceType),
                out List<ArchitectureBaselineCandidate>? bucket);

            ArchitectureBaselineCandidate[] matches = bucket == null
                ? Array.Empty<ArchitectureBaselineCandidate>()
                : bucket.Where(candidate => CandidateReferencesMatchViolation(candidate, violation)).ToArray();
            ArchitectureBaselineCandidate[] selected = violation.Payload is CompositionPayload
                ? matches
                : SelectOneCandidatePerReportedReference(violation, matches);
            foreach (ArchitectureBaselineCandidate candidate in selected)
            {
                bucket!.Remove(candidate);
            }

            ArchitectureViolationIdentity[] identities = selected
                .Select(candidate => candidate.Identity!)
                .ToArray();
            attached.Add(identities.Length == 0
                ? violation
                : violation with { Identity = identities[0], Identities = identities });
        }

        return attached;
    }

    private Dictionary<CandidateBucketKey, List<ArchitectureBaselineCandidate>> BuildCandidateBuckets(int cursor)
    {
        Dictionary<CandidateBucketKey, List<ArchitectureBaselineCandidate>> buckets = new();

        for (int i = cursor; i < _findingIdentityCandidates.Count; i++)
        {
            ArchitectureBaselineCandidate candidate = _findingIdentityCandidates[i];
            if (candidate.Identity is null)
            {
                continue;
            }

            CandidateBucketKey key = new(candidate.ContractId, candidate.SourceType);
            if (!buckets.TryGetValue(key, out List<ArchitectureBaselineCandidate>? bucket))
            {
                bucket = new List<ArchitectureBaselineCandidate>();
                buckets[key] = bucket;
            }

            bucket.Add(candidate);
        }

        return buckets;
    }

    // Contract id and source type are the bucket key, so only the reference comparison is left to
    // evaluate per candidate.
    private static bool CandidateReferencesMatchViolation(
        ArchitectureBaselineCandidate candidate,
        ArchitectureViolation violation)
    {
        string? targetMember = candidate.Identity?.TargetMember;
        return violation.ForbiddenReferences.Any(reference =>
            ReferenceMatches(reference, targetMember)
            || ReferenceMatches(reference, candidate.ForbiddenReference));
    }

    private static ArchitectureBaselineCandidate[] SelectOneCandidatePerReportedReference(
        ArchitectureViolation violation,
        IReadOnlyCollection<ArchitectureBaselineCandidate> candidates)
    {
        var selected = new List<ArchitectureBaselineCandidate>();
        // Value-equality set mirroring `selected`: the linear Contains it replaces turned a
        // reference-rich violation into quadratic work (issue #419).
        var selectedSet = new HashSet<ArchitectureBaselineCandidate>();
        foreach (string reference in violation.ForbiddenReferences)
        {
            ArchitectureBaselineCandidate? candidate = candidates.FirstOrDefault(candidate =>
                !selectedSet.Contains(candidate)
                && (ReferenceMatches(reference, candidate.Identity?.TargetMember)
                    || ReferenceMatches(reference, candidate.ForbiddenReference)));
            if (candidate is not null)
            {
                selected.Add(candidate);
                selectedSet.Add(candidate);
            }
        }

        return selected.ToArray();
    }

    // Matches `reportedReference` against `identityReference` either exactly or as an
    // '<identity>@...'/'<identity> ...' prefix. Written without the two string concatenations the
    // prefix checks used to allocate: this runs once per (reported reference, candidate) pair, so
    // the allocations dominated identity attachment for large finding sets (issue #419).
    private static bool ReferenceMatches(string reportedReference, string? identityReference)
    {
        if (identityReference is null || reportedReference.Length < identityReference.Length)
        {
            return false;
        }

        if (reportedReference.Length == identityReference.Length)
        {
            return reportedReference.Equals(identityReference, StringComparison.Ordinal);
        }

        char separator = reportedReference[identityReference.Length];
        return (separator == '@' || separator == ' ')
            && reportedReference.StartsWith(identityReference, StringComparison.Ordinal);
    }

    private readonly record struct CandidateBucketKey(string? ContractId, string SourceType);
}
