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

    [YamlMember(Alias = "contracts")] public ArchitectureContractGroups Contracts { get; set; } = new();
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

    [YamlIgnore] private NamespaceGlobPattern? _cachedGlobPattern;

    [YamlIgnore]
    internal NamespaceGlobPattern GlobPattern =>
        _cachedGlobPattern ??= NamespaceGlobPattern.Parse(Namespace);
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

public sealed class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict")] public List<ArchitectureDependencyContract> Strict { get; set; } = new();

    [YamlMember(Alias = "audit")] public List<ArchitectureDependencyContract> Audit { get; set; } = new();

    [YamlMember(Alias = "strict_layers")] public List<ArchitectureLayerContract> StrictLayers { get; set; } = new();

    [YamlMember(Alias = "audit_layers")] public List<ArchitectureLayerContract> AuditLayers { get; set; } = new();

    [YamlMember(Alias = "strict_allow_only")]
    public List<ArchitectureAllowOnlyContract> StrictAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_allow_only")]
    public List<ArchitectureAllowOnlyContract> AuditAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_cycles")] public List<ArchitectureCycleContract> StrictCycles { get; set; } = new();

    [YamlMember(Alias = "audit_cycles")] public List<ArchitectureCycleContract> AuditCycles { get; set; } = new();

    [YamlMember(Alias = "strict_method_body")]
    public List<ArchitectureMethodBodyContract> StrictMethodBody { get; set; } = new();

    [YamlMember(Alias = "audit_method_body")]
    public List<ArchitectureMethodBodyContract> AuditMethodBody { get; set; } = new();

    [YamlMember(Alias = "strict_asmdef")] public List<ArchitectureAsmdefContract> StrictAsmdef { get; set; } = new();

    [YamlMember(Alias = "audit_asmdef")] public List<ArchitectureAsmdefContract> AuditAsmdef { get; set; } = new();

    [YamlMember(Alias = "strict_independence")]
    public List<ArchitectureIndependenceContract> StrictIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_independence")]
    public List<ArchitectureIndependenceContract> AuditIndependence { get; set; } = new();

    [YamlMember(Alias = "strict_assembly_independence")]
    public List<ArchitectureAssemblyIndependenceContract> StrictAssemblyIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_independence")]
    public List<ArchitectureAssemblyIndependenceContract> AuditAssemblyIndependence { get; set; } = new();

    [YamlMember(Alias = "strict_assembly_dependency")]
    public List<ArchitectureAssemblyDependencyContract> StrictAssemblyDependency { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_dependency")]
    public List<ArchitectureAssemblyDependencyContract> AuditAssemblyDependency { get; set; } = new();

    [YamlMember(Alias = "strict_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> StrictAssemblyAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_assembly_allow_only")]
    public List<ArchitectureAssemblyAllowOnlyContract> AuditAssemblyAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_package_dependency")]
    public List<ArchitecturePackageDependencyContract> StrictPackageDependency { get; set; } = new();

    [YamlMember(Alias = "audit_package_dependency")]
    public List<ArchitecturePackageDependencyContract> AuditPackageDependency { get; set; } = new();

    [YamlMember(Alias = "strict_package_allow_only")]
    public List<ArchitecturePackageAllowOnlyContract> StrictPackageAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_package_allow_only")]
    public List<ArchitecturePackageAllowOnlyContract> AuditPackageAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_project_metadata")]
    public List<ArchitectureProjectMetadataContract> StrictProjectMetadata { get; set; } = new();

    [YamlMember(Alias = "audit_project_metadata")]
    public List<ArchitectureProjectMetadataContract> AuditProjectMetadata { get; set; } = new();

    [YamlMember(Alias = "strict_protected")]
    public List<ArchitectureProtectedContract> StrictProtected { get; set; } = new();

    [YamlMember(Alias = "audit_protected")]
    public List<ArchitectureProtectedContract> AuditProtected { get; set; } = new();

    [YamlMember(Alias = "strict_external")]
    public List<ArchitectureExternalDependencyContract> StrictExternal { get; set; } = new();

    [YamlMember(Alias = "audit_external")]
    public List<ArchitectureExternalDependencyContract> AuditExternal { get; set; } = new();

    [YamlMember(Alias = "strict_external_allow_only")]
    public List<ArchitectureExternalAllowOnlyContract> StrictExternalAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_external_allow_only")]
    public List<ArchitectureExternalAllowOnlyContract> AuditExternalAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_layer_templates")]
    public List<ArchitectureLayerTemplateContract> StrictLayerTemplates { get; set; } = new();

    [YamlMember(Alias = "audit_layer_templates")]
    public List<ArchitectureLayerTemplateContract> AuditLayerTemplates { get; set; } = new();

    [YamlMember(Alias = "strict_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblings { get; set; } = new();

    [YamlMember(Alias = "audit_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblings { get; set; } = new();

    [YamlMember(Alias = "strict_type_placement")]
    public List<ArchitectureTypePlacementContract> StrictTypePlacement { get; set; } = new();

    [YamlMember(Alias = "audit_type_placement")]
    public List<ArchitectureTypePlacementContract> AuditTypePlacement { get; set; } = new();

    [YamlMember(Alias = "strict_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> StrictPublicApiSurface { get; set; } = new();

    [YamlMember(Alias = "audit_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> AuditPublicApiSurface { get; set; } = new();

    [YamlMember(Alias = "strict_attribute_usage")]
    public List<ArchitectureAttributeUsageContract> StrictAttributeUsage { get; set; } = new();

    [YamlMember(Alias = "audit_attribute_usage")]
    public List<ArchitectureAttributeUsageContract> AuditAttributeUsage { get; set; } = new();

    [YamlMember(Alias = "strict_inheritance")]
    public List<ArchitectureInheritanceContract> StrictInheritance { get; set; } = new();

    [YamlMember(Alias = "audit_inheritance")]
    public List<ArchitectureInheritanceContract> AuditInheritance { get; set; } = new();

    [YamlMember(Alias = "strict_interface_implementation")]
    public List<ArchitectureInterfaceImplementationContract> StrictInterfaceImplementation { get; set; } = new();

    [YamlMember(Alias = "audit_interface_implementation")]
    public List<ArchitectureInterfaceImplementationContract> AuditInterfaceImplementation { get; set; } = new();

    [YamlMember(Alias = "strict_composition")]
    public List<ArchitectureCompositionContract> StrictComposition { get; set; } = new();

    [YamlMember(Alias = "audit_composition")]
    public List<ArchitectureCompositionContract> AuditComposition { get; set; } = new();

    // Bound (not executed) so a schema-valid coverage contract is detected and rejected with a
    // clear "reserved, not implemented" diagnostic instead of being silently dropped by
    // IgnoreUnmatchedProperties deserialization. See ArchitecturePolicyDocumentLoader.Load.
    // The coverage engine itself is implemented by #97-#103.
    [YamlMember(Alias = "strict_coverage")]
    public List<ArchitectureCoverageContract> StrictCoverage { get; set; } = new();

    [YamlMember(Alias = "audit_coverage")]
    public List<ArchitectureCoverageContract> AuditCoverage { get; set; } = new();

    public IEnumerable<IArchitectureContract> AllStrict => EnumerateStrict();

    public IEnumerable<IArchitectureContract> AllAudit => EnumerateAudit();

    private IEnumerable<IArchitectureContract> EnumerateStrict()
    {
        foreach (var c in Strict) yield return c;
        foreach (var c in StrictLayers) yield return c;
        foreach (var c in StrictAllowOnly) yield return c;
        foreach (var c in StrictCycles) yield return c;
        foreach (var c in StrictMethodBody) yield return c;
        foreach (var c in StrictAsmdef) yield return c;
        foreach (var c in StrictIndependence) yield return c;
        foreach (var c in StrictAssemblyIndependence) yield return c;
        foreach (var c in StrictAssemblyDependency) yield return c;
        foreach (var c in StrictAssemblyAllowOnly) yield return c;
        foreach (var c in StrictPackageDependency) yield return c;
        foreach (var c in StrictPackageAllowOnly) yield return c;
        foreach (var c in StrictProjectMetadata) yield return c;
        foreach (var c in StrictProtected) yield return c;
        foreach (var c in StrictExternal) yield return c;
        foreach (var c in StrictExternalAllowOnly) yield return c;
        foreach (var c in StrictAcyclicSiblings) yield return c;
        foreach (var c in StrictTypePlacement) yield return c;
        foreach (var c in StrictPublicApiSurface) yield return c;
        foreach (var c in StrictAttributeUsage) yield return c;
        foreach (var c in StrictInheritance) yield return c;
        foreach (var c in StrictInterfaceImplementation) yield return c;
        foreach (var c in StrictComposition) yield return c;
        foreach (var c in StrictCoverage) yield return c;
    }

    private IEnumerable<IArchitectureContract> EnumerateAudit()
    {
        foreach (var c in Audit) yield return c;
        foreach (var c in AuditLayers) yield return c;
        foreach (var c in AuditAllowOnly) yield return c;
        foreach (var c in AuditCycles) yield return c;
        foreach (var c in AuditMethodBody) yield return c;
        foreach (var c in AuditAsmdef) yield return c;
        foreach (var c in AuditIndependence) yield return c;
        foreach (var c in AuditAssemblyIndependence) yield return c;
        foreach (var c in AuditAssemblyDependency) yield return c;
        foreach (var c in AuditAssemblyAllowOnly) yield return c;
        foreach (var c in AuditPackageDependency) yield return c;
        foreach (var c in AuditPackageAllowOnly) yield return c;
        foreach (var c in AuditProjectMetadata) yield return c;
        foreach (var c in AuditProtected) yield return c;
        foreach (var c in AuditExternal) yield return c;
        foreach (var c in AuditExternalAllowOnly) yield return c;
        foreach (var c in AuditAcyclicSiblings) yield return c;
        foreach (var c in AuditTypePlacement) yield return c;
        foreach (var c in AuditPublicApiSurface) yield return c;
        foreach (var c in AuditAttributeUsage) yield return c;
        foreach (var c in AuditInheritance) yield return c;
        foreach (var c in AuditInterfaceImplementation) yield return c;
        foreach (var c in AuditComposition) yield return c;
        foreach (var c in AuditCoverage) yield return c;
    }
}

public sealed class ArchitectureExternalDependencyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureExternalAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureDependencyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "forbidden_legacy_runtime")]
    public bool ForbiddenLegacyRuntime { get; set; }

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureTemplateLayer
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "optional")] public bool Optional { get; set; }
}

public sealed class ArchitectureLayerTemplateContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "containers")] public List<string> Containers { get; set; } = new();

    [YamlMember(Alias = "layers")] public List<ArchitectureTemplateLayer> Layers { get; set; } = new();

    [YamlMember(Alias = "exhaustive")] public bool Exhaustive { get; set; }

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureLayerContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlIgnore] public HashSet<string> OptionalLayers { get; set; } = new(StringComparer.Ordinal);

    [YamlIgnore] public string? TemplateName { get; init; }

    [YamlIgnore] public string? ContainerNamespace { get; init; }

    [YamlIgnore] public bool Exhaustive { get; init; }

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCycleContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAcyclicSiblingContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "ancestors")] public List<string> Ancestors { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCoverageRoot
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "include")] public List<string> Include { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<string> Exclude { get; set; } = new();
}

