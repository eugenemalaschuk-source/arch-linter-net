using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public static class PublicApiComparisonModes
{
    public const string AdditionsOnly = "additions_only";
    public const string Exact = "exact";

    public static readonly IReadOnlyList<string> All = new[] { AdditionsOnly, Exact };
}

// Why a snapshot is unusable, as typed data rather than a substring of ApiSnapshotError's message.
// Consumers that need to branch on the reason (for example, "missing" is the one recoverable state
// a first `public-api capture` must be able to run through) must never parse the human-readable
// message: an existing, corrupt file could legitimately be named in a way that contains the phrase
// another error uses, which would misclassify it under text matching.
public enum PublicApiSnapshotErrorKind
{
    None,
    Missing,
    ParseError,
    OwnershipError,
}

// Restricts the contract's governed surface to types matching bounded, already-delivered
// evidence: the same structural matcher vocabulary type_placement.types_matching uses
// (ArchitectureTypeRoleMatcher), plus Role, matched via the existing semantic role index
// (ArchitectureContextSelectorMatcher.MatchesLiteral). No new matcher/tag engine — see
// openspec/changes/add-public-api-surface-selector/design.md Decision 1. Absent selector
// preserves the pre-existing unconditional assembly-wide behavior.
public sealed class ArchitecturePublicApiSurfaceSelector
{
    [YamlMember(Alias = "name_suffix")] public string NameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "name_prefix")] public string NamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "layer")] public string Layer { get; set; } = string.Empty;

    [YamlMember(Alias = "base_type")] public string BaseType { get; set; } = string.Empty;

    [YamlMember(Alias = "implements_interface")]
    public string ImplementsInterface { get; set; } = string.Empty;

    [YamlMember(Alias = "has_attribute")] public string HasAttribute { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    public bool HasAnyField =>
        !string.IsNullOrEmpty(NameSuffix)
        || !string.IsNullOrEmpty(NamePrefix)
        || !string.IsNullOrEmpty(Namespace)
        || !string.IsNullOrEmpty(Layer)
        || !string.IsNullOrEmpty(BaseType)
        || !string.IsNullOrEmpty(ImplementsInterface)
        || !string.IsNullOrEmpty(HasAttribute)
        || !string.IsNullOrEmpty(Role);
}

public sealed class ArchitecturePublicApiSurfaceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "assemblies")] public List<string> Assemblies { get; set; } = new();

    [YamlMember(Alias = "declared_api")] public List<string> DeclaredApi { get; set; } = new();

    // Optional intentional-surface selector (issue #525). Null means "no selector" — every
    // exported type/member in Assemblies remains governed, unchanged from prior behavior.
    [YamlMember(Alias = "surface_selector")]
    public ArchitecturePublicApiSurfaceSelector? SurfaceSelector { get; set; }

    // Path to a reviewed public API snapshot file, resolved relative to the policy boundary at
    // load time. Its entries are unioned with DeclaredApi to form the declared surface.
    [YamlMember(Alias = "api_snapshot")] public string? ApiSnapshot { get; set; }

    [YamlMember(Alias = "api_comparison")]
    public string ApiComparison { get; set; } = PublicApiComparisonModes.AdditionsOnly;

    // Populated by the policy loader from ApiSnapshot; never authored in YAML.
    [YamlIgnore]
    public IReadOnlyList<PublicApiSnapshotEntry> ResolvedSnapshotEntries { get; set; } =
        Array.Empty<PublicApiSnapshotEntry>();

    // Absolute, boundary-checked path of ApiSnapshot. Every read and write must target this path
    // rather than re-resolving the authored string against the process working directory.
    [YamlIgnore]
    public string? ResolvedSnapshotPath { get; set; }

    // Set when the snapshot is missing, unparsable, or owned by another contract. Recorded rather
    // than thrown so the first `public-api capture` can still load the policy that declares the
    // snapshot it is about to create; validation turns this into a violation.
    [YamlIgnore]
    public string? ApiSnapshotError { get; set; }

    [YamlIgnore]
    public PublicApiSnapshotErrorKind ApiSnapshotErrorKind { get; set; } = PublicApiSnapshotErrorKind.None;

    [YamlMember(Alias = "forbid_public_constants_unless_declared")]
    public bool ForbidPublicConstantsUnlessDeclared { get; set; }

    [YamlMember(Alias = "allowed_public_constants")]
    public List<string> AllowedPublicConstants { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
