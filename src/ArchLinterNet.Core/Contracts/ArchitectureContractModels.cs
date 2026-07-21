using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public interface IArchitectureContract
{
    string Name { get; }
    string? Id { get; set; }
}

public sealed class ArchitectureContractDocument
{
    [YamlMember(Alias = "version")] public int Version { get; set; }

    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "layers")] public Dictionary<string, ArchitectureLayer> Layers { get; set; } = new();

    [YamlMember(Alias = "external_dependencies")]
    public Dictionary<string, ArchitectureExternalDependencyGroup> ExternalDependencies { get; set; } = new();

    [YamlMember(Alias = "packages")]
    public Dictionary<string, ArchitecturePackageGroup> Packages { get; set; } = new();

    [YamlMember(Alias = "legacy_runtime_layers")]
    public List<string> LegacyRuntimeLayers { get; set; } = new();

    [YamlMember(Alias = "analysis")] public ArchitectureAnalysisConfiguration Analysis { get; set; } = new();

    [YamlMember(Alias = "contracts")] public Families.ArchitectureContractGroups Contracts { get; set; } = new();

    [YamlMember(Alias = "classification")]
    public ArchitectureClassificationConfiguration Classification { get; set; } = new();

    // Not YAML-bound (deliberately: classification.path itself stays unbound/inert, see
    // ArchitectureClassificationConfiguration) — set post-deserialization by ArchitecturePolicyDocumentLoader
    // from a raw-YAML presence check when classification.path declares at least one entry.
    [YamlIgnore]
    public ArchitectureClassificationPathDeferredNotice? ClassificationPathDeferred { get; set; }

    [YamlIgnore]
    public ArchitecturePolicyProvenanceIndex Provenance { get; internal set; } =
        ArchitecturePolicyProvenanceIndex.Empty;
}

public sealed class ArchitectureAnalysisConfiguration
{
    [YamlMember(Alias = "target_assemblies")]
    public List<string> TargetAssemblies { get; set; } = new();

    [YamlMember(Alias = "assembly_search_paths")]
    public List<string> AssemblySearchPaths { get; set; } = new();

    [YamlMember(Alias = "source_roots")]
    public List<string> SourceRoots { get; set; } = new();

    [YamlMember(Alias = "solution")]
    public string Solution { get; set; } = string.Empty;

    [YamlMember(Alias = "projects")]
    public List<string> Projects { get; set; } = new();

    [YamlMember(Alias = "project_include")]
    public List<string> ProjectInclude { get; set; } = new();

    [YamlMember(Alias = "project_exclude")]
    public List<string> ProjectExclude { get; set; } = new();

    [YamlMember(Alias = "configuration")]
    public string Configuration { get; set; } = "Debug";

    [YamlMember(Alias = "target_framework")]
    public string TargetFramework { get; set; } = string.Empty;

    [YamlMember(Alias = "unmatched_ignored_violations")]
    public string UnmatchedIgnoredViolations { get; set; } = "error";

    [YamlMember(Alias = "policy_consistency")]
    public string PolicyConsistency { get; set; } = "error";

    [YamlMember(Alias = "coverage")]
    public string Coverage { get; set; } = "error";

    [YamlMember(Alias = "condition_sets")]
    public Dictionary<string, List<string>> ConditionSets { get; set; } = new();

    [YamlMember(Alias = "default_condition_set")]
    public string DefaultConditionSet { get; set; } = string.Empty;
}

public sealed class ArchitectureLayer
{
    private string _namespace = string.Empty;

    [YamlMember(Alias = "namespace")]
    public string Namespace
    {
        get => _namespace;
        set
        {
            _namespace = value;
            _cachedGlobPattern = null;
        }
    }

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "external")] public bool External { get; set; }

    [YamlMember(Alias = "selector")] public ArchitectureLayerSelector? Selector { get; set; }

    // Namespaces matched by Namespace/NamespaceSuffix above are subtracted by any entry here:
    // a namespace is in this layer's scope only if it matches the inclusion glob AND matches no
    // Exclude entry. Empty by default, so layers with no `exclude:` key are byte-for-byte
    // unchanged. See ArchitectureLayerResolver.MatchNamespace and openspec/specs/layer-contracts.
    [YamlMember(Alias = "exclude")] public List<ArchitectureLayerExclusion> Exclude { get; set; } = new();

    [YamlIgnore] private NamespaceGlobPattern? _cachedGlobPattern;

    [YamlIgnore]
    internal NamespaceGlobPattern GlobPattern =>
        _cachedGlobPattern ??= NamespaceGlobPattern.Parse(Namespace);
}

