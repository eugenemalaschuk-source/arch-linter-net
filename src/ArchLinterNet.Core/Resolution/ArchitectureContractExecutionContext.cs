using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

internal sealed class ArchitectureContractExecutionContext
{
    private readonly IReadOnlyList<ArchitectureIgnoredViolation> _ignoredViolations;
    private readonly ArchitectureIgnoreUsageTracker? _tracker;
    private readonly string? _contractGroup;
    private readonly List<ArchitectureBaselineCandidate>? _baselineCandidates;

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
        List<ArchitectureBaselineCandidate>? baselineCandidates)
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
    }

    public string ContractName { get; }

    public string? ContractId { get; }

    public bool IsIgnored(
        string sourceType,
        string forbiddenReference,
        string? sourceAssembly = null,
        string? targetAssembly = null,
        string? sourceMember = null,
        string? targetMember = null,
        string? configuration = null)
    {
        ArchitectureViolationIdentity? liveIdentity = BuildLiveIdentity(
            sourceType, forbiddenReference, sourceAssembly, targetAssembly, sourceMember, targetMember, configuration);

        bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceType, forbiddenReference, _ignoredViolations, _tracker, liveIdentity);

        if (!ignored && ContractId != null && _baselineCandidates != null && liveIdentity != null)
        {
            _baselineCandidates.Add(new ArchitectureBaselineCandidate(_contractGroup!, ContractId, sourceType, forbiddenReference, liveIdentity));
        }

        return ignored;
    }

    private ArchitectureViolationIdentity? BuildLiveIdentity(
        string sourceType, string forbiddenReference, string? sourceAssembly, string? targetAssembly, string? sourceMember,
        string? targetMember, string? configuration = null)
    {
        if (ContractId == null || _contractGroup == null)
        {
            return null;
        }

        string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(_contractGroup);

        // Families that don't yet supply a richer targetMember (every family except method-body and
        // other qualified call sites) still need SOMETHING to discriminate genuinely distinct targets
        // from the same source — falling back to the full forbiddenReference string preserves the old
        // (source_type, forbidden_reference) discrimination exactly, so the occurrence discriminator
        // only kicks in for true duplicates, not distinct targets.
        string effectiveTargetMember = targetMember ?? forbiddenReference;

        var zeroed = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            contractFamily,
            ArchitectureViolationIdentity.ResolveKind(contractFamily),
            ContractId,
            sourceAssembly,
            sourceType,
            sourceMember,
            targetAssembly,
            null,
            effectiveTargetMember,
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
