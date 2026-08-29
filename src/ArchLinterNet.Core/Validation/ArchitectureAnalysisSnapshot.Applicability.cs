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
}
