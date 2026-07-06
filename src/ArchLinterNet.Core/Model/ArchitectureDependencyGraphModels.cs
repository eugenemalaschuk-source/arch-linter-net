namespace ArchLinterNet.Core.Model;

public enum ArchitectureGraphNodeKind
{
    Type,
    Namespace,
    Assembly,
    External,
}

public enum ArchitectureGraphLevel
{
    Namespace,
    Type,
    Assembly,
}

public sealed record ArchitectureGraphNode(string Id, ArchitectureGraphNodeKind Kind);

public sealed record ArchitectureGraphEdge(
    string SourceId,
    string TargetId,
    ArchitectureGraphNodeKind SourceKind,
    ArchitectureGraphNodeKind TargetKind,
    IReadOnlyList<string> ContractIds);

public sealed record ArchitectureDependencyGraph(
    IReadOnlyList<ArchitectureGraphNode> Nodes,
    IReadOnlyList<ArchitectureGraphEdge> Edges);
