using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    // Applicability is a trust boundary orthogonal to ordinary conformance. Derive it from the
    // complete expected/produced collections so a required missing, duplicate, orphan, or
    // incompatible record cannot look like a clean empty result. Empty inputs deliberately return
    // null, preserving pre-v0.8 policies.
    private static ArchitectureAssessmentCompletionEvidence? DeriveAssessmentCompletion(
        ArchitectureContractExecutionResult execution,
        bool ordinaryPassed)
    {
        return ArchitectureApplicabilityEvaluator.Evaluate(
            execution.ApplicabilityExpectedEntries,
            execution.ApplicabilityRecords,
            ordinaryPassed);
    }

    private static bool HasPassedAssessment(
        bool ordinaryPassed,
        ArchitectureAssessmentCompletionEvidence? assessmentCompletion)
    {
        return ordinaryPassed
            && assessmentCompletion?.State is not (ArchitectureAssessmentCompletionState.Fail
                or ArchitectureAssessmentCompletionState.Unassessable);
    }

    private static ArchitectureApplicabilityProjection? ProjectApplicability(
        ArchitectureAssessmentCompletionEvidence? assessmentCompletion,
        string mode) =>
        ArchitectureApplicabilityProjector.Project(assessmentCompletion, mode);

    /// <summary>
    /// Returns the exact candidate set already collected by the evaluated snapshot. Health uses
    /// this internal receipt to compare a baseline without running a second analysis path.
    /// </summary>
    internal ArchitectureSnapshotBaselineCandidateReceipt CollectBaselineCandidates(string mode)
    {
        if (mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_preflight.Blocked)
            {
                return new ArchitectureSnapshotBaselineCandidateReceipt(
                    _document,
                    null,
                    Array.Empty<ArchitectureViolation>(),
                    _preflight.Diagnostics);
            }

            string[] requiredModes = mode == "all" ? ["strict", "audit"] : [mode];
            if (requiredModes.Any(requiredMode => !_evaluatedModes.ContainsKey(requiredMode)))
            {
                throw new InvalidOperationException(
                    "Baseline candidates can only be reused after the snapshot evaluated every requested mode.");
            }

            IArchitectureContractRunner runner = EnsureSetup().Runner;
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
                    _document,
                    null,
                    configurationViolations,
                    Array.Empty<BuildStatePreflightDiagnostic>());
            }

            var candidates = runner.BaselineCandidates.ToList();
            foreach (string evaluatedMode in requiredModes)
            {
                ValidationOutcome outcome = _evaluatedModes[evaluatedMode];
                candidates.AddRange(ArchitectureApplicabilityBaselineCandidateProjector.Project(
                    _document,
                    evaluatedMode,
                    outcome.ApplicabilityProjection));
            }

            return new ArchitectureSnapshotBaselineCandidateReceipt(
                _document,
                candidates,
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<BuildStatePreflightDiagnostic>());
        }
    }
}
