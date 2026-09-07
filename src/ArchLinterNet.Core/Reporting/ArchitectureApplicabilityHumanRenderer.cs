using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Renders Core-owned applicability evidence for human reports.</summary>
internal static class ArchitectureApplicabilityHumanRenderer
{
    internal static string RenderAssessmentCompletion(ArchitectureAssessmentCompletionEvidence? completion)
    {
        if (completion is null)
        {
            return string.Empty;
        }

        string reasons = completion.Reasons.Count == 0
            ? "none"
            : string.Join(
                "; ",
                completion.Reasons.Select(reason =>
                {
                    ArchitectureApplicabilityProvenance provenance = reason.Provenance;
                    string policy = string.IsNullOrEmpty(provenance.PolicyIdentity)
                        ? string.Empty
                        : $", policy={provenance.PolicyIdentity}";
                    return $"{reason.Code} (family={provenance.Family}, control={provenance.ControlIdentity}{policy})";
                }));

        return $"Assessment completion: {completion.State.ToString().ToLowerInvariant()}; reasons: {reasons}";
    }

    internal static string RenderProjection(ArchitectureApplicabilityProjection? projection)
    {
        if (projection is null)
        {
            return string.Empty;
        }

        ArchitectureApplicabilitySummary summary = projection.Summary;
        string summaryLine = "Assessment completeness transparency (not an architecture quality score): "
            + $"required={summary.RequiredCount}, required_evaluable={summary.RequiredEvaluableCount}, "
            + $"required_unassessable={summary.RequiredUnassessableCount}, evaluable={summary.EvaluableCount}, "
            + $"unassessable={summary.UnassessableCount}, optional={summary.OptionalCount}, "
            + $"not_applicable={summary.NotApplicableCount}";

        var controlLines = projection.Controls.Select(FormatControl);
        string controls = "Applicability controls:" + Environment.NewLine
            + string.Join(Environment.NewLine, controlLines);

        string findings = projection.Findings.Count == 0
            ? string.Empty
            : Environment.NewLine + "Applicability findings:" + Environment.NewLine
                + string.Join(
                    Environment.NewLine,
                    projection.Findings.Select(ArchitectureDiagnosticFormatter.FormatFindingForHumansInternal));

        return string.Join(
            Environment.NewLine,
            new[]
            {
                RenderAssessmentCompletion(projection.Completion),
                summaryLine,
                controls,
            }) + findings;
    }

    internal static string RenderDiagnostic(ArchitectureApplicabilityDiagnostic applicability)
    {
        string membership = applicability.Membership is { } membershipValue
            ? ArchitectureApplicabilityWireNames.MembershipToken(membershipValue)
            : "unknown";
        string state = applicability.State is { } stateValue
            ? ArchitectureApplicabilityWireNames.StateToken(stateValue)
            : "missing";
        string validatedState = applicability.ValidatedState is { } validatedStateValue
            ? ArchitectureApplicabilityWireNames.StateToken(validatedStateValue)
            : "untrusted";
        string policy = string.IsNullOrEmpty(applicability.PolicyIdentity)
            ? string.Empty
            : $", policy={applicability.PolicyIdentity}";
        return $"- [applicability] control={applicability.ControlIdentity}, family={applicability.Family}, "
            + $"membership={membership}, state={state}, validated_state={validatedState}, "
            + $"reason={applicability.ReasonCode}, provenance=(family={applicability.Provenance.Family}, "
            + $"control={applicability.Provenance.ControlIdentity}{policy})";
    }

    private static string FormatControl(ArchitectureApplicabilityAssessment control)
    {
        string family = control.Expected?.Family ?? control.Record?.Family ?? "unknown";
        string membership = control.Membership is { } membershipValue
            ? ArchitectureApplicabilityWireNames.MembershipToken(membershipValue)
            : "unknown";
        string state = control.State is { } stateValue
            ? ArchitectureApplicabilityWireNames.StateToken(stateValue)
            : "unassessable";
        string recordState = control.Record?.State is { } rawState
            ? ArchitectureApplicabilityWireNames.StateToken(rawState)
            : "missing";
        string integrityReasons = control.IntegrityReasons.Count == 0
            ? "none"
            : string.Join(
                "; ",
                control.IntegrityReasons.Select(FormatReason));
        string expectedProvenance = control.Expected is null
            ? "none"
            : FormatProvenance(control.Expected.Provenance);
        string recordProvenance = control.Record is null
            ? "none"
            : FormatProvenance(control.Record.Provenance);
        string topologyEvidence = control.Record?.TopologyEvidence is { } topology
            ? $", topology=(declared_components={topology.DeclaredComponentCount}, observed_subjects={topology.ObservedSubjectCount}, "
                + $"mapped_subjects={topology.MappedSubjectCount}, unmapped_subjects={topology.UnmappedSubjectCount}, "
                + $"ambiguous_subjects={topology.AmbiguousSubjectCount})"
            : string.Empty;

        return $"- control={control.ControlIdentity}, family={family}, membership={membership}, "
            + $"state={state}, record_state={recordState}, integrity_valid={control.IsIntegrityValid}, "
            + $"integrity_reasons={integrityReasons}, expected_provenance={expectedProvenance}, "
            + $"record_provenance={recordProvenance}{topologyEvidence}";
    }

    private static string FormatReason(ArchitectureApplicabilityReason reason) =>
        $"{reason.Code} ({FormatProvenance(reason.Provenance)})";

    private static string FormatProvenance(ArchitectureApplicabilityProvenance provenance)
    {
        string policy = string.IsNullOrEmpty(provenance.PolicyIdentity)
            ? string.Empty
            : $", policy={provenance.PolicyIdentity}";
        return $"family={provenance.Family}, control={provenance.ControlIdentity}{policy}";
    }
}
