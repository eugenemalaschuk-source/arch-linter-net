using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

// This is the YAML binding root. Contract POCOs remain in one file per family; this single
// declaration owns every top-level strict/audit collection so the binding model is not a
// cross-file partial aggregate. ArchitectureContractFamilyBindings remains the authoritative
// registry for enumeration and execution.
public sealed class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict")]
    public List<ArchitectureDependencyContract> Strict { get; set; } = new();

    [YamlMember(Alias = "audit")]
    public List<ArchitectureDependencyContract> Audit { get; set; } = new();

    [YamlMember(Alias = "strict_allow_only")]
    public List<ArchitectureAllowOnlyContract> StrictAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_allow_only")]
    public List<ArchitectureAllowOnlyContract> AuditAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_layers")]
    public List<ArchitectureLayerContract> StrictLayers { get; set; } = new();

    [YamlMember(Alias = "audit_layers")]
    public List<ArchitectureLayerContract> AuditLayers { get; set; } = new();

    [YamlMember(Alias = "strict_layer_templates")]
    public List<ArchitectureLayerTemplateContract> StrictLayerTemplates { get; set; } = new();

    [YamlMember(Alias = "audit_layer_templates")]
    public List<ArchitectureLayerTemplateContract> AuditLayerTemplates { get; set; } = new();

    [YamlMember(Alias = "strict_cycles")]
    public List<ArchitectureCycleContract> StrictCycles { get; set; } = new();

    [YamlMember(Alias = "audit_cycles")]
    public List<ArchitectureCycleContract> AuditCycles { get; set; } = new();

    [YamlMember(Alias = "strict_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblings { get; set; } = new();

    [YamlMember(Alias = "audit_acyclic_siblings")]
    public List<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblings { get; set; } = new();

    [YamlMember(Alias = "strict_method_body")]
    public List<ArchitectureMethodBodyContract> StrictMethodBody { get; set; } = new();

    [YamlMember(Alias = "audit_method_body")]
    public List<ArchitectureMethodBodyContract> AuditMethodBody { get; set; } = new();

    [YamlMember(Alias = "strict_asmdef")]
    public List<ArchitectureAsmdefContract> StrictAsmdef { get; set; } = new();

    [YamlMember(Alias = "audit_asmdef")]
    public List<ArchitectureAsmdefContract> AuditAsmdef { get; set; } = new();

    [YamlMember(Alias = "strict_independence")]
    public List<ArchitectureIndependenceContract> StrictIndependence { get; set; } = new();

    [YamlMember(Alias = "audit_independence")]
    public List<ArchitectureIndependenceContract> AuditIndependence { get; set; } = new();

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

    [YamlMember(Alias = "strict_type_placement")]
    public List<ArchitectureTypePlacementContract> StrictTypePlacement { get; set; } = new();

    [YamlMember(Alias = "audit_type_placement")]
    public List<ArchitectureTypePlacementContract> AuditTypePlacement { get; set; } = new();

    [YamlMember(Alias = "strict_layout_conventions")]
    public List<ArchitectureLayoutConventionContract> StrictLayoutConventions { get; set; } = new();

    [YamlMember(Alias = "audit_layout_conventions")]
    public List<ArchitectureLayoutConventionContract> AuditLayoutConventions { get; set; } = new();

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

    [YamlMember(Alias = "strict_port_boundaries")]
    public List<ArchitecturePortBoundaryContract> StrictPortBoundaries { get; set; } = new();

    [YamlMember(Alias = "audit_port_boundaries")]
    public List<ArchitecturePortBoundaryContract> AuditPortBoundaries { get; set; } = new();

    [YamlMember(Alias = "strict_context_dependencies")]
    public List<ArchitectureContextDependencyContract> StrictContextDependencies { get; set; } = new();

    [YamlMember(Alias = "audit_context_dependencies")]
    public List<ArchitectureContextDependencyContract> AuditContextDependencies { get; set; } = new();

    [YamlMember(Alias = "strict_context_allow_only")]
    public List<ArchitectureContextAllowOnlyContract> StrictContextAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_context_allow_only")]
    public List<ArchitectureContextAllowOnlyContract> AuditContextAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> StrictFrameworkDependency { get; set; } = new();

    [YamlMember(Alias = "audit_framework_dependency")]
    public List<ArchitectureFrameworkReferenceContract> AuditFrameworkDependency { get; set; } = new();

    [YamlMember(Alias = "strict_framework_allow_only")]
    public List<ArchitectureFrameworkReferenceAllowOnlyContract> StrictFrameworkAllowOnly { get; set; } = new();

    [YamlMember(Alias = "audit_framework_allow_only")]
    public List<ArchitectureFrameworkReferenceAllowOnlyContract> AuditFrameworkAllowOnly { get; set; } = new();

    [YamlMember(Alias = "strict_project_metadata")]
    public List<ArchitectureProjectMetadataContract> StrictProjectMetadata { get; set; } = new();

    [YamlMember(Alias = "audit_project_metadata")]
    public List<ArchitectureProjectMetadataContract> AuditProjectMetadata { get; set; } = new();

    [YamlMember(Alias = "strict_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> StrictPublicApiSurface { get; set; } = new();

    [YamlMember(Alias = "audit_public_api_surface")]
    public List<ArchitecturePublicApiSurfaceContract> AuditPublicApiSurface { get; set; } = new();

    [YamlMember(Alias = "strict_coverage")]
    public List<ArchitectureCoverageContract> StrictCoverage { get; set; } = new();

    [YamlMember(Alias = "audit_coverage")]
    public List<ArchitectureCoverageContract> AuditCoverage { get; set; } = new();

    public IEnumerable<IArchitectureContract> AllStrict =>
        ArchitectureContractFamilyBindings.All
            .Where(binding => binding.IncludeInContractEnumeration)
            .SelectMany(binding => binding.Strict(this));

    public IEnumerable<IArchitectureContract> AllAudit =>
        ArchitectureContractFamilyBindings.All
            .Where(binding => binding.IncludeInContractEnumeration)
            .SelectMany(binding => binding.Audit(this));
}
