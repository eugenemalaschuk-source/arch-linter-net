using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

/// <summary>A named, bounded local surface used by versioned contract-surface isolation.</summary>
public sealed class ArchitectureVersionedContractSurfaceIsolationSurface
{
    [YamlMember(Alias = "id")] public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "types_matching")]
    public ArchitecturePublicApiSurfaceSelector TypesMatching { get; set; } = new();
}

/// <summary>Declares which local versioned surfaces may not be exposed by a source surface.</summary>
public sealed class ArchitectureVersionedContractSurfaceIsolationContract : IArchitectureContract
{
    [YamlMember(Alias = "id")] public string? Id { get; set; }
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;
    [YamlMember(Alias = "surfaces")] public List<ArchitectureVersionedContractSurfaceIsolationSurface> Surfaces { get; set; } = new();
    [YamlMember(Alias = "source_surface")] public string SourceSurface { get; set; } = string.Empty;
    [YamlMember(Alias = "forbidden_surfaces")] public List<string> ForbiddenSurfaces { get; set; } = new();
    [YamlMember(Alias = "ignored_violations")] public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
