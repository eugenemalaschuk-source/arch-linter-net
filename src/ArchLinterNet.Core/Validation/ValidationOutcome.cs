using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed record ValidationOutcome(
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

    // Captured by ArchitectureAnalysisSnapshot before contract execution and intentionally not
    // part of the public result/output model. Hosts hand the completed outcome back to
    // AnalysisCachePopulation, which uses this opaque state to revalidate pre-analysis inputs
    // immediately before publication instead of constructing authorization from post-run files.
    internal AnalysisCachePopulation.PreparedAuthorization? CachePopulationAuthorization { get; init; }
}
