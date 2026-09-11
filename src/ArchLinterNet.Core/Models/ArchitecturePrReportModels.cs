using System.Text.Json;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Availability of the canonical evidence needed to render an architecture PR report.</summary>
public enum ArchitecturePrReportAvailability
{
    Complete,
    Unavailable,
    Unassessable,
}

/// <summary>Typed input assembled from one Health artifact and one change-report artifact.</summary>
public sealed record ArchitecturePrReportInput(
    ArchitectureHealthSummary Summary,
    ArchitecturePrReportEvidence? Evidence,
    ArchitecturePrReportChange Change)
{
    public ArchitecturePrReportNavigationContext? NavigationContext { get; init; }
}

/// <summary>Correlation metadata carried by report artifacts from one workflow execution.</summary>
public sealed record ArchitecturePrReportExecutionContext(
    string ExecutionId,
    string ConditionSetName);

/// <summary>Versioned report evidence exported by the Health formatter.</summary>
public sealed record ArchitecturePrReportEvidence(
    int SchemaVersion,
    string Kind,
    ArchitectureHealthGate Gate,
    ArchitectureHealthState Health,
    IReadOnlyList<ArchitecturePrReportValidationReceipt> ValidationOutcomes,
    ArchitecturePrReportDebtGateReceipt DebtGate)
{
    public const int CurrentSchemaVersion = 2;
    public const string EvidenceKind = "architecture-health-report-evidence";

    /// <summary>Workflow context required before the evidence can be paired with change data.</summary>
    public ArchitecturePrReportExecutionContext? ExecutionContext { get; init; }
}

/// <summary>One already-evaluated mode receipt in a Health report-evidence envelope.</summary>
public sealed record ArchitecturePrReportValidationReceipt(
    string Mode,
    IReadOnlyDictionary<string, string> Availability,
    ArchitecturePolicyInventory? PolicyInventory,
    ArchitectureWaiverLifecycleAssessment? WaiverLifecycle,
    ArchitecturePrReportApplicability? Applicability,
    ArchitecturePrReportExternalEvidence? ExternalEvidence,
    IReadOnlyList<ArchitecturePrReportFinding> Findings,
    ArchitecturePrReportProvenance Provenance);

/// <summary>Applicability completion and control-level evidence retained for reporting.</summary>
public sealed record ArchitecturePrReportApplicability(
    string State,
    ArchitecturePrReportApplicabilitySummary Summary,
    IReadOnlyList<ArchitecturePrReportApplicabilityReason> Reasons,
    IReadOnlyList<ArchitecturePrReportApplicabilityControl> Controls);

public sealed record ArchitecturePrReportApplicabilitySummary(
    int Required,
    int RequiredEvaluable,
    int RequiredUnassessable);

public sealed record ArchitecturePrReportApplicabilityReason(
    string Code,
    ArchitecturePrReportProvenanceReference Provenance);

public sealed record ArchitecturePrReportApplicabilityControl(
    string ControlIdentity,
    string? Membership,
    string State,
    bool IntegrityValid,
    IReadOnlyList<ArchitecturePrReportApplicabilityReason> IntegrityReasons,
    ArchitecturePrReportApplicabilityExpected? Expected,
    ArchitecturePrReportApplicabilityRecord? Record);

public sealed record ArchitecturePrReportApplicabilityExpected(
    string ControlIdentity,
    string Family,
    string Membership,
    ArchitecturePrReportProvenanceReference Provenance);

public sealed record ArchitecturePrReportApplicabilityRecord(
    string ControlIdentity,
    string Family,
    string State,
    IReadOnlyList<ArchitecturePrReportApplicabilityReason> Reasons,
    ArchitecturePrReportProvenanceReference Provenance,
    ArchitecturePrReportTopology? Topology,
    ArchitecturePrReportMetric? Metric);

/// <summary>Declared-topology mapping evidence attached to an applicability record.</summary>
public sealed record ArchitecturePrReportTopology(
    string Mode,
    string SubjectKind,
    int DeclaredComponentCount,
    ArchitecturePrReportTopologyCounts Counts,
    IReadOnlyList<ArchitecturePrReportTopologySubject> Subjects,
    IReadOnlyList<ArchitecturePrReportTopologyRelation> Relationships,
    IReadOnlyList<string> StaleNodes,
    IReadOnlyList<ArchitecturePrReportTopologyEdge> StaleEdges);

