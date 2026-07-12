using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed record ArchitectureBaselineCandidate(
    string ContractGroup,
    string? ContractId,
    string SourceType,
    string ForbiddenReference);

public sealed class ArchitectureBaselineDocument
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; }

    [YamlMember(Alias = "baseline")]
    public ArchitectureBaselineContractGroups Baseline { get; set; } = new();
}

// Property order below matches ArchitectureContractCatalog.Build's baseline-capable family order
// (every executable group except asmdef and layer_templates, which never produce baseline
// candidates). GroupNames, GetGroup, and SetGroup are the single source of truth the baseline
// generator and comparer read from, so a new baseline-capable group is added in exactly one place.
public sealed class ArchitectureBaselineContractGroups
{
    [YamlMember(Alias = "strict")] public List<ArchitectureBaselineContractEntry> Strict { get; set; } = new();
    [YamlMember(Alias = "audit")] public List<ArchitectureBaselineContractEntry> Audit { get; set; } = new();
    [YamlMember(Alias = "strict_layers")] public List<ArchitectureBaselineContractEntry> StrictLayers { get; set; } = new();
    [YamlMember(Alias = "audit_layers")] public List<ArchitectureBaselineContractEntry> AuditLayers { get; set; } = new();
    [YamlMember(Alias = "strict_allow_only")] public List<ArchitectureBaselineContractEntry> StrictAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_allow_only")] public List<ArchitectureBaselineContractEntry> AuditAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_cycles")] public List<ArchitectureBaselineContractEntry> StrictCycles { get; set; } = new();
    [YamlMember(Alias = "audit_cycles")] public List<ArchitectureBaselineContractEntry> AuditCycles { get; set; } = new();
    [YamlMember(Alias = "strict_method_body")] public List<ArchitectureBaselineContractEntry> StrictMethodBody { get; set; } = new();
    [YamlMember(Alias = "audit_method_body")] public List<ArchitectureBaselineContractEntry> AuditMethodBody { get; set; } = new();
    [YamlMember(Alias = "strict_independence")] public List<ArchitectureBaselineContractEntry> StrictIndependence { get; set; } = new();
    [YamlMember(Alias = "audit_independence")] public List<ArchitectureBaselineContractEntry> AuditIndependence { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_independence")] public List<ArchitectureBaselineContractEntry> StrictAssemblyIndependence { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_independence")] public List<ArchitectureBaselineContractEntry> AuditAssemblyIndependence { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_dependency")] public List<ArchitectureBaselineContractEntry> StrictAssemblyDependency { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_dependency")] public List<ArchitectureBaselineContractEntry> AuditAssemblyDependency { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_allow_only")] public List<ArchitectureBaselineContractEntry> StrictAssemblyAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_allow_only")] public List<ArchitectureBaselineContractEntry> AuditAssemblyAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_package_dependency")] public List<ArchitectureBaselineContractEntry> StrictPackageDependency { get; set; } = new();
    [YamlMember(Alias = "audit_package_dependency")] public List<ArchitectureBaselineContractEntry> AuditPackageDependency { get; set; } = new();
    [YamlMember(Alias = "strict_package_allow_only")] public List<ArchitectureBaselineContractEntry> StrictPackageAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_package_allow_only")] public List<ArchitectureBaselineContractEntry> AuditPackageAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_project_metadata")] public List<ArchitectureBaselineContractEntry> StrictProjectMetadata { get; set; } = new();
    [YamlMember(Alias = "audit_project_metadata")] public List<ArchitectureBaselineContractEntry> AuditProjectMetadata { get; set; } = new();
    [YamlMember(Alias = "strict_protected")] public List<ArchitectureBaselineContractEntry> StrictProtected { get; set; } = new();
    [YamlMember(Alias = "audit_protected")] public List<ArchitectureBaselineContractEntry> AuditProtected { get; set; } = new();
    [YamlMember(Alias = "strict_external")] public List<ArchitectureBaselineContractEntry> StrictExternal { get; set; } = new();
    [YamlMember(Alias = "audit_external")] public List<ArchitectureBaselineContractEntry> AuditExternal { get; set; } = new();
    [YamlMember(Alias = "strict_external_allow_only")] public List<ArchitectureBaselineContractEntry> StrictExternalAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_external_allow_only")] public List<ArchitectureBaselineContractEntry> AuditExternalAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> StrictAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "audit_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> AuditAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "strict_type_placement")] public List<ArchitectureBaselineContractEntry> StrictTypePlacement { get; set; } = new();
    [YamlMember(Alias = "audit_type_placement")] public List<ArchitectureBaselineContractEntry> AuditTypePlacement { get; set; } = new();
    [YamlMember(Alias = "strict_public_api_surface")] public List<ArchitectureBaselineContractEntry> StrictPublicApiSurface { get; set; } = new();
    [YamlMember(Alias = "audit_public_api_surface")] public List<ArchitectureBaselineContractEntry> AuditPublicApiSurface { get; set; } = new();
    [YamlMember(Alias = "strict_attribute_usage")] public List<ArchitectureBaselineContractEntry> StrictAttributeUsage { get; set; } = new();
    [YamlMember(Alias = "audit_attribute_usage")] public List<ArchitectureBaselineContractEntry> AuditAttributeUsage { get; set; } = new();
    [YamlMember(Alias = "strict_inheritance")] public List<ArchitectureBaselineContractEntry> StrictInheritance { get; set; } = new();
    [YamlMember(Alias = "audit_inheritance")] public List<ArchitectureBaselineContractEntry> AuditInheritance { get; set; } = new();
    [YamlMember(Alias = "strict_interface_implementation")] public List<ArchitectureBaselineContractEntry> StrictInterfaceImplementation { get; set; } = new();
    [YamlMember(Alias = "audit_interface_implementation")] public List<ArchitectureBaselineContractEntry> AuditInterfaceImplementation { get; set; } = new();
    [YamlMember(Alias = "strict_composition")] public List<ArchitectureBaselineContractEntry> StrictComposition { get; set; } = new();
    [YamlMember(Alias = "audit_composition")] public List<ArchitectureBaselineContractEntry> AuditComposition { get; set; } = new();
    [YamlMember(Alias = "strict_coverage")] public List<ArchitectureBaselineContractEntry> StrictCoverage { get; set; } = new();
    [YamlMember(Alias = "audit_coverage")] public List<ArchitectureBaselineContractEntry> AuditCoverage { get; set; } = new();
    [YamlMember(Alias = "strict_context_dependencies")] public List<ArchitectureBaselineContractEntry> StrictContextDependencies { get; set; } = new();
    [YamlMember(Alias = "audit_context_dependencies")] public List<ArchitectureBaselineContractEntry> AuditContextDependencies { get; set; } = new();
    [YamlMember(Alias = "strict_context_allow_only")] public List<ArchitectureBaselineContractEntry> StrictContextAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_context_allow_only")] public List<ArchitectureBaselineContractEntry> AuditContextAllowOnly { get; set; } = new();

    // Canonical, ordered set of every baseline-capable group name. The baseline comparer iterates
    // this list; keeping it aligned with the properties above (and with the catalog's resolvable
    // groups, asserted by ArchitectureBaselineGroupCoverageTests) prevents silent drift where an
    // executable group would be classified/serialized by one component but ignored by another.
    public static readonly IReadOnlyList<string> GroupNames = new[]
    {
        "strict", "audit",
        "strict_layers", "audit_layers",
        "strict_allow_only", "audit_allow_only",
        "strict_cycles", "audit_cycles",
        "strict_method_body", "audit_method_body",
        "strict_independence", "audit_independence",
        "strict_assembly_independence", "audit_assembly_independence",
        "strict_assembly_dependency", "audit_assembly_dependency",
        "strict_assembly_allow_only", "audit_assembly_allow_only",
        "strict_package_dependency", "audit_package_dependency",
        "strict_package_allow_only", "audit_package_allow_only",
        "strict_project_metadata", "audit_project_metadata",
        "strict_protected", "audit_protected",
        "strict_external", "audit_external",
        "strict_external_allow_only", "audit_external_allow_only",
        "strict_acyclic_siblings", "audit_acyclic_siblings",
        "strict_type_placement", "audit_type_placement",
        "strict_public_api_surface", "audit_public_api_surface",
        "strict_attribute_usage", "audit_attribute_usage",
        "strict_inheritance", "audit_inheritance",
        "strict_interface_implementation", "audit_interface_implementation",
        "strict_composition", "audit_composition",
        "strict_coverage", "audit_coverage",
        "strict_context_dependencies", "audit_context_dependencies",
        "strict_context_allow_only", "audit_context_allow_only",
    };

    public List<ArchitectureBaselineContractEntry> GetGroup(string groupName)
    {
        return groupName switch
        {
            "strict" => Strict,
            "audit" => Audit,
            "strict_layers" => StrictLayers,
            "audit_layers" => AuditLayers,
            "strict_allow_only" => StrictAllowOnly,
            "audit_allow_only" => AuditAllowOnly,
            "strict_cycles" => StrictCycles,
            "audit_cycles" => AuditCycles,
            "strict_method_body" => StrictMethodBody,
            "audit_method_body" => AuditMethodBody,
            "strict_independence" => StrictIndependence,
            "audit_independence" => AuditIndependence,
            "strict_assembly_independence" => StrictAssemblyIndependence,
            "audit_assembly_independence" => AuditAssemblyIndependence,
            "strict_assembly_dependency" => StrictAssemblyDependency,
            "audit_assembly_dependency" => AuditAssemblyDependency,
            "strict_assembly_allow_only" => StrictAssemblyAllowOnly,
            "audit_assembly_allow_only" => AuditAssemblyAllowOnly,
            "strict_package_dependency" => StrictPackageDependency,
            "audit_package_dependency" => AuditPackageDependency,
            "strict_package_allow_only" => StrictPackageAllowOnly,
            "audit_package_allow_only" => AuditPackageAllowOnly,
            "strict_project_metadata" => StrictProjectMetadata,
            "audit_project_metadata" => AuditProjectMetadata,
            "strict_protected" => StrictProtected,
            "audit_protected" => AuditProtected,
            "strict_external" => StrictExternal,
            "audit_external" => AuditExternal,
            "strict_external_allow_only" => StrictExternalAllowOnly,
            "audit_external_allow_only" => AuditExternalAllowOnly,
            "strict_acyclic_siblings" => StrictAcyclicSiblings,
            "audit_acyclic_siblings" => AuditAcyclicSiblings,
            "strict_type_placement" => StrictTypePlacement,
            "audit_type_placement" => AuditTypePlacement,
            "strict_public_api_surface" => StrictPublicApiSurface,
            "audit_public_api_surface" => AuditPublicApiSurface,
            "strict_attribute_usage" => StrictAttributeUsage,
            "audit_attribute_usage" => AuditAttributeUsage,
            "strict_inheritance" => StrictInheritance,
            "audit_inheritance" => AuditInheritance,
            "strict_interface_implementation" => StrictInterfaceImplementation,
            "audit_interface_implementation" => AuditInterfaceImplementation,
            "strict_composition" => StrictComposition,
            "audit_composition" => AuditComposition,
            "strict_coverage" => StrictCoverage,
            "audit_coverage" => AuditCoverage,
            "strict_context_dependencies" => StrictContextDependencies,
            "audit_context_dependencies" => AuditContextDependencies,
            "strict_context_allow_only" => StrictContextAllowOnly,
            "audit_context_allow_only" => AuditContextAllowOnly,
            _ => throw new ArgumentOutOfRangeException(
                nameof(groupName), groupName, "Unknown baseline group. Add it to ArchitectureBaselineContractGroups."),
        };
    }

    public void SetGroup(string groupName, List<ArchitectureBaselineContractEntry> entries)
    {
        switch (groupName)
        {
            case "strict": Strict = entries; break;
            case "audit": Audit = entries; break;
            case "strict_layers": StrictLayers = entries; break;
            case "audit_layers": AuditLayers = entries; break;
            case "strict_allow_only": StrictAllowOnly = entries; break;
            case "audit_allow_only": AuditAllowOnly = entries; break;
            case "strict_cycles": StrictCycles = entries; break;
            case "audit_cycles": AuditCycles = entries; break;
            case "strict_method_body": StrictMethodBody = entries; break;
            case "audit_method_body": AuditMethodBody = entries; break;
            case "strict_independence": StrictIndependence = entries; break;
            case "audit_independence": AuditIndependence = entries; break;
            case "strict_assembly_independence": StrictAssemblyIndependence = entries; break;
            case "audit_assembly_independence": AuditAssemblyIndependence = entries; break;
            case "strict_assembly_dependency": StrictAssemblyDependency = entries; break;
            case "audit_assembly_dependency": AuditAssemblyDependency = entries; break;
            case "strict_assembly_allow_only": StrictAssemblyAllowOnly = entries; break;
            case "audit_assembly_allow_only": AuditAssemblyAllowOnly = entries; break;
            case "strict_package_dependency": StrictPackageDependency = entries; break;
            case "audit_package_dependency": AuditPackageDependency = entries; break;
            case "strict_package_allow_only": StrictPackageAllowOnly = entries; break;
            case "audit_package_allow_only": AuditPackageAllowOnly = entries; break;
            case "strict_project_metadata": StrictProjectMetadata = entries; break;
            case "audit_project_metadata": AuditProjectMetadata = entries; break;
            case "strict_protected": StrictProtected = entries; break;
            case "audit_protected": AuditProtected = entries; break;
            case "strict_external": StrictExternal = entries; break;
            case "audit_external": AuditExternal = entries; break;
            case "strict_external_allow_only": StrictExternalAllowOnly = entries; break;
            case "audit_external_allow_only": AuditExternalAllowOnly = entries; break;
            case "strict_acyclic_siblings": StrictAcyclicSiblings = entries; break;
            case "audit_acyclic_siblings": AuditAcyclicSiblings = entries; break;
            case "strict_type_placement": StrictTypePlacement = entries; break;
            case "audit_type_placement": AuditTypePlacement = entries; break;
            case "strict_public_api_surface": StrictPublicApiSurface = entries; break;
            case "audit_public_api_surface": AuditPublicApiSurface = entries; break;
            case "strict_attribute_usage": StrictAttributeUsage = entries; break;
            case "audit_attribute_usage": AuditAttributeUsage = entries; break;
            case "strict_inheritance": StrictInheritance = entries; break;
            case "audit_inheritance": AuditInheritance = entries; break;
            case "strict_interface_implementation": StrictInterfaceImplementation = entries; break;
            case "audit_interface_implementation": AuditInterfaceImplementation = entries; break;
            case "strict_composition": StrictComposition = entries; break;
            case "audit_composition": AuditComposition = entries; break;
            case "strict_coverage": StrictCoverage = entries; break;
            case "audit_coverage": AuditCoverage = entries; break;
            case "strict_context_dependencies": StrictContextDependencies = entries; break;
            case "audit_context_dependencies": AuditContextDependencies = entries; break;
            case "strict_context_allow_only": StrictContextAllowOnly = entries; break;
            case "audit_context_allow_only": AuditContextAllowOnly = entries; break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(groupName), groupName, "Unknown baseline group. Add it to ArchitectureBaselineContractGroups.");
        }
    }
}

public sealed class ArchitectureBaselineContractEntry
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
}
