using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// Projects one mode's execution into the public ValidationOutcome shape. The supplied runner and
// session are the snapshot's already-prepared facts; this collaborator never creates, caches, or
// disposes an analysis session.
internal static class ArchitectureAnalysisSnapshotEvaluationProjector
{
    internal static ValidationOutcome BuildBlockedOutcome(ArchitectureAnalysisSnapshotBlockedEvaluationInput input) =>
        new(
            false,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            input.CoverageConfig,
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            input.UnmatchedConfig,
            Array.Empty<PolicyConsistencyDiagnostic>(),
            input.PolicyConsistencyConfig,
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = input.RepositoryRoot,
            PreflightDiagnostics = input.PreflightDiagnostics,
            PreflightBlocked = true,
            PolicyImportPaths = input.PolicyImportPaths,
            ResolvedAssemblyPaths = input.ResolvedAssemblyPaths,
            DiscoveredProjectPaths = input.DiscoveredProjectPaths,
            ConsumedInputPaths = input.ConsumedInputPaths,
            SourceExpansion = input.Document.SourceExpansion,
            ExternalEvidenceRequirements = input.Document.ExternalEvidence,
        };

    internal static (ValidationOutcome Outcome, IReadOnlyDictionary<string, int> ContractFamilyResultCounts) Evaluate(
        ArchitectureAnalysisSnapshotEvaluationInput input)
    {
        IArchitectureContractRunner runner = input.Runner;
        List<ArchitectureViolation> allViolations = new();

        // ArchitectureAnalysisSession.UnmatchedIgnoredViolations is one mutable list that every
        // contract check across every mode appends to as it runs against the shared session. Slice
        // from the mode's starting count so each mode reports only its own additions.
        int unmatchedStartIndex = input.UnmatchedStartIndex;

        // SubtractiveMatcherParticipation is likewise shared across all modes on one snapshot.
        int subtractiveMatcherStartIndex = input.SubtractiveMatcherStartIndex;

        CancellationToken cancellationToken = runner.Session.Context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        using (input.Timing?.Measure("configuration_check"))
            allViolations.AddRange(runner.CheckConfiguration(strict: input.Mode == "strict"));

        cancellationToken.ThrowIfCancellationRequested();

        List<PolicyConsistencyDiagnostic> policyConsistencyFindings;
        using (input.Timing?.Measure("policy_consistency_check"))
        {
            policyConsistencyFindings = input.PolicyConsistencyConfig == "off"
                ? new List<PolicyConsistencyDiagnostic>()
                : runner.CheckPolicyConsistency();
        }

        cancellationToken.ThrowIfCancellationRequested();

        ArchitectureContractExecutionResult execution;
        using (input.Timing?.Measure("contract_checks"))
        {
            input.ProfilingCounters?.ResetContractFamilyResultCounts();
            execution = input.ContractExecutor.Execute(
                runner.Session,
                input.Mode,
                input.HandlerRegistry,
                input.IncludeAsmdefContracts,
                input.Timing);
        }

        allViolations.AddRange(execution.Violations);

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<ArchitectureViolation> coverageFindings = input.CoverageConfig == "off"
            ? Array.Empty<ArchitectureViolation>()
            : execution.CoverageViolations;

        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> rawUnmatched;
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched;
        using (input.Timing?.Measure("post_processing"))
        {
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> allUnmatched = runner.UnmatchedIgnoredViolations;
            rawUnmatched = unmatchedStartIndex >= allUnmatched.Count
                ? Array.Empty<ArchitectureUnmatchedIgnoredViolation>()
                : allUnmatched.Skip(unmatchedStartIndex).ToList();
            unmatched = ResolveUnmatchedIgnoredViolations(
                runner,
                input.EnforceUnmatchedIgnoredViolationsPolicy,
                input.UnmatchedConfig,
                unmatchedStartIndex);
        }

        unmatched = FilterUnmatchedForDisabledCoverage(unmatched, input.CoverageConfig);
        unmatched = unmatched.Select(input.Document.Provenance.Enrich).ToList();

        bool hasBlockingUnmatched = input.EnforceUnmatchedIgnoredViolationsPolicy
            && input.UnmatchedConfig == "error" && unmatched.Count > 0;

        bool hasBlockingPolicyConsistency =
            input.PolicyConsistencyConfig == "error" && policyConsistencyFindings.Count > 0;

        bool hasBlockingCoverage = input.CoverageConfig == "error" && coverageFindings.Count > 0;

        IReadOnlyList<ArchitectureWaiverLifecycleRecord> waivers = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            input.Document,
            input.Mode,
            rawUnmatched,
            input.WaiverEvaluationDate,
            input.RequestedContractIds);
        string waiverProfile = ArchitectureWaiverProfile.Resolve(input.Document);
        string[] blockingWaiverStates = waiverProfile == ArchitectureWaiverProfile.Strict
            ? ["expired", "invalid", "stale"]
            : ["invalid"];
        var waiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment(
            waiverProfile,
            waivers,
            blockingWaiverStates);
        bool hasBlockingWaiver = waiverLifecycleAssessment.HasBlockingRecords;

        bool ordinaryPassed = allViolations.Count == 0 && execution.Cycles.Count == 0
            && !hasBlockingUnmatched && !hasBlockingPolicyConsistency && !hasBlockingCoverage && !hasBlockingWaiver;

