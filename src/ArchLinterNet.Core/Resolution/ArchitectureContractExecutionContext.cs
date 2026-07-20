using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Resolution;

internal sealed class ArchitectureContractExecutionContext
{
    private readonly IReadOnlyList<ArchitectureIgnoredViolation> _ignoredViolations;
    private readonly ArchitectureIgnoreUsageTracker? _tracker;
    private readonly string? _contractGroup;
    private readonly List<ArchitectureBaselineCandidate>? _baselineCandidates;

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
        _contractGroup = enableUnmatchedIgnoreTracking ? contractGroup : null;
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
        string? targetMember = null)
    {
        bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceType, forbiddenReference, _ignoredViolations, _tracker);

        if (!ignored && ContractId != null && _contractGroup != null && _baselineCandidates != null)
        {
            string contractFamily = ArchitectureViolationIdentity.ResolveContractFamily(_contractGroup);

            // Families that don't yet supply a richer targetMember (every family except method-body
            // and other qualified call sites) still need SOMETHING to discriminate genuinely distinct
            // targets from the same source — falling back to the full forbiddenReference string
            // preserves the old (source_type, forbidden_reference) discrimination exactly, so the
            // occurrence discriminator only kicks in for true duplicates, not distinct targets.
            string effectiveTargetMember = targetMember ?? forbiddenReference;

            var identity = new ArchitectureViolationIdentity(
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
                0);

            _baselineCandidates.Add(new ArchitectureBaselineCandidate(_contractGroup, ContractId, sourceType, forbiddenReference, identity));
        }

        return ignored;
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
