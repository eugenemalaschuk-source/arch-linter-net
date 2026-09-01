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
    ArchitecturePrReportChange Change);

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
    IReadOnlyList<ArchitecturePrReportFinding> Findings);

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

/// <summary>Headline and fully typed data exposed to a presentation adapter.</summary>
public sealed record ArchitecturePrReportProjection(
    ArchitecturePrReportHeadline Headline,
    ArchitecturePrReportEvidence? Evidence,
    ArchitecturePrReportChange Change,
    IReadOnlyList<ArchitecturePrReportNavigationReference> Navigation)
{
    public ArchitecturePrReportAvailability Availability => Headline.Availability;

    public bool IsAvailable => Availability == ArchitecturePrReportAvailability.Complete;
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
    IReadOnlyList<ArchitectureHealthDimension> Dimensions);

public sealed record ArchitecturePrReportNavigationReference(
    string Authority,
    string? Identity,
    string? Path);