        ArchitectureAssessmentCompletionEvidence? assessmentCompletion =
            ArchitectureAnalysisSnapshotApplicabilityProjector.DeriveAssessmentCompletion(
                execution,
                ordinaryPassed);
        ArchitectureApplicabilityProjection? applicabilityProjection =
            ArchitectureAnalysisSnapshotApplicabilityProjector.ProjectApplicability(
                assessmentCompletion,
                input.Mode);
        bool passed = ArchitectureAnalysisSnapshotApplicabilityProjector.HasPassedAssessment(
            ordinaryPassed,
            assessmentCompletion);

        (IReadOnlyList<ArchitectureClassificationConflict> classificationConflicts,
            IReadOnlyList<ArchitectureClassificationMetadataFailure> classificationMetadataFailures) =
                runner.Session.CheckClassificationFacts();
        IReadOnlyList<ArchitectureClassificationRoleFact> classificationRoles = runner.Session.CheckClassificationRoles();
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred =
            runner.Session.CheckClassificationPathDeferred();

        ArchitecturePolicyInventory policyInventory = ArchitecturePolicyInventoryProjector.Project(
            input.Document,
            input.Mode,
            waivers,
            input.RequestedContractIds,
            input.IncludeAsmdefContracts,
            input.CoverageConfig != "off");

        // Classification post-processing can materialize additional facts. A signal observed
        // there must win over constructing and returning an apparently complete outcome.
        cancellationToken.ThrowIfCancellationRequested();

        ValidationOutcome outcome = new(
            passed,
            allViolations,
            execution.Cycles,
            coverageFindings,
            input.CoverageConfig,
            unmatched,
            input.UnmatchedConfig,
            policyConsistencyFindings,
            input.PolicyConsistencyConfig,
            execution.CoverageSummaries,
            classificationConflicts,
            classificationMetadataFailures)
        {
            RepositoryRoot = input.RepositoryRoot,
            CycleFindings = execution.CycleFindings,
            ClassificationRoles = classificationRoles,
            ClassificationPathDeferred = classificationPathDeferred,
            PreflightDiagnostics = input.PreflightDiagnostics,
            PolicyImportPaths = input.PolicyImportPaths,
            ResolvedAssemblyPaths = input.ResolvedAssemblyPaths,
            DiscoveredProjectPaths = input.DiscoveredProjectPaths,
            SourceExpansion = input.Document.SourceExpansion,
            Waivers = waivers,
            WaiverLifecycleAssessment = waiverLifecycleAssessment,
            PolicyInventory = policyInventory,
            ApplicabilityExpectedEntries = execution.ApplicabilityExpectedEntries,
            ApplicabilityRecords = execution.ApplicabilityRecords,
            AssessmentCompletionEvidence = assessmentCompletion,
            ApplicabilityProjection = applicabilityProjection,
            SubtractiveMatcherParticipation = runner.Session.SubtractiveMatcherParticipation
                .Skip(subtractiveMatcherStartIndex)
                .ToList(),
            ExternalEvidenceRequirements = input.Document.ExternalEvidence,
        };

        return (outcome, execution.ContractFamilyResultCounts);
    }

    private static IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> ResolveUnmatchedIgnoredViolations(
        IArchitectureContractRunner runner,
        bool enforceUnmatchedIgnoredViolationsPolicy,
        string unmatchedConfig,
        int unmatchedStartIndex)
    {
        if (!enforceUnmatchedIgnoredViolationsPolicy || unmatchedConfig == "off")
        {
            return Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
        }

        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> all = runner.UnmatchedIgnoredViolations;
        return unmatchedStartIndex >= all.Count
            ? Array.Empty<ArchitectureUnmatchedIgnoredViolation>()
            : all.Skip(unmatchedStartIndex).ToList();
    }

    // See ArchitectureValidationApplicationService.FilterUnmatchedForDisabledCoverage for why this
    // filters by contract group rather than by contract ID.
    private static IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> FilterUnmatchedForDisabledCoverage(
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> unmatched,
        string coverageConfig)
    {
        if (coverageConfig != "off" || unmatched.Count == 0)
        {
            return unmatched;
        }

        return unmatched
            .Where(u => u.ContractGroup is not ("strict_coverage" or "audit_coverage"))
            .ToList();
    }
}

internal sealed record ArchitectureAnalysisSnapshotEvaluationInput(
    ArchitectureContractDocument Document,
    string RepositoryRoot,
    IArchitectureContractRunner Runner,
    IArchitectureContractExecutor ContractExecutor,
    IArchitectureContractHandlerRegistry HandlerRegistry,
    string Mode,
    ValidationTiming? Timing,
    string UnmatchedConfig,
    string PolicyConsistencyConfig,
    string CoverageConfig,
    bool EnforceUnmatchedIgnoredViolationsPolicy,
    bool IncludeAsmdefContracts,
    IReadOnlyCollection<string>? RequestedContractIds,
    DateOnly WaiverEvaluationDate,
    AnalysisSessionProfilingCounters? ProfilingCounters,
    int UnmatchedStartIndex,
    int SubtractiveMatcherStartIndex,
    IReadOnlyList<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    IReadOnlyList<string> PolicyImportPaths,
    IReadOnlyList<string> ResolvedAssemblyPaths,
    IReadOnlyList<string> DiscoveredProjectPaths);

internal sealed record ArchitectureAnalysisSnapshotBlockedEvaluationInput(
    ArchitectureContractDocument Document,
    string RepositoryRoot,
    IReadOnlyList<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string CoverageConfig,
    string UnmatchedConfig,
    string PolicyConsistencyConfig,
    IReadOnlyList<string> PolicyImportPaths,
    IReadOnlyList<string> ResolvedAssemblyPaths,
    IReadOnlyList<string> DiscoveredProjectPaths,
    IReadOnlyList<string> ConsumedInputPaths);
