namespace ArchLinterNet.Core.Model;

/// <summary>One canonical current-policy architecture-waiver lifecycle record.</summary>
public sealed record ArchitectureWaiverLifecycleRecord(
    string Id,
    string State,
    string ContractName,
    string? ContractId,
    string ContractGroup,
    string SourceType,
    string ForbiddenReference,
    string? TargetFingerprint,
    string Reason,
    string? Owner,
    string? Issue,
    DateOnly? Introduced,
    DateOnly? Expires,
    DateOnly EvaluationDate,
    bool MatchesGovernedFinding)
{
    /// <summary>Composed policy source location for the waiver declaration.</summary>
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }
}
