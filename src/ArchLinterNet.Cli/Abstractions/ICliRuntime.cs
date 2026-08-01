using ArchLinterNet.Core.BuildState;
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

    ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing);

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
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics);

    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics);

    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries) =>
        FormatResultAsSarif(mode, violations, cycles, cycleFindings, preflightDiagnostics);

    string FormatResultAsSarif( // NOSONAR: each parameter represents a semantically distinct section of the SARIF payload; grouping would obscure the data contract
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null) =>
        FormatResultAsSarif(mode, violations, cycles, cycleFindings, preflightDiagnostics, coverageSummaries);

    /// <summary>
    /// Cancellation-aware widest overload. Default interface implementation ignores the token and
    /// delegates to the overload above, so every existing test fake keeps compiling unaffected —
    /// only <see cref="ArchLinterNet.Cli.Infrastructure.CliRuntime"/> overrides it with a
    /// genuinely per-finding cancellation-aware implementation.
    /// <paramref name="subtractiveMatcherParticipation"/> has no default here (unlike the overload
    /// above) purely so this overload stays unambiguous by arity against it.
    /// </summary>
    string FormatResultAsSarif( // NOSONAR: each parameter represents a semantically distinct section of the SARIF payload; grouping would obscure the data contract
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via
        // ThrowIfCancellationRequested, not by forwarding it further.
        return FormatResultAsSarif( // NOSONAR: see comment above
            mode, violations, cycles, cycleFindings, preflightDiagnostics, coverageSummaries, sourceExpansion,
            subtractiveMatcherParticipation);
    }

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
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null) =>
        FormatResultForCiArtifacts(
            mode, passed, violations, cycles, cycleFindings, coverageFindings, unmatchedIgnoredViolations,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures,
            classificationRoles, classificationPathDeferred, preflightDiagnostics);

    /// <summary>
    /// Cancellation-aware widest overload. Declared with a default interface implementation that
    /// ignores the token and delegates to the overload above, so every existing test fake
    /// implementing this interface keeps compiling unaffected — only <see cref="ArchLinterNet.Cli.Infrastructure.CliRuntime"/>
    /// overrides it with a genuinely per-finding cancellation-aware implementation.
    /// <paramref name="subtractiveMatcherParticipation"/> has no default here (unlike the overload
    /// above) purely so this overload stays unambiguous by arity against it.
    /// </summary>
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
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via
        // ThrowIfCancellationRequested, not by forwarding it further.
        return FormatResultForCiArtifacts( // NOSONAR: see comment above
            mode, passed, violations, cycles, cycleFindings, coverageFindings, unmatchedIgnoredViolations,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures,
            classificationRoles, classificationPathDeferred, preflightDiagnostics, sourceExpansion,
            subtractiveMatcherParticipation);
    }

    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations);

    /// <summary>
    /// Cancellation-aware overload. Default interface implementation ignores the token and
    /// delegates to the overload above, so every existing test fake keeps compiling unaffected —
    /// only <see cref="ArchLinterNet.Cli.Infrastructure.CliRuntime"/> overrides it with a
    /// genuinely per-finding cancellation-aware implementation.
    /// </summary>
    string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // This method IS the cancellation-aware overload; the token is already observed via
        // ThrowIfCancellationRequested, not by forwarding it further.
        return FormatViolationsForHumans(violations); // NOSONAR: see comment above
    }

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

    string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics);

    BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request);

    BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request);

    BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request);

    BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request);

    BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request);

    BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request);

    PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request);

    PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request);

    PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request);

    PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request);

    ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request);

    string FormatGraphAsJson(ArchitectureDependencyGraph graph);

    string FormatGraphAsDot(ArchitectureDependencyGraph graph);

    string FormatGraphAsMermaid(ArchitectureDependencyGraph graph);

    ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request);
}