public sealed record ArchitecturePrReportTopologyCounts(
    int Observed,
    int Mapped,
    int ReviewedOutOfScope,
    int Unmapped,
    int Ambiguous);

public sealed record ArchitecturePrReportTopologySubject(
    string Identity,
    string Project,
    string Assembly,
    string Subject,
    string Disposition,
    IReadOnlyList<string> NodeIds,
    string? ReviewedOutOfScopeId);

public sealed record ArchitecturePrReportTopologyRelation(
    string SourceNode,
    string TargetNode,
    string Witness,
    bool IsAllowed);

public sealed record ArchitecturePrReportTopologyEdge(string SourceNode, string TargetNode);

public sealed record ArchitecturePrReportMetric(
    string MetricId,
    string Kind,
    string? NativeSubject,
    string? Unit,
    string EffectiveScope,
    int? Value,
    IReadOnlyList<string>? Contributors);

/// <summary>External-evidence requirements and selected normalized findings.</summary>
public sealed record ArchitecturePrReportExternalEvidence(
    string Mode,
    IReadOnlyList<ArchitecturePrReportExternalRequirement> Requirements,
    IReadOnlyList<ArchitecturePrReportFinding> Findings)
{
    /// <summary>
    /// Canonical trust results for the declared logical evidence requirements. An empty collection
    /// on a non-empty requirement set denotes a legacy/incomplete report-evidence envelope and is
    /// deliberately unavailable rather than implicitly trusted.
    /// </summary>
    public IReadOnlyList<ArchitecturePrReportExternalEvidenceTrustReceipt> TrustReceipts { get; init; } =
        Array.Empty<ArchitecturePrReportExternalEvidenceTrustReceipt>();

    /// <summary>Whether the receipt set covers every declared logical evidence requirement exactly once.</summary>
    public bool HasCompleteTrustReceipts => TrustReceipts.Count == Requirements.Count
        && Requirements.All(requirement => TrustReceipts.Any(receipt =>
            string.Equals(receipt.LogicalId, requirement.Id, StringComparison.Ordinal)));
}

public sealed record ArchitecturePrReportExternalRequirement(
    string Id,
    string Format,
    bool Required,
    string Tool,
    string? ToolVersion,
    string Run,
    bool RequireRepository,
    bool RequireRevision,
    bool RequireScope,
    ArchitecturePrReportDiagnosticFilter? DiagnosticFilter);

/// <summary>Report-owned summary state for one canonical external-evidence trust decision.</summary>
public enum ArchitecturePrReportExternalEvidenceTrustState
{
    Current,
    Stale,
    WrongContext,
    Missing,
    Invalid,
    NotConfigured,
}

/// <summary>
/// Canonical trust and provenance retained for one externally produced evidence artifact.
/// </summary>
public sealed record ArchitecturePrReportExternalEvidenceTrustReceipt(
    string LogicalId,
    ArchitecturePrReportExternalEvidenceTrustState State,
    SarifEvidenceTrustStatus Status,
    string ReasonCode,
    string? ArtifactPath,
    string? ArtifactSha256,
    string? RunId,
    int? ResultCount,
    ArchitecturePrReportExternalEvidenceContext? Context);

/// <summary>Resolved producer context retained by one external-evidence trust receipt.</summary>
public sealed record ArchitecturePrReportExternalEvidenceContext(
    string? Repository,
    string? Revision,
    string? Scope);

public sealed record ArchitecturePrReportDiagnosticFilter(
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> RuleTags,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> PathPrefixes,
    IReadOnlyDictionary<string, string> Severity,
    bool RequireMatches);

/// <summary>Normalized finding facts and supplied remediation guidance.</summary>
public sealed record ArchitecturePrReportFinding(
    int SchemaVersion,
    string Kind,
    string CanonicalIdentity,
    string? Mode,
    string? Severity,
    string MessageCode,
    string ContractName,
    string? ContractId,
    string? PolicyIdentity,
    ArchitecturePrReportSourceLocation? SourceLocation,
    ArchitecturePrReportRemediation? Remediation,
    JsonElement Details);

public sealed record ArchitecturePrReportSourceLocation(string Path, int? Line, int? Column);