public sealed class ArchitectureLayerExclusion
{
    private string _namespace = string.Empty;

    [YamlMember(Alias = "namespace")]
    public string Namespace
    {
        get => _namespace;
        set
        {
            _namespace = value;
            _cachedGlobPattern = null;
        }
    }

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlIgnore] private NamespaceGlobPattern? _cachedGlobPattern;

    [YamlIgnore]
    internal NamespaceGlobPattern GlobPattern =>
        _cachedGlobPattern ??= NamespaceGlobPattern.Parse(Namespace);
}

public sealed class ArchitectureLayerSelector
{
    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);

    // Refines the selector match over a `subject` CEL context. See
    // openspec/specs/cel-policy-model/spec.md and Contracts/Validators/ExpressionCompilationValidator.cs.
    [YamlMember(Alias = "when")] public string? When { get; set; }

    // Populated once by ExpressionCompilationValidator during ArchitecturePolicyDocumentLoader.Load
    // when When is non-empty; mirrors ArchitectureLayer's _cachedGlobPattern/GlobPattern lazy-field
    // idiom so evaluation (added by #164) never re-parses the expression.
    [YamlIgnore]
    internal CelCompiledPredicate? CompiledWhen { get; set; }

    // Populated alongside CompiledWhen via ArchitecturePolicyProvenanceIndex.TryGetLocation(path) —
    // a real, resolved location (source file, YAML path, line/column) — so an evaluation-time error
    // can construct a proper ArchitecturePolicyDiagnostic naming exactly which layer's selector
    // failed, through the same structured JSON/SARIF path load-time errors use. The layer name
    // isn't otherwise recoverable at the matcher, which only receives the selector object.
    [YamlIgnore]
    internal ArchitecturePolicySourceLocation? WhenLocation { get; set; }
}

public sealed class ArchitectureExternalDependencyGroup
{
    [YamlMember(Alias = "namespace_prefixes")]
    public List<string> NamespacePrefixes { get; set; } = new();

    [YamlMember(Alias = "type_prefixes")]
    public List<string> TypePrefixes { get; set; } = new();
}

public sealed class ArchitecturePackageGroup
{
    [YamlMember(Alias = "package_ids")]
    public List<string> PackageIds { get; set; } = new();

    [YamlMember(Alias = "package_prefixes")]
    public List<string> PackagePrefixes { get; set; } = new();
}

public sealed class ArchitectureIgnoredViolation
{
    [YamlMember(Alias = "source_type")] public string SourceType { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_reference")]
    public string ForbiddenReference { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    // Populated only when this entry was merged in from a `version: 2` baseline document (see
    // ArchitectureBaselineLoadingService.ContractGroupMerger). When IdentityVersion is set,
    // ArchitectureIgnoreMatcher matches this entry against the live structured identity instead of
    // the (source_type, forbidden_reference) glob pair, so runtime `validate --baseline` gets the
    // same exact-identity guarantees as `baseline diff`/`verify`. Manually authored policy
    // `ignored_violations` never set these — they stay null and match via the legacy glob path.
    [YamlMember(Alias = "identity_version")] public int? IdentityVersion { get; set; }

    [YamlMember(Alias = "contract_family")] public string? ContractFamily { get; set; }

    [YamlMember(Alias = "kind")] public string? Kind { get; set; }

    [YamlMember(Alias = "source_assembly")] public string? SourceAssembly { get; set; }

    [YamlMember(Alias = "source_member")] public string? SourceMember { get; set; }

    [YamlMember(Alias = "target_assembly")] public string? TargetAssembly { get; set; }

    [YamlMember(Alias = "target_type")] public string? TargetType { get; set; }

    [YamlMember(Alias = "target_member")] public string? TargetMember { get; set; }

    [YamlMember(Alias = "occurrence")] public int? Occurrence { get; set; }

    [YamlMember(Alias = "configuration")] public string? Configuration { get; set; }
}
