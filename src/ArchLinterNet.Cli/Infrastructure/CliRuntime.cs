using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Infrastructure;

internal sealed class CliRuntime : ICliRuntime
{
    private readonly ArchitectureDiagnosticFormatter _formatter = new();
    private readonly ArchitectureSarifFormatter _sarifFormatter = new();
    private readonly ArchitectureEngine _engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();

    public string Version { get; } = typeof(ArchitectureEngine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public bool TryParseGraphLevel(string value, out ArchitectureGraphLevel level)
    {
        switch (value)
        {
            case "namespace":
                level = ArchitectureGraphLevel.Namespace;
                return true;
            case "type":
                level = ArchitectureGraphLevel.Type;
                return true;
            case "assembly":
                level = ArchitectureGraphLevel.Assembly;
                return true;
            default:
                level = default;
                return false;
        }
    }

    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing)
    {
        return _engine.Validate(request, timing);
    }

    public (ValidationOutcome Outcome, ArchitectureAnalysisSnapshotCounters Counters) ValidateWithCounters(
        ValidationRequest request, ValidationTiming? timing)
    {
        return _engine.ValidateWithCounters(request, timing);
    }

    public ArchitectureAnalysisSnapshot CreateSnapshot(AnalysisSnapshotRequest request, ValidationTiming? timing)
    {
        return _engine.CreateSnapshot(request, timing);
    }

    public ArchitectureMetricMeasurementOutcome Measure(
        ArchitectureMetricMeasurementRequest request,
        ValidationTiming? timing)
    {
        return _engine.Measure(request, timing);
    }

