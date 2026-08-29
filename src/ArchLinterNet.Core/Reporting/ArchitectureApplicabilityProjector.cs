using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>
/// Projects evaluator-owned applicability completion into reusable summaries and normalized
/// findings.  This boundary consumes only the completed join; it never reads policy or recounts
/// effective controls.
/// </summary>
public static class ArchitectureApplicabilityProjector
{
    /// <summary>
    /// Returns no projection for a policy that did not opt into applicability evidence.
    /// </summary>
    public static ArchitectureApplicabilityProjection? Project(
        ArchitectureAssessmentCompletionEvidence? completion,
        string? mode = null)
    {
        if (completion is null)
        {
            return null;
        }

        ArchitectureApplicabilitySummary summary = Summarize(completion);
        IReadOnlyList<ArchitectureFinding> findings = ToFindings(completion, mode);
        return new ArchitectureApplicabilityProjection(completion, summary, findings);
    }

    /// <summary>Derives counts from joined controls, preserving the explicit expected denominator.</summary>
    public static ArchitectureApplicabilitySummary Summarize(
        ArchitectureAssessmentCompletionEvidence completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        IReadOnlyList<ArchitectureApplicabilityAssessment> controls = completion.Controls;

        return new ArchitectureApplicabilitySummary(
            requiredCount: controls.Count(control =>
                control.Membership == ArchitectureApplicabilityMembership.Required),
            requiredEvaluableCount: controls.Count(control =>
                control.Membership == ArchitectureApplicabilityMembership.Required
                && control.State == ArchitectureApplicabilityRecordState.Evaluable),
            requiredUnassessableCount: controls.Count(control =>
                control.Membership == ArchitectureApplicabilityMembership.Required
                && IsUnassessable(control)),
            evaluableCount: controls.Count(control =>
                control.IsIntegrityValid
                && control.State == ArchitectureApplicabilityRecordState.Evaluable),
            unassessableCount: controls.Count(IsUnassessable),
            optionalCount: controls.Count(control =>
                control.Membership == ArchitectureApplicabilityMembership.Optional),
            notApplicableCount: controls.Count(control =>
                control.IsIntegrityValid
                && control.State == ArchitectureApplicabilityRecordState.NotApplicable));
    }

    /// <summary>
    /// Emits one finding per distinct valid insufficiency reason.  Healthy and deliberately
    /// not-applicable controls remain control-summary evidence rather than synthetic failures.
    /// </summary>
    public static IReadOnlyList<ArchitectureFinding> ToFindings(
        ArchitectureAssessmentCompletionEvidence completion,
        string? mode = null)
    {
        ArgumentNullException.ThrowIfNull(completion);

        // Completion.Reasons is already evaluator-ordered.  Deduplicate by all canonical reason
        // dimensions because duplicate collection entries must not create ambiguous baseline
        // candidates, while distinct policy identities remain distinct findings.
        IEnumerable<ArchitectureApplicabilityReason> reasons = completion.Reasons
            .Where(IsProjectableReason)
            .GroupBy(ReasonKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Code, StringComparer.Ordinal);

        var findings = new List<ArchitectureFinding>();
        foreach (ArchitectureApplicabilityReason reason in reasons)
        {
            ArchitectureApplicabilityAssessment? assessment = FindAssessment(completion.Controls, reason);
            ArchitectureApplicabilityDiagnostic diagnostic = new(
                controlIdentity: reason.Provenance.ControlIdentity,
                family: assessment?.Expected?.Family
                    ?? assessment?.Record?.Family
                    ?? reason.Provenance.Family,
                membership: assessment?.Membership,
                state: assessment?.Record?.State,
                validatedState: assessment?.State,
                reason: reason);
            findings.Add(ArchitectureFindingMapper.FromApplicabilityDiagnostic(diagnostic, mode));
        }

        return ArchitectureFindingMapper.Order(findings);
    }

    private static bool IsUnassessable(ArchitectureApplicabilityAssessment control) =>
        !control.IsIntegrityValid
        || control.State == ArchitectureApplicabilityRecordState.Unassessable;

    private static ArchitectureApplicabilityAssessment? FindAssessment(
        IReadOnlyList<ArchitectureApplicabilityAssessment> controls,
        ArchitectureApplicabilityReason reason)
    {
        // A reason's control identity is canonical.  Family is used as a tie-breaker for a
        // malformed hand-built completion where an orphan and expected row share an identity.
        return controls
            .Where(control => string.Equals(
                control.ControlIdentity,
                reason.Provenance.ControlIdentity,
                StringComparison.Ordinal))
            .OrderBy(control => string.Equals(
                control.Expected?.Family ?? control.Record?.Family,
                reason.Provenance.Family,
                StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(control => control.Expected is null ? 1 : 0)
            .FirstOrDefault();
    }

    private static bool IsProjectableReason(ArchitectureApplicabilityReason reason)
    {
        // These are the closed evaluator reason families.  Unknown strings in manually-created
        // completion objects are not trusted as normalized findings; the evaluator itself only
        // emits values from this set.
        return reason is not null && reason.Code is
            ArchitectureApplicabilityReasonCodes.MissingRequiredInput
            or ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput
            or ArchitectureApplicabilityReasonCodes.UnmappedSubject
            or ArchitectureApplicabilityReasonCodes.AmbiguousSubject
            or ArchitectureApplicabilityReasonCodes.StaleDeclaration
            or ArchitectureApplicabilityReasonCodes.MalformedExternalInput
            or ArchitectureApplicabilityReasonCodes.WrongExternalRepository
            or ArchitectureApplicabilityReasonCodes.WrongExternalRevision
            or ArchitectureApplicabilityReasonCodes.WrongExternalScope
            or ArchitectureApplicabilityReasonCodes.MissingApplicabilityRecord
            or ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityRecordIdentity
            or ArchitectureApplicabilityReasonCodes.UnknownApplicabilityRecordIdentity
            or ArchitectureApplicabilityReasonCodes.IncompatibleApplicabilityRecord
            or ArchitectureApplicabilityReasonCodes.InvalidApplicabilityExpectedIntegrity
            or ArchitectureApplicabilityReasonCodes.InvalidApplicabilityRecordIntegrity
            or ArchitectureApplicabilityReasonCodes.DuplicateApplicabilityExpectedIdentity;
    }

    private static string ReasonKey(ArchitectureApplicabilityReason reason) =>
        string.Join(
            '\u001f',
            reason.Code,
            reason.Provenance.Family,
            reason.Provenance.ControlIdentity,
            reason.Provenance.PolicyIdentity);
}
