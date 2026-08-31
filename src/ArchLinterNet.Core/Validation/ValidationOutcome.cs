using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed record ValidationOutcome
{
    private bool _nativePassed;
    private bool _passed;

    public ValidationOutcome(
        bool Passed,
        IReadOnlyCollection<ArchitectureViolation> Violations,
        IReadOnlyCollection<string> Cycles,
        IReadOnlyCollection<ArchitectureViolation> CoverageFindings,
        string CoverageConfig,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations,
        string UnmatchedIgnoredViolationsConfig,
        IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings,
        string PolicyConsistencyConfig,
        IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries,
        IReadOnlyCollection<ArchitectureClassificationConflict> ClassificationConflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures)
    {
        this.Passed = Passed;
        this.Violations = Violations;
        this.Cycles = Cycles;
        this.CoverageFindings = CoverageFindings;
        this.CoverageConfig = CoverageConfig;
        this.UnmatchedIgnoredViolations = UnmatchedIgnoredViolations;
        this.UnmatchedIgnoredViolationsConfig = UnmatchedIgnoredViolationsConfig;
        this.PolicyConsistencyFindings = PolicyConsistencyFindings;
        this.PolicyConsistencyConfig = PolicyConsistencyConfig;
        this.CoverageSummaries = CoverageSummaries;
        this.ClassificationConflicts = ClassificationConflicts;
        this.ClassificationMetadataFailures = ClassificationMetadataFailures;
    }

    /// <summary>Effective pass state after all attached Core governance projections.</summary>
    public bool Passed
    {
        get => _passed;
        init
        {
            _nativePassed = value;
            _passed = value && !ImportedDiagnostics.HasBlockingFindings;
        }
    }

    /// <summary>Immutable native conformance state before imported-diagnostic governance.</summary>
    public bool NativePassed => _nativePassed;

    private bool EffectivePassed
    {
        get => _passed;
        init => _passed = value;
    }

    public IReadOnlyCollection<ArchitectureViolation> Violations { get; init; }
    public IReadOnlyCollection<string> Cycles { get; init; }
    public IReadOnlyCollection<ArchitectureViolation> CoverageFindings { get; init; }
    public string CoverageConfig { get; init; }
    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; init; }
    public string UnmatchedIgnoredViolationsConfig { get; init; }
    public IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings { get; init; }
    public string PolicyConsistencyConfig { get; init; }
    public IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries { get; init; }
    public IReadOnlyCollection<ArchitectureClassificationConflict> ClassificationConflicts { get; init; }
    public IReadOnlyCollection<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures { get; init; }

    /// <summary>
    /// Preserves the public positional-record deconstruction shape that callers used before the
    /// outcome gained an explicit native pass-state. The value returned for <paramref name="Passed"/>
    /// is the current effective state, exactly as it was for the original positional record.
    /// </summary>
    public void Deconstruct(
        out bool Passed,
        out IReadOnlyCollection<ArchitectureViolation> Violations,
        out IReadOnlyCollection<string> Cycles,
        out IReadOnlyCollection<ArchitectureViolation> CoverageFindings,
        out string CoverageConfig,
        out IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations,
        out string UnmatchedIgnoredViolationsConfig,
        out IReadOnlyCollection<PolicyConsistencyDiagnostic> PolicyConsistencyFindings,
        out string PolicyConsistencyConfig,
        out IReadOnlyCollection<ArchitectureCoverageSummary> CoverageSummaries,
        out IReadOnlyCollection<ArchitectureClassificationConflict> ClassificationConflicts,
        out IReadOnlyCollection<ArchitectureClassificationMetadataFailure> ClassificationMetadataFailures)
    {
        Passed = this.Passed;
        Violations = this.Violations;
        Cycles = this.Cycles;
        CoverageFindings = this.CoverageFindings;
        CoverageConfig = this.CoverageConfig;
        UnmatchedIgnoredViolations = this.UnmatchedIgnoredViolations;
        UnmatchedIgnoredViolationsConfig = this.UnmatchedIgnoredViolationsConfig;
        PolicyConsistencyFindings = this.PolicyConsistencyFindings;
        PolicyConsistencyConfig = this.PolicyConsistencyConfig;
        CoverageSummaries = this.CoverageSummaries;
        ClassificationConflicts = this.ClassificationConflicts;
        ClassificationMetadataFailures = this.ClassificationMetadataFailures;
    }

    // Declared as an init-only property outside the primary constructor, not as a 13th positional
    // parameter, so existing positional `new ValidationOutcome(...)` call sites and Deconstruct
    // usages compiled against the prior (12-parameter) shape keep working unchanged; callers who
    // want discovered roles opt in via an object initializer.
    public IReadOnlyCollection<ArchitectureClassificationRoleFact> ClassificationRoles { get; init; } =
        Array.Empty<ArchitectureClassificationRoleFact>();

    // Non-null when the loaded policy declared a non-empty classification.path section — see
    // ArchitectureAnalysisSession.CheckClassificationPathDeferred.
    public ArchitectureClassificationPathDeferredNotice? ClassificationPathDeferred { get; init; }

    public IReadOnlyCollection<ArchitectureCycleFinding> CycleFindings { get; init; } =
        Array.Empty<ArchitectureCycleFinding>();

    // Non-empty only when build-state preflight ran (see ValidationRequest.PreparationMode).
    // When PreflightBlocked is true, no contract executed and every other collection above is
    // empty — Passed is false and the only findings are these preflight diagnostics.
    public IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics { get; init; } =
        Array.Empty<BuildStatePreflightDiagnostic>();

    public bool PreflightBlocked { get; init; }

    // Present for a successful explicit preparation. Snapshot orchestration can hand this
    // immutable, receipt-backed selection to later contributors so they materialize the exact
    // post-build artifacts rather than rediscovering policy-default output paths.
    public ArchitectureRunnerPreparation? PreparedPostBuildRunner { get; init; }

    // The authoritative repository root this analysis resolved (ArchitectureAnalysisSnapshot's own
    // RepositoryRoot, itself produced by IArchitectureRepositoryRootResolver) — never re-derived by
    // a host from Path.GetDirectoryName(policyPath), which disagrees with the real root whenever the
    // policy file lives under a conventional "architecture/" subfolder (see
    // ArchitectureRepositoryRootResolver.ResolveFrom). Threaded through so CLI/Testing cache
    // population and lookup use the same root the pipeline itself used, not their own ad-hoc guess.
    public string RepositoryRoot { get; init; } = string.Empty;

    // All source file paths in the policy import graph, including the root document.
    // Populated by ArchitectureAnalysisSnapshot after policy loading.
    public IReadOnlyList<string> PolicyImportPaths { get; init; } =
        Array.Empty<string>();

    // Every target assembly file path actually resolved for this analysis, independent of
    // whether build-state preflight ran for it. Preflight diagnostics are skipped entirely when
    // project discovery found no projects (see
    // ArchitectureValidationApplicationService.RunBuildStatePreflight), but
    // analysis.target_assemblies configured directly can still resolve assemblies in that case —
    // this is the complete inventory, PreflightDiagnostics is not.
    public IReadOnlyList<string> ResolvedAssemblyPaths { get; init; } =
        Array.Empty<string>();

    // Every project file (.csproj) discovered while building this analysis, independent of
    // whether that project's assembly ultimately resolved. Populated by ArchitectureAnalysisSnapshot
    // from the same project-discovery pass build-state preflight and assembly resolution both
    // consume — a genuinely consumed input, protected the same way policy imports and resolved
    // assemblies are.
    public IReadOnlyList<string> DiscoveredProjectPaths { get; init; } =
        Array.Empty<string>();

    // The loaded policy's resolved source-set expansion, so JSON and SARIF consumers can prove
    // which sources each authored contract expanded to. Empty for policies that declare no set.
    public Model.ArchitectureSourceExpansionInventory SourceExpansion { get; init; } =
        Model.ArchitectureSourceExpansionInventory.Empty;

    // Matched/stale evidence for exclude_types_matching/exclude_files_matching items, recorded as
    // this mode's type-placement/layout-convention contracts executed. Empty when the preflight
    // blocked execution (BuildBlockedOutcome) or no contract declared these matchers.
    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation { get; init; } =
        Array.Empty<ArchitectureSubtractiveMatcherParticipation>();

    // One record per current manual architecture waiver. Baseline-imported ignore entries retain
    // the independently governed baseline lifecycle and are intentionally absent here.
    public IReadOnlyList<ArchitectureWaiverLifecycleRecord> Waivers { get; init; } =
        Array.Empty<ArchitectureWaiverLifecycleRecord>();

    /// <summary>
    /// The canonical effective-policy control and explicit waiver-debt inventory produced for
    /// this validation. A null value is retained for compatibility outcomes reconstructed from
    /// cache entries written before the inventory was introduced.
    /// </summary>
    public ArchitecturePolicyInventory? PolicyInventory { get; init; }

    // Applicability is an additive, opt-in evidence boundary. Empty expected/produced
    // collections and null completion preserve behavior for policies that do not participate in
    // v0.8 applicability semantics; ordinary findings remain independent of this evidence.
    public IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpectedEntries { get; init; } =
        Array.Empty<ArchitectureApplicabilityExpectedEntry>();

    public IReadOnlyList<ArchitectureApplicabilityRecord> ApplicabilityRecords { get; init; } =
        Array.Empty<ArchitectureApplicabilityRecord>();

    public ArchitectureAssessmentCompletionEvidence? AssessmentCompletionEvidence { get; init; }

    /// <summary>
    /// The single Core-owned applicability projection consumed by output and adapter layers. It is
    /// null for policies without applicability opt-in, just like completion evidence.
    /// </summary>
    public ArchitectureApplicabilityProjection? ApplicabilityProjection { get; init; }

    /// <summary>
    /// The Core-owned projection of trusted imported diagnostics. Its findings and blocking state
    /// remain coupled so strict diagnostics always affect the effective validation outcome.
    /// </summary>
    public ImportedExternalDiagnosticProjection ImportedDiagnostics { get; private init; } =
        ImportedExternalDiagnosticProjection.Empty;

    /// <summary>Trusted imported-diagnostic findings in the normalized finding envelope.</summary>
    public IReadOnlyList<ArchitectureFinding> ImportedDiagnosticFindings => ImportedDiagnostics.Findings;

    /// <summary>
    /// Attaches Core-projected imported diagnostics and derives the effective pass state from their
    /// governance mode. Audit diagnostics remain reportable without making the result fail.
    /// </summary>
    public ValidationOutcome WithImportedDiagnostics(ImportedExternalDiagnosticProjection importedDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(importedDiagnostics);
        return this with
        {
            ImportedDiagnostics = importedDiagnostics,
            EffectivePassed = NativePassed && !importedDiagnostics.HasBlockingFindings,
        };
    }

    /// <summary>Normalized applicability insufficiency findings, when the projection is present.</summary>
    public IReadOnlyList<ArchitectureFinding> ApplicabilityFindings =>
        ApplicabilityProjection?.Findings
        ?? (AssessmentCompletionEvidence is null
            ? Array.Empty<ArchitectureFinding>()
            : ArchitectureApplicabilityProjector.ToFindings(AssessmentCompletionEvidence));
}
