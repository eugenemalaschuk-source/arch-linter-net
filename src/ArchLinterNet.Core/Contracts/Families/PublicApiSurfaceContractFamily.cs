using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> StrictPublicApiSurface { get; set; } = new();

    [YamlMember(Alias = "audit_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> AuditPublicApiSurface { get; set; } = new();
}

public sealed class ArchitecturePublicApiSurfaceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "assemblies")] public List<string> Assemblies { get; set; } = new();

    [YamlMember(Alias = "declared_api")] public List<string> DeclaredApi { get; set; } = new();

    [YamlMember(Alias = "forbid_public_constants_unless_declared")]
    public bool ForbidPublicConstantsUnlessDeclared { get; set; }

    [YamlMember(Alias = "allowed_public_constants")]
    public List<string> AllowedPublicConstants { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
