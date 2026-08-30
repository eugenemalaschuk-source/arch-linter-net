using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    /// <summary>
    /// Formats the complete applicability projection for the human validation report. The
    /// summary is deliberately labelled as completeness transparency: evaluability is evidence
    /// coverage, never an architecture-quality score.
    /// </summary>
    public static string FormatApplicabilityProjectionForHumans(
        ArchitectureApplicabilityProjection? projection)
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

        var controlLines = projection.Controls.Select(FormatApplicabilityControlForHumans);
        string controls = "Applicability controls:" + Environment.NewLine
            + string.Join(Environment.NewLine, controlLines);

        string findings = projection.Findings.Count == 0
            ? string.Empty
            : Environment.NewLine + "Applicability findings:" + Environment.NewLine
                + string.Join(Environment.NewLine, projection.Findings.Select(FormatFindingForHumans));

        return string.Join(
            Environment.NewLine,
            new[]
            {
                FormatAssessmentCompletionForHumans(projection.Completion),
                summaryLine,
                controls,
            }) + findings;
    }

    private static string FormatApplicabilityControlForHumans(ArchitectureApplicabilityAssessment control)
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
                control.IntegrityReasons.Select(FormatApplicabilityReasonForHumans));
        string expectedProvenance = control.Expected is null
            ? "none"
            : FormatApplicabilityProvenanceForHumans(control.Expected.Provenance);
        string recordProvenance = control.Record is null
            ? "none"
            : FormatApplicabilityProvenanceForHumans(control.Record.Provenance);
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

    private static string FormatApplicabilityReasonForHumans(ArchitectureApplicabilityReason reason)
    {
        return $"{reason.Code} ({FormatApplicabilityProvenanceForHumans(reason.Provenance)})";
    }

    private static string FormatApplicabilityProvenanceForHumans(ArchitectureApplicabilityProvenance provenance)
    {
        string policy = string.IsNullOrEmpty(provenance.PolicyIdentity)
            ? string.Empty
            : $", policy={provenance.PolicyIdentity}";
        return $"family={provenance.Family}, control={provenance.ControlIdentity}{policy}";
    }
}
