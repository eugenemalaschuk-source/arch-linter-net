namespace ArchLinterNet.Core.Contracts;

// Single source of truth, within Contracts, for which contract families exist and how to read
// their strict/audit lists off ArchitectureContractGroups. Order matches the historical
// ArchitectureContractGroups.EnumerateStrict/EnumerateAudit and DuplicateIdValidator group order
// exactly, so consumers filtering/iterating this list reproduce prior behavior unchanged:
// - ArchitectureContractGroups.AllStrict/AllAudit filter to IncludeInContractEnumeration == true
//   (excludes layer_template, whose raw contracts are expanded into real layer contracts
//   elsewhere and are never themselves catalog-eligible).
// - DuplicateIdValidator iterates every entry, including layer_template.
internal static class ArchitectureContractFamilyBindings
{
    internal static readonly IReadOnlyList<ArchitectureContractFamilyBinding> All = new[]
    {
        new ArchitectureContractFamilyBinding("dependency", g => g.Strict, g => g.Audit, true),
        new ArchitectureContractFamilyBinding("layer", g => g.StrictLayers, g => g.AuditLayers, true),
        new ArchitectureContractFamilyBinding("allow_only", g => g.StrictAllowOnly, g => g.AuditAllowOnly, true),
        new ArchitectureContractFamilyBinding("cycle", g => g.StrictCycles, g => g.AuditCycles, true),
        new ArchitectureContractFamilyBinding("method_body", g => g.StrictMethodBody, g => g.AuditMethodBody, true),
        new ArchitectureContractFamilyBinding("asmdef", g => g.StrictAsmdef, g => g.AuditAsmdef, true),
        new ArchitectureContractFamilyBinding("independence", g => g.StrictIndependence, g => g.AuditIndependence, true),
        new ArchitectureContractFamilyBinding("assembly_independence", g => g.StrictAssemblyIndependence,
            g => g.AuditAssemblyIndependence, true),
        new ArchitectureContractFamilyBinding("assembly_dependency", g => g.StrictAssemblyDependency,
            g => g.AuditAssemblyDependency, true),
        new ArchitectureContractFamilyBinding("assembly_allow_only", g => g.StrictAssemblyAllowOnly,
            g => g.AuditAssemblyAllowOnly, true),
        new ArchitectureContractFamilyBinding("package_dependency", g => g.StrictPackageDependency,
            g => g.AuditPackageDependency, true),
        new ArchitectureContractFamilyBinding("package_allow_only", g => g.StrictPackageAllowOnly,
            g => g.AuditPackageAllowOnly, true),
        new ArchitectureContractFamilyBinding("framework_dependency", g => g.StrictFrameworkDependency,
            g => g.AuditFrameworkDependency, true),
        new ArchitectureContractFamilyBinding("framework_allow_only", g => g.StrictFrameworkAllowOnly,
            g => g.AuditFrameworkAllowOnly, true),
        new ArchitectureContractFamilyBinding("project_metadata", g => g.StrictProjectMetadata,
            g => g.AuditProjectMetadata, true),
        new ArchitectureContractFamilyBinding("protected", g => g.StrictProtected, g => g.AuditProtected, true),
        new ArchitectureContractFamilyBinding("external", g => g.StrictExternal, g => g.AuditExternal, true),
        new ArchitectureContractFamilyBinding("external_allow_only", g => g.StrictExternalAllowOnly,
            g => g.AuditExternalAllowOnly, true),
        new ArchitectureContractFamilyBinding("layer_template", g => g.StrictLayerTemplates,
            g => g.AuditLayerTemplates, false),
        new ArchitectureContractFamilyBinding("acyclic_sibling", g => g.StrictAcyclicSiblings,
            g => g.AuditAcyclicSiblings, true),
        new ArchitectureContractFamilyBinding("type_placement", g => g.StrictTypePlacement,
            g => g.AuditTypePlacement, true),
        new ArchitectureContractFamilyBinding("layout_conventions", g => g.StrictLayoutConventions,
            g => g.AuditLayoutConventions, true),
        new ArchitectureContractFamilyBinding("public_api_surface", g => g.StrictPublicApiSurface,
            g => g.AuditPublicApiSurface, true),
        new ArchitectureContractFamilyBinding("attribute_usage", g => g.StrictAttributeUsage,
            g => g.AuditAttributeUsage, true),
        new ArchitectureContractFamilyBinding("inheritance", g => g.StrictInheritance, g => g.AuditInheritance, true),
        new ArchitectureContractFamilyBinding("interface_implementation", g => g.StrictInterfaceImplementation,
            g => g.AuditInterfaceImplementation, true),
        new ArchitectureContractFamilyBinding("composition", g => g.StrictComposition, g => g.AuditComposition, true),
        new ArchitectureContractFamilyBinding("coverage", g => g.StrictCoverage, g => g.AuditCoverage, true),
        new ArchitectureContractFamilyBinding("context_dependency", g => g.StrictContextDependencies,
            g => g.AuditContextDependencies, true),
        new ArchitectureContractFamilyBinding("context_allow_only", g => g.StrictContextAllowOnly,
            g => g.AuditContextAllowOnly, true),
        new ArchitectureContractFamilyBinding("port_boundary", g => g.StrictPortBoundaries,
            g => g.AuditPortBoundaries, true),
    };
}
