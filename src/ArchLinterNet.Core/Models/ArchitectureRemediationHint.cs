namespace ArchLinterNet.Core.Model;

/// <summary>Finite, machine-readable categories for evidence-backed architectural remediation.</summary>
public enum ArchitectureRemediationHintCategory
{
    MoveCode,
    DependOnAbstraction,
    InvertDependency,
    IntroduceAdapter,
    UseDeclaredPort,
    FixClassification,
    FixPolicyInput,
    NarrowException,
    RemoveOrReplaceDependency,
    ReviewContract,
}

/// <summary>One deterministic fact that makes a remediation hint applicable.</summary>
public sealed record ArchitectureRemediationHintEvidence(string Kind, string Value);

/// <summary>
/// Optional, evidence-backed guidance attached to a normalized finding. It describes a bounded
/// remediation direction; it never represents an automatic edit, policy mutation, or approval.
/// </summary>
public sealed record ArchitectureRemediationHint(
    ArchitectureRemediationHintCategory Category,
    string Summary,
    string ContractIdentity,
    ArchitectureViolationIdentity FindingIdentity,
    IReadOnlyList<ArchitectureRemediationHintEvidence> Evidence)
{
    /// <summary>Policy-evidenced seam or architectural direction, when one is available.</summary>
    public string? ExpectedSeamOrDirection { get; init; }

    /// <summary>Bounded precondition or safety limitation associated with the hint.</summary>
    public string? Caveat { get; init; }

    /// <summary>Whether applying this direction requires explicit architecture review.</summary>
    public bool RequiresReview { get; init; }
}