public sealed record ArchitecturePrReportRemediation(
    string Category,
    string Summary,
    string ContractIdentity,
    string FindingIdentity,
    IReadOnlyList<ArchitecturePrReportEvidenceFact> Evidence,
    string? ExpectedSeamOrDirection,
    string? Caveat,
    bool RequiresReview);

public sealed record ArchitecturePrReportEvidenceFact(string Kind, string Value);

/// <summary>Portable source identities carried by each validation receipt.</summary>
public sealed record ArchitecturePrReportProvenance(
    string RepositoryRoot,
    IReadOnlyList<string> PolicyImportPaths,
    IReadOnlyList<string> ResolvedAssemblyPaths,
    IReadOnlyList<string> DiscoveredProjectPaths);

public sealed record ArchitecturePrReportProvenanceReference(
    string? Family,
    string? ControlIdentity,
    string? PolicyIdentity,
    string? EvidenceIdentity);

/// <summary>Already-evaluated baseline/debt-gate evidence retained by Health.</summary>
public sealed record ArchitecturePrReportDebtGateReceipt(
    bool Succeeded,
    bool Passed,
    ArchitecturePrReportDebtEvaluation Evaluation,
    ArchitecturePrReportPersistentDebt PersistentDebt,
    ArchitecturePrReportPolicyWeakening? PolicyWeakening);

public sealed record ArchitecturePrReportDebtEvaluation(
    bool Completed,
    string Mode,
    bool ReusedAnalysisSnapshot,
    IReadOnlyList<ArchitecturePrReportFinding> PreflightDiagnostics);

public sealed record ArchitecturePrReportPersistentDebt(
    bool Succeeded,
    bool InSync,
    IReadOnlyList<ArchitecturePrReportBaselineEntry> Entries,
    IReadOnlyList<ArchitecturePrReportFinding> ConfigurationViolations);

public sealed record ArchitecturePrReportBaselineEntry(
    string Status,
    string Disposition,
    string ContractGroup,
    string ContractId,
    string SourceType,
    string ForbiddenReference,
    string? Reason,
    string? Issue,
    string? CurrentForbiddenReference,
    string? Identity);

public sealed record ArchitecturePrReportPolicyWeakening(
    int SchemaVersion,
    string Kind,
    string PolicyName,
    int PolicyVersion,
    string Severity,
    bool HasBlockingFindings,
    IReadOnlyList<ArchitecturePrReportPolicyWeakeningFinding> Findings);

public sealed record ArchitecturePrReportPolicyWeakeningFinding(
    string Identity,
    string Kind,
    string ControlIdentity,
    string Classification,
    string Severity,
    IReadOnlyList<string> BaseValues,
    IReadOnlyList<string> CurrentValues,
    IReadOnlyList<string> AffectedSubjects,
    ArchitecturePrReportPolicyContextProvenance? BaseProvenance,
    ArchitecturePrReportPolicyContextProvenance? CurrentProvenance,
    string? Rationale);

public sealed record ArchitecturePrReportPolicyContextProvenance(
    string SourcePath,
    string RootPath,
    string Role,
    string YamlPath,
    int SourceOrder);

/// <summary>Canonical Health dimension reason classified for PR-report presentation.</summary>
/// <remarks>Source reason and identities are preserved; only fail and unassessable are blocking.</remarks>
public sealed record ArchitecturePrReportDimensionExplanation(
    string Dimension,
    ArchitectureHealthDimensionState State,
    ArchitectureHealthReason Reason,
    bool IsBlocking)
{
    public string Code => Reason.Code;
    public string Source => Reason.Source;
    public string? Family => Reason.Family;
    public string? ControlIdentity => Reason.ControlIdentity;
    public string? PolicyIdentity => Reason.PolicyIdentity;

    public string? EvidenceIdentity => Reason.EvidenceIdentity;
}

