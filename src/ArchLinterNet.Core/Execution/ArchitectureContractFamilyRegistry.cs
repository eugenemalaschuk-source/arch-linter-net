using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;

namespace ArchLinterNet.Core.Execution;

// Single ordered source of truth for every contract family's catalog metadata AND checker
// behavior. The order here is observable: ArchitectureContractCatalog.Build iterates this list to
// populate FamiliesInOrder, which determines ArchitectureContractExecutor's dispatch order and
// therefore violation/cycle insertion order in ValidationOutcome (and JSON output) and --timings
// entry order. This mirrors the AddGroup call order ArchitectureContractCatalog.Build used before
// this registry existed, so migrating onto it does not reorder observable output (pinned by
// ArchitectureContractFamilyRegistryTests and ArchitectureContractCatalogTests).
//
// Adding a new contract family means appending one descriptor here (catalog metadata plus its
// Checker delegate) instead of hand-editing ArchitectureContractCatalog.Build, adding a handler
// class, and adding a DI registration line (see #211).
internal static class ArchitectureContractFamilyRegistry
{
    public static IReadOnlyList<ArchitectureContractFamilyDescriptor> All { get; } = new List<ArchitectureContractFamilyDescriptor>
    {
        new(
            "dependency", "strict", "audit", true,
            g => g.Strict, g => g.Audit,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckContract((ArchitectureDependencyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureDependencyContract) },
        },
        new(
            "layer", "strict_layers", "audit_layers", true,
            g => g.StrictLayers, g => g.AuditLayers,
            CheckLayerContract)
        {
            OwnedContractTypes = new[] { typeof(ArchitectureLayerContract) },
        },
        new(
            "layer_template", "strict_layer_templates", "audit_layer_templates", false,
            g => LayerTemplateExpander.Expand(g.StrictLayerTemplates),
            g => LayerTemplateExpander.Expand(g.AuditLayerTemplates),
            CheckLayerContract)
        {
            OwnedContractTypes = new[] { typeof(ArchitectureLayerTemplateContract) },
        },
        new(
            "allow_only", "strict_allow_only", "audit_allow_only", true,
            g => g.StrictAllowOnly, g => g.AuditAllowOnly,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAllowOnlyContract((ArchitectureAllowOnlyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAllowOnlyContract) },
        },
        new(
            "cycle", "strict_cycles", "audit_cycles", true,
            g => g.StrictCycles, g => g.AuditCycles,
            (session, contract) =>
            {
                var cycleContract = (ArchitectureCycleContract)contract;
                IReadOnlyCollection<string> cycles = session.CheckCycleContract(cycleContract);
                string idPrefix = cycleContract.Id != null ? $"[{cycleContract.Id}] " : string.Empty;
                return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
            })
        {
            OwnedContractTypes = new[] { typeof(ArchitectureCycleContract) },
        },
        new(
            "method_body", "strict_method_body", "audit_method_body", true,
            g => g.StrictMethodBody, g => g.AuditMethodBody,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckMethodBodyContract((ArchitectureMethodBodyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureMethodBodyContract) },
        },
        new(
            "asmdef", "strict_asmdef", "audit_asmdef", false,
            g => g.StrictAsmdef, g => g.AuditAsmdef,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAsmdefContract((ArchitectureAsmdefContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAsmdefContract) },
        },
        new(
            "independence", "strict_independence", "audit_independence", true,
            g => g.StrictIndependence, g => g.AuditIndependence,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckIndependenceContract((ArchitectureIndependenceContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureIndependenceContract) },
        },
        new(
            "assembly_independence", "strict_assembly_independence", "audit_assembly_independence", true,
            g => g.StrictAssemblyIndependence, g => g.AuditAssemblyIndependence,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAssemblyIndependenceContract((ArchitectureAssemblyIndependenceContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAssemblyIndependenceContract) },
        },
        new(
            "assembly_dependency", "strict_assembly_dependency", "audit_assembly_dependency", true,
            g => g.StrictAssemblyDependency, g => g.AuditAssemblyDependency,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAssemblyDependencyContract((ArchitectureAssemblyDependencyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAssemblyDependencyContract) },
        },
        new(
            "assembly_allow_only", "strict_assembly_allow_only", "audit_assembly_allow_only", true,
            g => g.StrictAssemblyAllowOnly, g => g.AuditAssemblyAllowOnly,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAssemblyAllowOnlyContract((ArchitectureAssemblyAllowOnlyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAssemblyAllowOnlyContract) },
        },
        new(
            "package_dependency", "strict_package_dependency", "audit_package_dependency", true,
            g => g.StrictPackageDependency, g => g.AuditPackageDependency,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckPackageDependencyContract((ArchitecturePackageDependencyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitecturePackageDependencyContract) },
        },
        new(
            "package_allow_only", "strict_package_allow_only", "audit_package_allow_only", true,
            g => g.StrictPackageAllowOnly, g => g.AuditPackageAllowOnly,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckPackageAllowOnlyContract((ArchitecturePackageAllowOnlyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitecturePackageAllowOnlyContract) },
        },
        new(
            "project_metadata", "strict_project_metadata", "audit_project_metadata", true,
            g => g.StrictProjectMetadata, g => g.AuditProjectMetadata,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckProjectMetadataContract((ArchitectureProjectMetadataContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureProjectMetadataContract) },
        },
        new(
            "protected", "strict_protected", "audit_protected", true,
            g => g.StrictProtected, g => g.AuditProtected,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckProtectedContract((ArchitectureProtectedContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureProtectedContract) },
        },
        new(
            "external", "strict_external", "audit_external", true,
            g => g.StrictExternal, g => g.AuditExternal,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckExternalContract((ArchitectureExternalDependencyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureExternalDependencyContract) },
        },
        new(
            "external_allow_only", "strict_external_allow_only", "audit_external_allow_only", true,
            g => g.StrictExternalAllowOnly, g => g.AuditExternalAllowOnly,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckExternalAllowOnlyContract((ArchitectureExternalAllowOnlyContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureExternalAllowOnlyContract) },
        },
        new(
            "acyclic_sibling", "strict_acyclic_siblings", "audit_acyclic_siblings", true,
            g => g.StrictAcyclicSiblings, g => g.AuditAcyclicSiblings,
            (session, contract) =>
            {
                var acyclicSiblingContract = (ArchitectureAcyclicSiblingContract)contract;
                IReadOnlyCollection<string> cycles = session.CheckAcyclicSiblingContract(acyclicSiblingContract);
                string idPrefix = acyclicSiblingContract.Id != null ? $"[{acyclicSiblingContract.Id}] " : string.Empty;
                return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
            })
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAcyclicSiblingContract) },
        },
        new(
            "type_placement", "strict_type_placement", "audit_type_placement", true,
            g => g.StrictTypePlacement, g => g.AuditTypePlacement,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckTypePlacementContract((ArchitectureTypePlacementContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureTypePlacementContract) },
        },
        new(
            "public_api_surface", "strict_public_api_surface", "audit_public_api_surface", true,
            g => g.StrictPublicApiSurface, g => g.AuditPublicApiSurface,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckPublicApiSurfaceContract((ArchitecturePublicApiSurfaceContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitecturePublicApiSurfaceContract) },
        },
        new(
            "attribute_usage", "strict_attribute_usage", "audit_attribute_usage", true,
            g => g.StrictAttributeUsage, g => g.AuditAttributeUsage,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckAttributeUsageContract((ArchitectureAttributeUsageContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureAttributeUsageContract) },
        },
        new(
            "inheritance", "strict_inheritance", "audit_inheritance", true,
            g => g.StrictInheritance, g => g.AuditInheritance,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckInheritanceContract((ArchitectureInheritanceContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureInheritanceContract) },
        },
        new(
            "interface_implementation", "strict_interface_implementation", "audit_interface_implementation", true,
            g => g.StrictInterfaceImplementation, g => g.AuditInterfaceImplementation,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckInterfaceImplementationContract((ArchitectureInterfaceImplementationContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureInterfaceImplementationContract) },
        },
        new(
            "composition", "strict_composition", "audit_composition", true,
            g => g.StrictComposition, g => g.AuditComposition,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckCompositionContract((ArchitectureCompositionContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureCompositionContract) },
        },
        new(
            "coverage", "strict_coverage", "audit_coverage", true,
            g => g.StrictCoverage, g => g.AuditCoverage,
            (session, contract) => ArchitectureHandlerResult.FromViolations(
                session.CheckCoverageContract((ArchitectureCoverageContract)contract)))
        {
            OwnedContractTypes = new[] { typeof(ArchitectureCoverageContract) },
        },
    };

    // Shared by "layer" and "layer_template": layer_template contracts are expanded into
    // ArchitectureLayerContract instances before execution, so they run through the same checker.
    private static ArchitectureHandlerResult CheckLayerContract(ArchitectureAnalysisSession session, IArchitectureContract contract) =>
        ArchitectureHandlerResult.FromViolations(session.CheckLayerContract((ArchitectureLayerContract)contract));
}
