using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

internal sealed class ArchitectureContractExecutionContext
{
    private readonly IReadOnlyList<ArchitectureIgnoredViolation> _ignoredViolations;
    private readonly ArchitectureIgnoreUsageTracker? _tracker;
    private readonly string? _contractGroup;
    private readonly List<ArchitectureBaselineCandidate>? _baselineCandidates;
    private readonly List<ArchitectureBaselineCandidate> _findingIdentityCandidates;

    // Assigns each occurrence's non-line-based discriminator live, in deterministic call order,
    // incremented unconditionally (before the ignore decision is known) — so a baselined occurrence's
    // index matches what generation originally assigned it, whether or not this particular call ends
    // up suppressed. A post-hoc pass over only the surviving (non-ignored) candidates cannot reproduce
    // this: it would renumber survivors contiguously from zero, diverging from what suppressed
    // occurrences were actually numbered.
    private readonly Dictionary<ArchitectureViolationIdentity, int> _occurrenceCounters = new();

    public ArchitectureContractExecutionContext(
        string contractName,
        string? contractId,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        bool enableUnmatchedIgnoreTracking,
        string? contractGroup,
        List<ArchitectureBaselineCandidate>? baselineCandidates,
        List<ArchitectureBaselineCandidate>? findingIdentityCandidates = null)
    {
        ContractName = contractName ?? throw new ArgumentNullException(nameof(contractName));
        ContractId = contractId;
        _ignoredViolations = ignoredViolations ?? throw new ArgumentNullException(nameof(ignoredViolations));
        _tracker = enableUnmatchedIgnoreTracking && ignoredViolations.Count > 0
            ? new ArchitectureIgnoreUsageTracker()
            : null;
        // contractGroup is kept regardless of tracking so structured-identity ignore matching (which
        // version-2 baseline entries require) works whether or not unmatched-ignore tracking / baseline
        // candidate collection is enabled for this run.
        _contractGroup = contractGroup;
        _baselineCandidates = enableUnmatchedIgnoreTracking ? baselineCandidates : null;
        _findingIdentityCandidates = findingIdentityCandidates ?? new List<ArchitectureBaselineCandidate>();
    }

    public string ContractName { get; }

    public string? ContractId { get; }

    public bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        string? sourceAssembly = null,
        string? targetAssembly = null,
        string? targetType = null,
        string? sourceMember = null,
        string? targetMember = null,
        string? configuration = null,
        Action<ArchitectureBaselineCandidate>? observeCandidate = null)
    {
        ArchitectureViolationIdentity? liveIdentity = BuildLiveIdentity(
            sourceType, sourceAssembly, targetAssembly, targetType, sourceMember, targetMember, configuration);

        bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceType, forbiddenReference, _ignoredViolations, _tracker, liveIdentity);

        if (ContractId != null && liveIdentity != null)
        {
            var candidate = new ArchitectureBaselineCandidate(
                _contractGroup!, ContractId, sourceType, forbiddenReference, liveIdentity);

            if (observeCandidate == null)
            {
                // _baselineCandidates feeds a debt-gate/baseline comparison against a *loaded*
                // baseline (ArchitectureAnalysisSnapshot.CollectBaselineCandidates, reused by
                // health/gate) and must see every occurrence, matched or not, to classify baseline
                // entries as Frozen/Resolved/New. The standalone baseline generate/update/verify/diff
                // flows that also populate this list never load a baseline for their own
                // candidate-collection pass (ignored is always false there), so recording matched
                // occurrences too has no effect on them.
                _baselineCandidates?.Add(candidate);
            }
            else
            {
                // A caller with its own observeCandidate delegate (cycle checking: it filters
                // candidates through EdgeParticipatesInCycle before deciding whether to record them
                // via AddCycleBaselineCandidates/Record, which shares this same underlying list) owns
                // deciding what reaches _baselineCandidates. Only a still-live occurrence participates
                // in that decision -- a suppressed one is already reviewed.
                if (!ignored)
                {
                    observeCandidate(candidate);
                }
            }

            // _findingIdentityCandidates (occurrence attribution) remains scoped to still-live,
            // unmatched occurrences -- a suppressed occurrence must not be attributed as a new finding.
            if (!ignored)
            {
                _findingIdentityCandidates.Add(candidate);
            }
        }

        return ignored;
    }

    public bool IsIgnoredWithAliases(
        string sourceType,
        IReadOnlyList<string> forbiddenReferenceAliases,
        string canonicalForbiddenReference,
        string? sourceAssembly = null,
        string? targetAssembly = null,
        string? targetType = null,
        string? sourceMember = null,
        string? targetMember = null,
        string? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(forbiddenReferenceAliases);

        ArchitectureViolationIdentity? liveIdentity = BuildLiveIdentity(
            sourceType, sourceAssembly, targetAssembly, targetType, sourceMember, targetMember, configuration);

        bool ignored = forbiddenReferenceAliases.Any(alias =>
            ArchitectureIgnoreMatcher.IsIgnored(sourceType, alias, _ignoredViolations, _tracker, liveIdentity));

        if (ContractId != null && liveIdentity != null)
        {
            var candidate = new ArchitectureBaselineCandidate(
                _contractGroup!, ContractId, sourceType, canonicalForbiddenReference, liveIdentity);

            // See the matching comment in IsIgnored: _baselineCandidates must see every occurrence,
            // matched or not, for debt-gate comparison against a loaded baseline.
            _baselineCandidates?.Add(candidate);
            if (!ignored)
            {
                _findingIdentityCandidates.Add(candidate);
            }
        }

        return ignored;
    }

    private ArchitectureViolationIdentity? BuildLiveIdentity(
        string sourceType, string? sourceAssembly, string? targetAssembly, string? targetType, string? sourceMember,
        string? targetMember, string? configuration = null)
    {
        if (ContractId == null || _contractGroup == null)
        {
            return null;
        }

        string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(_contractGroup);

        var zeroed = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            contractFamily,
            ArchitectureViolationIdentity.ResolveKind(contractFamily),
            ContractId,
            sourceAssembly,
            sourceType,
            sourceMember,
            targetAssembly,
            targetType,
            targetMember,
            0,
            configuration);

        int occurrence = _occurrenceCounters.TryGetValue(zeroed, out int count) ? count : 0;
        _occurrenceCounters[zeroed] = occurrence + 1;

        return zeroed with { Occurrence = occurrence };
    }

    public void CollectUnmatchedIgnores(List<ArchitectureUnmatchedIgnoredViolation> result)
    {
        if (_tracker == null)
        {
            return;
        }

        for (int i = 0; i < _ignoredViolations.Count; i++)
        {
            if (_tracker.IsMatched(i))
            {
                continue;
            }

            ArchitectureIgnoredViolation ignore = _ignoredViolations[i];
            result.Add(new ArchitectureUnmatchedIgnoredViolation(
                ContractName, ContractId, i, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason)
            {
                ContractGroup = _contractGroup
            });
        }
    }
}
