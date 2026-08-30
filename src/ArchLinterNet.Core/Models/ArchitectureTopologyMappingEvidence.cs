namespace ArchLinterNet.Core.Model;

/// <summary>One canonical observed subject and its declared-topology disposition.</summary>
public sealed record ArchitectureTopologySubjectEvidence(
    string Identity,
    string Project,
    string Assembly,
    string Subject,
    string Disposition,
    IReadOnlyList<string>? NodeIds = null,
    string? ReviewedOutOfScopeId = null)
{
    public IReadOnlyList<string> NodeIds { get; init; } = (NodeIds ?? Array.Empty<string>())
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();
}

/// <summary>One observed relationship between two exactly mapped topology components.</summary>
public sealed record ArchitectureTopologyRelationEvidence(
    string SourceNode,
    string TargetNode,
    string Witness,
    bool IsAllowed);

/// <summary>One enabled declared edge that did not occur in the observed topology.</summary>
public sealed record ArchitectureTopologyStaleEdgeEvidence(string SourceNode, string TargetNode);

/// <summary>
/// Deterministic, family-native evidence for the declared-topology applicability control. Counts
/// are completeness evidence only; they are never an architecture-quality score.
/// </summary>
public sealed record ArchitectureTopologyMappingEvidence(
    string Mode,
    string SubjectKind,
    int DeclaredComponentCount,
    IReadOnlyList<ArchitectureTopologySubjectEvidence> Subjects,
    IReadOnlyList<ArchitectureTopologyRelationEvidence> Relationships,
    IReadOnlyList<string> StaleNodes,
    IReadOnlyList<ArchitectureTopologyStaleEdgeEvidence> StaleEdges)
{
    public IReadOnlyList<ArchitectureTopologySubjectEvidence> Subjects { get; init; } = Subjects
        .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<ArchitectureTopologyRelationEvidence> Relationships { get; init; } = Relationships
        .OrderBy(relationship => relationship.SourceNode, StringComparer.Ordinal)
        .ThenBy(relationship => relationship.TargetNode, StringComparer.Ordinal)
        .ThenBy(relationship => relationship.Witness, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> StaleNodes { get; init; } = StaleNodes
        .OrderBy(node => node, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<ArchitectureTopologyStaleEdgeEvidence> StaleEdges { get; init; } = StaleEdges
        .OrderBy(edge => edge.SourceNode, StringComparer.Ordinal)
        .ThenBy(edge => edge.TargetNode, StringComparer.Ordinal)
        .ToArray();

    public int ObservedSubjectCount => Subjects.Count;

    public int MappedSubjectCount => Count("mapped");

    public int ReviewedOutOfScopeSubjectCount => Count("reviewed_out_of_scope");

    public int UnmappedSubjectCount => Count("unmapped");

    public int AmbiguousSubjectCount => Count("ambiguous");

    private int Count(string disposition) => Subjects.Count(subject =>
        string.Equals(subject.Disposition, disposition, StringComparison.Ordinal));
}
