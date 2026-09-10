using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Requests a base-to-current architecture-policy weakening comparison.</summary>
public sealed record ArchitecturePolicyWeakeningRequest(
    ArchitecturePolicyContextExport BaseContext,
    ArchitecturePolicyContextExport CurrentContext,
    ArchitecturePolicyMembershipEvidence? BaseMembership = null,
    ArchitecturePolicyMembershipEvidence? CurrentMembership = null)
{
    /// <summary>Explicit, context-bound approvals for reviewed public API additions.</summary>
    public IReadOnlyList<ArchitecturePublicApiWeakeningApproval> PublicApiApprovals { get; init; } = [];

    /// <summary>Canonical current CLR public API captures bound to the current policy context.</summary>
    public IReadOnlyList<ArchitecturePublicApiLiveEvidence> PublicApiLiveEvidence { get; init; } = [];
}

/// <summary>One deterministic policy-weakening comparison result.</summary>
public sealed record ArchitecturePolicyWeakeningResult(
    int SchemaVersion,
    string Kind,
    string PolicyName,
    int PolicyVersion,
    string Severity,
    IReadOnlyList<ArchitecturePolicyWeakeningFinding> Findings)
{
    /// <summary>Current supported result-schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable result document kind.</summary>
    public const string ResultKind = "architecture-policy-weakening";

    /// <summary>Gets whether the configured severity makes the comparison fail.</summary>
    public bool HasErrors => Findings.Any(finding => finding.Severity == "error");

    /// <summary>Gets exact reviewed public API additions accepted by this comparison.</summary>
    public IReadOnlyList<ArchitectureApprovedPublicApiAddition> ApprovedPublicApiAdditions { get; init; } = [];
}

/// <summary>Explicit approval for exact additions to one reviewed public API contract.</summary>
public sealed record ArchitecturePublicApiWeakeningApproval(
    int SchemaVersion,
    string Kind,
    string BaseContextDigest,
    string CurrentContextDigest,
    string ContractId,
    IReadOnlyList<PublicApiSnapshotEntry> Added)
{
    /// <summary>Current supported approval schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable approval document kind.</summary>
    public const string ApprovalKind = "architecture-public-api-addition-approval";
}

/// <summary>Canonical current CLR public API capture used to prove an approval against live code.</summary>
public sealed record ArchitecturePublicApiLiveEvidence(
    int SchemaVersion,
    string Kind,
    string ContextDigest,
    string ContractId,
    IReadOnlyList<PublicApiSnapshotEntry> Entries)
{
    /// <summary>Current supported live-evidence schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable live-evidence document kind.</summary>
    public const string EvidenceKind = "architecture-public-api-live-evidence";
}

/// <summary>Auditable evidence for a public API addition accepted by policy weakening.</summary>
public sealed record ArchitectureApprovedPublicApiAddition(
    string ContractId,
    string BaseContextDigest,
    string CurrentContextDigest,
    string ComparisonMode,
    IReadOnlyList<PublicApiSnapshotEntry> Added);

/// <summary>Normalized evidence that one policy change weakens a governed control.</summary>
public sealed record ArchitecturePolicyWeakeningFinding(
    string Identity,
    string Kind,
    string ControlIdentity,
    string Classification,
    string Severity,
    IReadOnlyList<string> BaseValues,
    IReadOnlyList<string> CurrentValues,
    ArchitecturePolicyContextProvenance? BaseProvenance,
    ArchitecturePolicyContextProvenance? CurrentProvenance,
    IReadOnlyList<string> AffectedSubjects,
    string? Rationale);

/// <summary>Complete canonical evaluator membership bound to one policy-context artifact.</summary>
public sealed record ArchitecturePolicyMembershipEvidence(
    int SchemaVersion,
    string Kind,
    string ContextDigest,
    bool Complete,
    IReadOnlyList<ArchitecturePolicyContractMembership> Contracts)
{
    /// <summary>Current supported membership-evidence schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable membership-evidence document kind.</summary>
    public const string EvidenceKind = "architecture-policy-membership";
}

/// <summary>Canonical subjects matched by one effective policy control.</summary>
public sealed record ArchitecturePolicyContractMembership(
    string Family,
    string Id,
    IReadOnlyList<string> Subjects);
