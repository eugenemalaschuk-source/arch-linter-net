using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal sealed record TopologyDiffReport(
    string Mode,
    ArchitectureApplicabilityRecord Applicability,
    ArchitectureApplicabilityMembership? Membership,
    ArchitectureTopologyMappingEvidence Evidence,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> Structural,
    IReadOnlyList<ArchitectureTopologyRelationEvidence> Relational,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> Unmapped,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> ReviewedOutOfScope)
{
    internal bool IsNonReviewableUnassessability =>
        Applicability.State == ArchitectureApplicabilityRecordState.Unassessable
        && Applicability.Reasons.Any(reason => string.Equals(
            reason.Code,
            ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput,
            StringComparison.Ordinal));
}

/// <summary>Renders a projection of native topology evidence for human review.</summary>
internal static class TopologyDiffRenderer
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    internal static string FormatJson(TopologyDiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArchitectureTopologyMappingEvidence evidence = report.Evidence;
        return JsonSerializer.Serialize(new
        {
            kind = "topology-diff",
            schema_version = 1,
            outcome = report.IsNonReviewableUnassessability ? "unassessable" : "reviewable",
            mode = report.Mode,
            subject_kind = evidence.SubjectKind,
            applicability = FormatApplicability(report.Applicability, report.Membership),
            structural = report.Structural.Select(FormatSubject).ToArray(),
            relational = report.Relational.Select(FormatRelationship).ToArray(),
            unmapped = report.Unmapped.Select(FormatSubject).ToArray(),
            stale = new
            {
                nodes = evidence.StaleNodes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                edges = evidence.StaleEdges
                    .OrderBy(edge => edge.SourceNode, StringComparer.Ordinal)
                    .ThenBy(edge => edge.TargetNode, StringComparer.Ordinal)
                    .Select(edge => new { source_node = edge.SourceNode, target_node = edge.TargetNode })
                    .ToArray(),
            },
            reviewed_out_of_scope = report.ReviewedOutOfScope.Select(FormatSubject).ToArray(),
            evidence = FormatEvidence(evidence),
        }, _jsonOptions);
    }

    internal static string FormatHuman(TopologyDiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> lines =
        [
            "Topology diff (review projection)",
            $"Outcome: {(report.IsNonReviewableUnassessability ? "unassessable" : "reviewable")}",
            $"Mode: {report.Mode}",
            $"Subject kind: {report.Evidence.SubjectKind}",
            $"Applicability: {FormatState(report.Applicability.State)}",
            $"Structural: {report.Structural.Count}",
            $"Relational: {report.Relational.Count}",
            $"Unmapped: {report.Unmapped.Count}",
            $"Stale nodes: {report.Evidence.StaleNodes.Count}",
            $"Stale edges: {report.Evidence.StaleEdges.Count}",
            $"Reviewed out of scope: {report.ReviewedOutOfScope.Count}",
        ];

        if (report.Membership is not null)
        {
            lines.Add($"Applicability membership: {FormatMembership(report.Membership.Value)}");
        }

        lines.Add($"Applicability provenance: {FormatProvenance(report.Applicability.Provenance)}");
        foreach (ArchitectureApplicabilityReason reason in report.Applicability.Reasons)
        {
            lines.Add($"  applicability reason: {reason.Code} ({FormatProvenance(reason.Provenance)})");
        }

        foreach (ArchitectureTopologySubjectEvidence subject in report.Structural)
        {
            lines.Add($"  structural: {subject.Subject} ({string.Join(", ", subject.NodeIds)})");
        }

        foreach (ArchitectureTopologyRelationEvidence relationship in report.Relational)
        {
            lines.Add($"  relational: {relationship.SourceNode} -> {relationship.TargetNode} ({relationship.Witness})");
        }

        foreach (ArchitectureTopologySubjectEvidence subject in report.Unmapped)
        {
            lines.Add($"  unmapped: {subject.Subject} [{subject.Identity}]");
        }

        foreach (string node in report.Evidence.StaleNodes)
        {
            lines.Add($"  stale node: {node}");
        }

        foreach (ArchitectureTopologyStaleEdgeEvidence edge in report.Evidence.StaleEdges)
        {
            lines.Add($"  stale edge: {edge.SourceNode} -> {edge.TargetNode}");
        }

        foreach (ArchitectureTopologySubjectEvidence subject in report.ReviewedOutOfScope)
        {
            lines.Add($"  reviewed out of scope: {subject.Subject} ({subject.ReviewedOutOfScopeId})");
        }

        lines.Add("This is review evidence; drift is not a separate approval criterion.");
        return string.Join(Environment.NewLine, lines);
    }

    private static object FormatSubject(ArchitectureTopologySubjectEvidence subject) => new
    {
        identity = subject.Identity,
        project = subject.Project,
        assembly = subject.Assembly,
        subject = subject.Subject,
        disposition = subject.Disposition,
        node_ids = subject.NodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        reviewed_out_of_scope_id = subject.ReviewedOutOfScopeId,
    };

    private static object FormatRelationship(ArchitectureTopologyRelationEvidence relationship) => new
    {
        source_node = relationship.SourceNode,
        target_node = relationship.TargetNode,
        witness = relationship.Witness,
        is_allowed = relationship.IsAllowed,
    };

    private static object FormatApplicability(
        ArchitectureApplicabilityRecord applicability,
        ArchitectureApplicabilityMembership? membership) => new
        {
            control_identity = applicability.ControlIdentity,
            family = applicability.Family,
            state = FormatState(applicability.State),
            membership = membership is null ? null : FormatMembership(membership.Value),
            provenance = FormatProvenance(applicability.Provenance),
            reasons = applicability.Reasons.Select(reason => new
            {
                code = reason.Code,
                provenance = FormatProvenance(reason.Provenance),
            }).ToArray(),
        };

    private static object FormatProvenance(ArchitectureApplicabilityProvenance provenance) => new
    {
        family = provenance.Family,
        control_identity = provenance.ControlIdentity,
        policy_identity = provenance.PolicyIdentity,
    };

    private static string FormatState(ArchitectureApplicabilityRecordState state) =>
        state.ToString().ToLowerInvariant();

    private static string FormatMembership(ArchitectureApplicabilityMembership membership) =>
        membership.ToString().ToLowerInvariant();

    private static object FormatEvidence(ArchitectureTopologyMappingEvidence evidence) => new
    {
        mode = evidence.Mode,
        subject_kind = evidence.SubjectKind,
        declared_component_count = evidence.DeclaredComponentCount,
        observed_subject_count = evidence.ObservedSubjectCount,
        mapped_subject_count = evidence.MappedSubjectCount,
        reviewed_out_of_scope_subject_count = evidence.ReviewedOutOfScopeSubjectCount,
        unmapped_subject_count = evidence.UnmappedSubjectCount,
        ambiguous_subject_count = evidence.AmbiguousSubjectCount,
        subjects = evidence.Subjects.Select(FormatSubject).ToArray(),
        relationships = evidence.Relationships.Select(FormatRelationship).ToArray(),
        stale_nodes = evidence.StaleNodes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        stale_edges = evidence.StaleEdges
            .OrderBy(edge => edge.SourceNode, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetNode, StringComparer.Ordinal)
            .Select(edge => new { source_node = edge.SourceNode, target_node = edge.TargetNode })
            .ToArray(),
    };
}
