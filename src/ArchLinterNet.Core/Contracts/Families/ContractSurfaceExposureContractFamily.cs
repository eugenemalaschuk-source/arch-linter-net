using ArchLinterNet.Core.Contracts;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

/// <summary>
/// The source universe for a contract-surface exposure rule.
/// </summary>
/// <remarks>
/// Every populated member is an independent conjunctive constraint. The type selector and
/// forbidden selectors intentionally reuse <see cref="ArchitecturePublicApiSurfaceSelector"/>
/// so the exposure family cannot introduce a second structural or semantic selector language.
/// </remarks>
public sealed class ArchitectureContractSurfaceExposureSource
{
    [YamlMember(Alias = "assemblies")]
    public List<string> Assemblies { get; set; } = new();

    [YamlMember(Alias = "projects")]
    public List<string> Projects { get; set; } = new();

    [YamlMember(Alias = "types_matching")]
    public ArchitecturePublicApiSurfaceSelector? TypesMatching { get; set; }

    [YamlMember(Alias = "public_api_surface")]
    public string? PublicApiSurface { get; set; }
}

/// <summary>
/// Prohibits selected visible contract surfaces from exposing selected type targets.
/// </summary>
public sealed class ArchitectureContractSurfaceExposureContract : IArchitectureContract
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    [YamlMember(Alias = "source")]
    public ArchitectureContractSurfaceExposureSource Source { get; set; } = new();

    [YamlMember(Alias = "forbidden")]
    public List<ArchitecturePublicApiSurfaceSelector> Forbidden { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")]
    public string Reason { get; set; } = string.Empty;
}
