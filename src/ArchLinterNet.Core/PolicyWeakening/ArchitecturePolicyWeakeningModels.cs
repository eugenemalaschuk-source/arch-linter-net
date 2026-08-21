using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

/// <summary>Requests a base-to-current architecture-policy weakening comparison.</summary>
public sealed record ArchitecturePolicyWeakeningRequest(
    ArchitecturePolicyContextExport BaseContext,
    ArchitecturePolicyContextExport CurrentContext,
    ArchitecturePolicyMembershipEvidence? BaseMembership = null,
    ArchitecturePolicyMembershipEvidence? CurrentMembership = null);

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
}

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
