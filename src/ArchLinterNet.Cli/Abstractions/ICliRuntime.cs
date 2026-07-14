using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Abstractions;

internal interface ICliRuntime
{
    string Version { get; }

    bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level);

    ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing);

    string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<ArchitectureViolation> coverageFindings,
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedIgnoredViolations,
        IReadOnlyCollection<PolicyConsistencyDiagnostic> policyConsistencyFindings,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        IReadOnlyCollection<ArchitectureClassificationConflict> classificationConflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> classificationMetadataFailures,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred);

    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings);

    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    string FormatCyclesForHumans(
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings);

    string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics);

    string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations);

    string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings);

    string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries);

    string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred);

    BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request);

    BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request);

    BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request);

    BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request);

    BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request);

    ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request);

    string FormatGraphAsJson(ArchitectureDependencyGraph graph);

    string FormatGraphAsDot(ArchitectureDependencyGraph graph);

    string FormatGraphAsMermaid(ArchitectureDependencyGraph graph);

    ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request);
}
