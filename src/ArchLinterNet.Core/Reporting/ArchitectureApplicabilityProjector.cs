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

        // Completion.Reasons comes from the authoritative evaluator boundary.  Family-specific
        // machine-readable reason codes are intentionally open-ended, so every non-null reason
        // that reached this completed assessment projects through the normalized envelope.
        // Deduplication uses only canonical dimensions so distinct policy identities remain
        // distinct findings.
        IEnumerable<ArchitectureApplicabilityReason> reasons = completion.Reasons
            .Where(reason => reason is not null)
            .GroupBy(ReasonKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(reason => reason.Provenance.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.Family, StringComparer.Ordinal)
            .ThenBy(reason => reason.Provenance.PolicyIdentity, StringComparer.Ordinal)
            .ThenBy(reason => reason.Code, StringComparer.Ordinal);

        var assessments = new AssessmentLookup(completion.Controls);
        var findings = new List<ArchitectureFinding>();
        foreach (ArchitectureApplicabilityReason reason in reasons)
        {
            ArchitectureApplicabilityAssessment? assessment = assessments.Find(reason);
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

    private static string ReasonKey(ArchitectureApplicabilityReason reason) =>
        string.Join(
            '\u001f',
            reason.Code,
            reason.Provenance.Family,
            reason.Provenance.ControlIdentity,
            reason.Provenance.PolicyIdentity);

    // A reason normally identifies one control. Keep the evaluator's historic tie-breaking for
    // malformed hand-built completions (matching family first, then expected evidence) without
    // sorting the full control collection for every projected reason.
    private sealed class AssessmentLookup
    {
        private readonly Dictionary<string, ArchitectureApplicabilityAssessment> _byControlIdentity =
            new(StringComparer.Ordinal);
        private readonly Dictionary<AssessmentKey, ArchitectureApplicabilityAssessment> _byControlAndFamily = new();

        public AssessmentLookup(IReadOnlyList<ArchitectureApplicabilityAssessment> controls)
        {
            foreach (ArchitectureApplicabilityAssessment control in controls)
            {
                AddPreferred(_byControlIdentity, control.ControlIdentity, control);

                string? family = control.Expected?.Family ?? control.Record?.Family;
                if (!string.IsNullOrEmpty(family))
                {
                    AddPreferred(_byControlAndFamily, new AssessmentKey(control.ControlIdentity, family), control);
                }
            }
        }

        public ArchitectureApplicabilityAssessment? Find(ArchitectureApplicabilityReason reason)
        {
            var exactKey = new AssessmentKey(reason.Provenance.ControlIdentity, reason.Provenance.Family);
            return _byControlAndFamily.GetValueOrDefault(exactKey)
                ?? _byControlIdentity.GetValueOrDefault(reason.Provenance.ControlIdentity);
        }

        private static void AddPreferred<TKey>(
            Dictionary<TKey, ArchitectureApplicabilityAssessment> lookup,
            TKey key,
            ArchitectureApplicabilityAssessment candidate)
            where TKey : notnull
        {
            if (!lookup.TryGetValue(key, out ArchitectureApplicabilityAssessment? existing)
                || (existing.Expected is null && candidate.Expected is not null))
            {
                lookup[key] = candidate;
            }
        }
    }

    private readonly record struct AssessmentKey(string ControlIdentity, string Family);
}