    public string FormatResultForCiArtifacts(
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
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
    {
        return FormatResultForCiArtifacts(
            mode, passed, violations, cycles, cycleFindings, coverageFindings, unmatchedIgnoredViolations,
            policyConsistencyFindings, coverageSummaries, classificationConflicts, classificationMetadataFailures,
            classificationRoles, classificationPathDeferred, preflightDiagnostics,
            ArchitectureSourceExpansionInventory.Empty);
    }

    public string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
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
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null)
    {
        return ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            mode,
            passed,
            violations,
            cycles,
            cycleFindings,
            classificationRoles,
            classificationPathDeferred,
            preflightDiagnostics,
            sourceExpansion,
            coverageFindings,
            unmatchedIgnoredViolations,
            policyConsistencyFindings,
            coverageSummaries,
            classificationConflicts,
            classificationMetadataFailures,
            subtractiveMatcherParticipation);
    }

    public string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
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
        return ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            mode,
            passed,
            violations,
            cycles,
            cycleFindings,
            classificationRoles,
            classificationPathDeferred,
            preflightDiagnostics,
            sourceExpansion,
            coverageFindings,
            unmatchedIgnoredViolations,
            policyConsistencyFindings,
            coverageSummaries,
            classificationConflicts,
            classificationMetadataFailures,
            subtractiveMatcherParticipation,
            cancellationToken);
    }

    public string FormatBuildStatePreflightForHumans(IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics)
    {
        return _formatter.FormatBuildStatePreflightForHumans(diagnostics);
    }

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
    {
        return FormatResultAsSarif(
            mode,
            violations,
            cycles,
            cycleFindings,
            preflightDiagnostics,
            Array.Empty<ArchitectureCoverageSummary>());
    }

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries)
    {
        return FormatResultAsSarif(
            mode,
            violations,
            cycles,
            cycleFindings,
            preflightDiagnostics,
            coverageSummaries,
            ArchitectureSourceExpansionInventory.Empty);
    }

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null)
    {
        return cycleFindings.Count > 0
            ? ArchitectureSarifFormatter.FormatResultAsSarif(
                mode, violations, cycleFindings, preflightDiagnostics, coverageSummaries, sourceExpansion, Version,
                subtractiveMatcherParticipation)
            : _sarifFormatter.FormatResultAsSarif(
                mode, violations, cycles, preflightDiagnostics, coverageSummaries, sourceExpansion, Version,
                subtractiveMatcherParticipation);
    }

    public string FormatResultAsSarif(
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
        return cycleFindings.Count > 0
            ? ArchitectureSarifFormatter.FormatResultAsSarif(
                mode, violations, cycleFindings, preflightDiagnostics, coverageSummaries, sourceExpansion, Version,
                subtractiveMatcherParticipation, cancellationToken)
            : _sarifFormatter.FormatResultAsSarif(
                mode, violations, cycles, preflightDiagnostics, coverageSummaries, sourceExpansion, Version,
                subtractiveMatcherParticipation, cancellationToken);
    }

    public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return _formatter.FormatViolationsForHumans(violations);
    }

    public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations, CancellationToken cancellationToken)
    {
        return _formatter.FormatViolationsForHumans(violations, cancellationToken);
    }

    public string FormatCyclesForHumans(
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings)
    {
        return cycleFindings.Count > 0
            ? ArchitectureDiagnosticFormatter.FormatCyclesForHumans(cycleFindings)
            : _formatter.FormatCyclesForHumans(cycles);
    }

    public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> diagnostics)
    {
        return _formatter.FormatPolicyConsistencyForHumans(diagnostics);
    }

    public string FormatUnmatchedForHumans(IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatchedViolations)
    {
        return _formatter.FormatUnmatchedForHumans(unmatchedViolations);
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings)
    {
        return _formatter.FormatCoverageForHumans(coverageFindings);
    }

    public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> coverageFindings, CancellationToken cancellationToken)
    {
        return _formatter.FormatCoverageForHumans(coverageFindings, cancellationToken);
    }

    public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> coverageSummaries)
    {
        return _formatter.FormatCoverageSummaryForHumans(coverageSummaries);
    }

    public string FormatClassificationFactsForHumans(
        IReadOnlyCollection<ArchitectureClassificationConflict> conflicts,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure> metadataFailures,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred)
    {
        return _formatter.FormatClassificationFactsForHumans(conflicts, metadataFailures, classificationPathDeferred);
    }

    public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request)
    {
        return _engine.GenerateBaseline(request);
    }

    public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request)
    {
        return _engine.UpdateBaseline(request);
    }

    public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request)
    {
        return _engine.PruneBaseline(request);
    }

    public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
    {
        return _engine.DiffBaseline(request);
    }

    public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request)
    {
        return _engine.VerifyBaseline(request);
    }

    public BaselineMigrateOutcome MigrateBaseline(BaselineMigrateRequest request)
    {
        return _engine.MigrateBaseline(request);
    }

    public ArchitectureDebtGateOutcome EvaluateDebtGate(ArchitectureDebtGateRequest request)
    {
        return _engine.EvaluateDebtGate(request);
    }

    public ArchitectureHealthOutcome EvaluateHealth(ArchitectureHealthRequest request)
    {
        return _engine.EvaluateHealth(request);
    }

    public string FormatHealthAsHuman(ArchitectureHealthOutcome outcome)
    {
        return ArchitectureHealthProjector.FormatAsHuman(outcome.Summary);
    }

    public string FormatHealthAsJson(ArchitectureHealthOutcome outcome)
    {
        return ArchitectureHealthProjector.FormatAsJson(outcome);
    }

    public PublicApiCaptureOutcome CapturePublicApi(PublicApiCaptureRequest request)
    {
        return _engine.CapturePublicApi(request);
    }

    public PublicApiDiffOutcome DiffPublicApi(PublicApiDiffRequest request)
    {
        return _engine.DiffPublicApi(request);
    }

    public PublicApiUpdateOutcome UpdatePublicApi(PublicApiUpdateRequest request)
    {
        return _engine.UpdatePublicApi(request);
    }

    public PublicApiMigrateOutcome MigratePublicApi(PublicApiMigrateRequest request)
    {
        return _engine.MigratePublicApi(request);
    }

    public ArchitecturePolicyContextExport ExportPolicyContext(ArchitecturePolicyContextRequest request)
    {
        return _engine.ExportPolicyContext(request);
    }

    public string FormatPolicyContextAsJson(ArchitecturePolicyContextExport context)
    {
        return ArchitecturePolicyContextFormatter.FormatAsJson(context);
    }

    public string FormatPolicyContextAsMarkdown(ArchitecturePolicyContextExport context)
    {
        return ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);
    }

    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        return _engine.BuildGraph(request);
    }

    public ArchitectureTopologyCaptureOutcome CaptureTopology(ArchitectureTopologyCaptureRequest request)
    {
        return _engine.CaptureTopology(request);
    }

    public string FormatGraphAsJson(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsJson(graph);
    }

    public string FormatGraphAsDot(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsDot(graph);
    }

    public string FormatGraphAsMermaid(ArchitectureDependencyGraph graph)
    {
        return _engine.GraphFormatter.FormatAsMermaid(graph);
    }

    public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request)
    {
        return _engine.Explain(request);
    }
}
