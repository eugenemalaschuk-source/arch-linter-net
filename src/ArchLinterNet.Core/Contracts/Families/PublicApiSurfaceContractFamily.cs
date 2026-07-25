using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public static class PublicApiComparisonModes
{
    public const string AdditionsOnly = "additions_only";
    public const string Exact = "exact";

    public static readonly IReadOnlyList<string> All = new[] { AdditionsOnly, Exact };
}

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

    // Path to a reviewed public API snapshot file, resolved relative to the policy boundary at
    // load time. Its entries are unioned with DeclaredApi to form the declared surface.
    [YamlMember(Alias = "api_snapshot")] public string? ApiSnapshot { get; set; }

    [YamlMember(Alias = "api_comparison")]
    public string ApiComparison { get; set; } = PublicApiComparisonModes.AdditionsOnly;

    // Populated by the policy loader from ApiSnapshot; never authored in YAML.
    [YamlIgnore]
    public IReadOnlyList<PublicApiSnapshotEntry> ResolvedSnapshotEntries { get; set; } =
        Array.Empty<PublicApiSnapshotEntry>();

    [YamlMember(Alias = "forbid_public_constants_unless_declared")]
    public bool ForbidPublicConstantsUnlessDeclared { get; set; }

    [YamlMember(Alias = "allowed_public_constants")]
    public List<string> AllowedPublicConstants { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
