using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

/// <summary>Closed metric catalog accepted by architecture policies.</summary>
public static class ArchitectureMetricKinds
{
    public const string OutgoingComponentCount = "outgoing_component_count";
    public const string IncomingComponentCount = "incoming_component_count";
    public const string ExternalDependencyGroupCount = "external_dependency_group_count";
    public const string ComponentFootprintCount = "component_footprint_count";
    public const string TopologyTypeCount = "topology_type_count";
    public const string PublicContractSurfaceCount = "public_contract_surface_count";

    public static IReadOnlyList<string> All { get; } =
    [
        OutgoingComponentCount,
        IncomingComponentCount,
        ExternalDependencyGroupCount,
        ComponentFootprintCount,
        TopologyTypeCount,
        PublicContractSurfaceCount,
    ];
}

/// <summary>Native target fields accepted by metric definitions.</summary>
public static class ArchitectureMetricTargetKinds
{
    public const string TopologyNode = "topology_node";
    public const string PublicApiSurface = "public_api_surface";
}

/// <summary>One policy-owned, threshold-free architecture metric definition.</summary>
public sealed class ArchitectureMetricDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "kind")]
    public string Kind { get; set; } = string.Empty;

    [YamlMember(Alias = "topology_node")]
    public string? TopologyNode { get; set; }

    [YamlMember(Alias = "unit")]
    public string? Unit { get; set; }

    [YamlMember(Alias = "public_api_surface")]
    public string? PublicApiSurface { get; set; }

    // The aliases make the domain vocabulary explicit for callers without adding another
    // serialized field or selector concept.
    [YamlIgnore]
    public string? TopologyNodeId => TopologyNode;

    [YamlIgnore]
    public string? PublicApiSurfaceContractId => PublicApiSurface;
}
