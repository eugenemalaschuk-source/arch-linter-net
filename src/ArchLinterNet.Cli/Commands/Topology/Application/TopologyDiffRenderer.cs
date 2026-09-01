using System.Text.Json;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Topology.Application;

internal sealed record TopologyDiffReport(
    string Mode,
    ArchitectureTopologyMappingEvidence Evidence,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> Structural,
    IReadOnlyList<ArchitectureTopologyRelationEvidence> Relational,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> Unmapped,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> ReviewedOutOfScope);

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
            mode = report.Mode,
            subject_kind = evidence.SubjectKind,
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
            $"Mode: {report.Mode}",
            $"Subject kind: {report.Evidence.SubjectKind}",
            $"Structural: {report.Structural.Count}",
            $"Relational: {report.Relational.Count}",
            $"Unmapped: {report.Unmapped.Count}",
            $"Stale nodes: {report.Evidence.StaleNodes.Count}",
            $"Stale edges: {report.Evidence.StaleEdges.Count}",
            $"Reviewed out of scope: {report.ReviewedOutOfScope.Count}",
        ];

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