public sealed class ArchitectureCoverageExclusion
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "project")] public string Project { get; set; } = string.Empty;

    [YamlMember(Alias = "assembly")] public string Assembly { get; set; } = string.Empty;

    [YamlMember(Alias = "contract_id")] public string ContractId { get; set; } = string.Empty;

    [YamlMember(Alias = "between")] public List<string> Between { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureCoverageContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "scope")] public string Scope { get; set; } = string.Empty;

    [YamlMember(Alias = "roots")] public List<ArchitectureCoverageRoot> Roots { get; set; } = new();

    [YamlMember(Alias = "between")] public List<List<string>> Between { get; set; } = new();

    [YamlMember(Alias = "contract_ids")] public List<string> ContractIds { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<ArchitectureCoverageExclusion> Exclude { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
}

public sealed class ArchitectureMethodBodyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_calls")]
    public List<string> ForbiddenCalls { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAsmdefContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source_assemblies")]
    public List<string> SourceAssemblies { get; set; } = new();

    [YamlMember(Alias = "forbidden_editor_refs")]
    public bool ForbiddenEditorRefs { get; set; }

    [YamlMember(Alias = "forbidden_asmdef_prefixes")]
    public List<string> ForbiddenAsmdefPrefixes { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureIndependenceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "layers")] public List<string> Layers { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAssemblyIndependenceContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "assemblies")] public List<string> Assemblies { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAssemblyDependencyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureAssemblyAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitecturePackageDependencyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden")] public List<string> Forbidden { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitecturePackageAllowOnlyContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source")] public string Source { get; set; } = string.Empty;

    [YamlMember(Alias = "allowed")] public List<string> Allowed { get; set; } = new();

    [YamlMember(Alias = "dependency_depth")]
    public DependencyDepthMode DependencyDepth { get; set; } = DependencyDepthMode.Direct;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureProjectMetadataContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "projects")] public List<string> Projects { get; set; } = new();

    [YamlMember(Alias = "required_properties")]
    public Dictionary<string, string> RequiredProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [YamlMember(Alias = "forbidden_properties")]
    public Dictionary<string, string> ForbiddenProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [YamlMember(Alias = "allowed_friend_assemblies")]
    public List<string> AllowedFriendAssemblies { get; set; } = new();

    [YamlMember(Alias = "forbidden_project_references")]
    public List<string> ForbiddenProjectReferences { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureProtectedContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "protected")] public List<string> Protected { get; set; } = new();

    [YamlMember(Alias = "allowed_importers")]
    public List<string> AllowedImporters { get; set; } = new();

    [YamlMember(Alias = "allowed_types")] public List<string> AllowedTypes { get; set; } = new();

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureIgnoredViolation
{
    [YamlMember(Alias = "source_type")] public string SourceType { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_reference")]
    public string ForbiddenReference { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

