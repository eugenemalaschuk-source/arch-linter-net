using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    // Applicability is a trust boundary orthogonal to ordinary conformance. Derive it from the
    // complete expected/produced collections so a required missing, duplicate, orphan, or
    // incompatible record cannot look like a clean empty result. Empty inputs deliberately return
    // null, preserving pre-v0.8 policies. An executor may provide already-derived completion only
    // for a transport-only result with no collections.
    private static ArchitectureAssessmentCompletionEvidence? DeriveAssessmentCompletion(
        ArchitectureContractExecutionResult execution,
        bool ordinaryPassed)
    {
        return ArchitectureApplicabilityEvaluator.Evaluate(
                   execution.ApplicabilityExpectedEntries,
                   execution.ApplicabilityRecords,
                   ordinaryPassed)
               ?? execution.AssessmentCompletionEvidence;
    }

    private static bool HasPassedAssessment(
        bool ordinaryPassed,
        ArchitectureAssessmentCompletionEvidence? assessmentCompletion)
    {
        return ordinaryPassed
            && assessmentCompletion?.State is not (ArchitectureAssessmentCompletionState.Fail
                or ArchitectureAssessmentCompletionState.Unassessable);
    }
}