public sealed record ArchitecturePrReportNavigationContext(
    string RepositoryUrl,
    string HeadSha,
    string? ArtifactUrl)
{
    public bool IsUsable => TryNormalize(this, out _);

    public string? GetSourceUrl(string? relativePath)
    {
        if (!TryNormalize(this, out ArchitecturePrReportNavigationContext? normalized)
            || !TryNormalizeRelativePath(relativePath, out string? path))
        {
            return null;
        }

        return $"{normalized!.RepositoryUrl}/blob/{normalized.HeadSha}/{path}";
    }

    internal static bool TryNormalize(
        ArchitecturePrReportNavigationContext? context,
        out ArchitecturePrReportNavigationContext? normalized)
    {
        normalized = null;
        if (context is null || !Uri.TryCreate(context.RepositoryUrl, UriKind.Absolute, out Uri? repository)
            || !TryRepository(repository, out string? repositoryUrl, out string[]? repositoryParts)
            || !IsCommitSha(context.HeadSha))
        {
            return false;
        }

        string? artifactUrl = context.ArtifactUrl is null
            ? null
            : NormalizeActionUrl(context.ArtifactUrl, repositoryParts!);
        if (context.ArtifactUrl is not null && artifactUrl is null)
        {
            return false;
        }

        normalized = new ArchitecturePrReportNavigationContext(repositoryUrl!, context.HeadSha.Trim(), artifactUrl);
        return true;
    }

    private static bool TryRepository(Uri uri, out string? normalized, out string[]? parts)
    {
        normalized = null;
        parts = null;
        if (!IsAllowedHttps(uri) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || segments.Any(segment => segment is "." or ".." || string.IsNullOrWhiteSpace(segment)))
        {
            return false;
        }

        string name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        parts = [segments[0], name];
        normalized = $"https://github.com/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(name)}";
        return true;
    }

    private static string? NormalizeActionUrl(string value, string[] repository)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !IsAllowedHttps(uri) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        bool prefix = segments.Length is 5 or 7
            && string.Equals(segments[0], repository[0], StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], repository[1], StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "actions", StringComparison.Ordinal)
            && string.Equals(segments[3], "runs", StringComparison.Ordinal)
            && IsDigits(segments[4]);
        bool artifact = segments.Length == 7
            && string.Equals(segments[5], "artifacts", StringComparison.Ordinal)
            && IsDigits(segments[6]);
        return prefix && (segments.Length == 5 || artifact)
            ? $"https://github.com/{repository[0]}/{repository[1]}/{string.Join('/', segments[2..])}" : null;
    }

    private static bool IsAllowedHttps(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && string.Equals(uri.UserInfo, string.Empty, StringComparison.Ordinal)
        && uri.Port is -1 or 443;

    private static bool IsCommitSha(string? value) =>
        value is not null
        && value.Trim().Length == 40
        && value.Trim().All(Uri.IsHexDigit);

    private static bool IsDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static bool TryNormalizeRelativePath(string? relativePath, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string path = relativePath.Trim().Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':'))
        {
            return false;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        normalized = string.Join('/', segments.Select(Uri.EscapeDataString));
        return true;
    }
}

/// <summary>Headline and fully typed data exposed to a presentation adapter.</summary>
public sealed record ArchitecturePrReportProjection(
    ArchitecturePrReportHeadline Headline,
    ArchitecturePrReportEvidence? Evidence,
    ArchitecturePrReportChange Change,
    IReadOnlyList<ArchitecturePrReportNavigationReference> Navigation)
{
    public ArchitecturePrReportAvailability Availability => Headline.Availability;

    public bool IsAvailable => Availability == ArchitecturePrReportAvailability.Complete;

    public ArchitecturePrReportNavigationContext? NavigationContext { get; init; }
}

/// <summary>Report-owned view of the compatible canonical change artifact.</summary>
public sealed record ArchitecturePrReportChange(
    ArchitecturePrReportExecutionContext ExecutionContext,
    string Mode,
    IReadOnlyList<ArchitectureChangeEntry> Added,
    IReadOnlyList<ArchitectureChangeEntry> Removed,
    IReadOnlyList<ArchitectureChangeFinding> NewFindings,
    IReadOnlyList<ArchitectureChangeFinding> ExistingFindings,
    IReadOnlyList<ArchitectureChangeFinding> ResolvedFindings,
    IReadOnlyList<string> BaselineDebt);

public sealed record ArchitecturePrReportHeadline(
    ArchitectureHealthGate Gate,
    ArchitectureHealthState Health,
    ArchitecturePrReportAvailability Availability,
    IReadOnlyList<ArchitectureHealthDimension> Dimensions)
{
    public IReadOnlyList<ArchitecturePrReportDimensionExplanation> DimensionExplanations { get; init; } =
        Array.Empty<ArchitecturePrReportDimensionExplanation>();
}

public sealed record ArchitecturePrReportNavigationReference(
    string Authority,
    string? Identity,
    string? Path);
