using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_layout_conventions")]
    public List<ArchitectureLayoutConventionContract> StrictLayoutConventions { get; set; } = new();

    [YamlMember(Alias = "audit_layout_conventions")]
    public List<ArchitectureLayoutConventionContract> AuditLayoutConventions { get; set; } = new();
}

// files_matching selects candidate source files from ArchitectureSourceFileFactIndex by
// folder/namespace/file-name path facts (exact/prefix/suffix only, AND-combined) - a distinct type
// from ArchitectureTypeMatcher (which selects live reflected Types) and from ArchitectureContextSelector
// (role+metadata). The optional `when` predicate is call-site-scoped per
// openspec/changes/add-semantic-layout-convention-contracts/design.md Decision D2 (mirrors
// ArchitectureContextSelector's WhenLocation/WhenContractName/CompiledWhen cache idiom) and compiles
// against the existing `subject` schema unchanged - no new CEL schema surface.
public sealed class ArchitectureLayoutFileMatcher
{
    [YamlMember(Alias = "folder_segment")] public string FolderSegment { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_segment")] public string NamespaceSegment { get; set; } = string.Empty;

    [YamlMember(Alias = "file_name_suffix")] public string FileNameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "file_name_prefix")] public string FileNamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "when")] public string? When { get; set; }

    [YamlIgnore]
    internal CelCompiledPredicate? CompiledWhen { get; set; }

    [YamlIgnore]
    internal ArchitecturePolicySourceLocation? WhenLocation { get; set; }

    [YamlIgnore]
    internal string? WhenContractName { get; set; }
}

public sealed class ArchitectureRequireMatchingInterface
{
    [YamlMember(Alias = "name_prefix")] public string? NamePrefix { get; set; }
}

public sealed class ArchitectureLayoutConventionContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "files_matching")]
    public ArchitectureLayoutFileMatcher FilesMatching { get; set; } = new();

    [YamlMember(Alias = "require_type_kind")] public string RequireTypeKind { get; set; } = string.Empty;

    [YamlMember(Alias = "forbid_type_kind")] public string ForbidTypeKind { get; set; } = string.Empty;

    [YamlMember(Alias = "required_name_suffix")]
    public string RequiredNameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "required_name_prefix")]
    public string RequiredNamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_name_suffix")]
    public string ForbiddenNameSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_name_prefix")]
    public string ForbiddenNamePrefix { get; set; } = string.Empty;

    [YamlMember(Alias = "require_type_name_matches_file_name")]
    public bool RequireTypeNameMatchesFileName { get; set; }

    [YamlMember(Alias = "require_matching_interface")]
    public ArchitectureRequireMatchingInterface? RequireMatchingInterface { get; set; }

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

// Single source of truth for `require_type_kind`/`forbid_type_kind` string parsing, shared by
// LayoutConventionsValidator (load-time) and ArchitectureAnalysisSession.LayoutConventions
// (execution-time), so both reject the same inputs identically. Deliberately NOT Enum.TryParse:
// that method accepts any string parseable as the enum's underlying integer (e.g. "6", "999")
// as an unnamed, always-non-matching value, and accepts "Unknown" - an internal reflection-fallback
// sentinel from ArchitectureDeclaredTypeFact, never a legitimate policy-author choice - producing a
// contract that loads successfully but can never match anything (fail-open, silently a no-op).
internal static class ArchitectureLayoutTypeKindParser
{
    private static readonly Dictionary<string, ArchitectureTypeKind> _byName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["class"] = ArchitectureTypeKind.Class,
            ["interface"] = ArchitectureTypeKind.Interface,
            ["struct"] = ArchitectureTypeKind.Struct,
            ["enum"] = ArchitectureTypeKind.Enum,
            ["record"] = ArchitectureTypeKind.Record,
            ["delegate"] = ArchitectureTypeKind.Delegate,
        };

    public static bool TryParse(string value, out ArchitectureTypeKind kind) => _byName.TryGetValue(value, out kind);
}
