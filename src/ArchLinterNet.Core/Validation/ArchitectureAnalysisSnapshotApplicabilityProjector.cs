using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// Applicability is a projection of one completed contract execution. Keeping this work in a
// stateless collaborator makes the snapshot's trust-boundary and baseline receipt entry points
// easy to reuse without introducing another runner or lifecycle owner.
internal static class ArchitectureAnalysisSnapshotApplicabilityProjector
{
    internal static ArchitectureAssessmentCompletionEvidence? DeriveAssessmentCompletion(
        ArchitectureContractExecutionResult execution,
        bool ordinaryPassed) =>
        ArchitectureApplicabilityEvaluator.Evaluate(
            execution.ApplicabilityExpectedEntries,
            execution.ApplicabilityRecords,
            ordinaryPassed);

    internal static bool HasPassedAssessment(
        bool ordinaryPassed,
        ArchitectureAssessmentCompletionEvidence? assessmentCompletion) =>
        ordinaryPassed
        && assessmentCompletion?.State is not (ArchitectureAssessmentCompletionState.Fail
            or ArchitectureAssessmentCompletionState.Unassessable);

    internal static ArchitectureApplicabilityProjection? ProjectApplicability(
        ArchitectureAssessmentCompletionEvidence? assessmentCompletion,
        string mode) => ArchitectureApplicabilityProjector.Project(assessmentCompletion, mode);

    internal static ArchitectureSnapshotBaselineCandidateReceipt CollectBaselineCandidates(
        ArchitectureContractDocument document,
        BuildStatePreflightResult preflight,
        string mode,
        IReadOnlyDictionary<string, ValidationOutcome> evaluatedModes,
        IArchitectureContractRunner runner)
    {
        if (mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        string[] requiredModes = mode == "all" ? ["strict", "audit"] : [mode];
        if (requiredModes.Any(requiredMode => !evaluatedModes.ContainsKey(requiredMode)))
        {
            throw new InvalidOperationException(
                "Baseline candidates can only be reused after the snapshot evaluated every requested mode.");
        }

        List<ArchitectureViolation> configurationViolations = mode switch
        {
            "strict" => runner.CheckConfiguration(strict: true),
            "audit" => runner.CheckConfiguration(strict: false),
            "all" => runner.CheckConfiguration(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        if (configurationViolations.Count > 0)
        {
            return new ArchitectureSnapshotBaselineCandidateReceipt(
                document,
                null,
                configurationViolations,
                Array.Empty<BuildStatePreflightDiagnostic>());
        }

        var candidates = runner.BaselineCandidates.ToList();
        foreach (string evaluatedMode in requiredModes)
        {
            ValidationOutcome outcome = evaluatedModes[evaluatedMode];
            candidates.AddRange(ArchitectureApplicabilityBaselineCandidateProjector.Project(
                document,
                evaluatedMode,
                outcome.ApplicabilityProjection));
        }

        return new ArchitectureSnapshotBaselineCandidateReceipt(
            document,
            candidates,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<BuildStatePreflightDiagnostic>());
    }
}
