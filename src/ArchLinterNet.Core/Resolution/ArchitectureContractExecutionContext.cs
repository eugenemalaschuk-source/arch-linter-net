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

    public bool IsIgnored(string sourceType, string forbiddenReference)
    {
        bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceType, forbiddenReference, _ignoredViolations, _tracker);

        if (!ignored && ContractId != null && _contractGroup != null && _baselineCandidates != null)
        {
            _baselineCandidates.Add(new ArchitectureBaselineCandidate(_contractGroup, ContractId, sourceType, forbiddenReference));
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
                ContractName, ContractId, i, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason));
        }
    }
}
