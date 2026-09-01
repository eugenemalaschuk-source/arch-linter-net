using System.Collections;
using System.Globalization;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.PolicyContext;

// This is deliberately a typed switch, rather than a reflection-based property sweep. Contract
// families carry different executable shapes (for example, ordered template layers and typed
// composition selectors), so an explicit projection makes newly added policy semantics reviewable.
internal static class ArchitecturePolicyContextContractFactsProjector
{
    private const string SourceFact = "source";
    private const string ForbiddenFact = "forbidden";
    private const string AllowedTypesFact = "allowed_types";
    private const string DependencyDepthFact = "dependency_depth";
    private const string AllowedFact = "allowed";
    private const string NamespaceFact = "namespace";
    private const string LayerFact = "layer";
    private const string PropertyFact = "property";

    internal static IReadOnlyCollection<Type> SupportedContractTypes { get; } =
    [
        typeof(ArchitectureDependencyContract), typeof(ArchitectureLayerContract),
        typeof(ArchitectureLayerTemplateContract), typeof(ArchitectureAllowOnlyContract),
        typeof(ArchitectureCycleContract), typeof(ArchitectureMethodBodyContract), typeof(ArchitectureAsmdefContract),
        typeof(ArchitectureIndependenceContract), typeof(ArchitectureAssemblyIndependenceContract),
        typeof(ArchitectureAssemblyDependencyContract), typeof(ArchitectureAssemblyAllowOnlyContract),
        typeof(ArchitecturePackageDependencyContract), typeof(ArchitecturePackageAllowOnlyContract),
        typeof(ArchitectureFrameworkReferenceContract), typeof(ArchitectureFrameworkReferenceAllowOnlyContract),
        typeof(ArchitectureProjectMetadataContract), typeof(ArchitectureProtectedContract),
        typeof(ArchitectureExternalDependencyContract), typeof(ArchitectureExternalAllowOnlyContract),
        typeof(ArchitectureAcyclicSiblingContract), typeof(ArchitectureModuleContainerContract),
        typeof(ArchitectureTypePlacementContract), typeof(ArchitectureLayoutConventionContract),
        typeof(ArchitecturePublicApiSurfaceContract), typeof(ArchitectureAttributeUsageContract),
        typeof(ArchitectureContractSurfaceExposureContract),
        typeof(ArchitectureVersionedContractSurfaceIsolationContract),
        typeof(ArchitectureInheritanceContract), typeof(ArchitectureInterfaceImplementationContract),
        typeof(ArchitectureCompositionContract), typeof(ArchitectureContextDependencyContract),
        typeof(ArchitectureContextAllowOnlyContract), typeof(ArchitectureCoverageContract),
        typeof(ArchitectureMetricBudgetContract),
        typeof(ArchitecturePortBoundaryContract),
    ];

    internal static ArchitecturePolicyContextContractProjection Project(IArchitectureContract contract) => contract switch
    {
        ArchitectureDependencyContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Text(SourceFact, value.Source), Values(ForbiddenFact, value.Forbidden), Values(AllowedTypesFact, value.AllowedTypes),
            Flag("forbidden_legacy_runtime", value.ForbiddenLegacyRuntime), EnumValue(DependencyDepthFact, value.DependencyDepth))),
        ArchitectureLayerContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            OrderedLayers(value.Layers, value.OptionalLayers), Text("template_name", value.TemplateName),
            Text("template_owner_id", value.TemplateOwnerId), Text("container_namespace", value.ContainerNamespace),
            Flag("exhaustive", value.Exhaustive))),
        ArchitectureLayerTemplateContract value => Create(value.Reason, Array.Empty<ArchitectureIgnoredViolation>(), Facts(
            Values("containers", value.Containers), Values("exclude_containers", value.ExcludeContainers),
            OrderedLayers(value.Layers), Flag("exhaustive", value.Exhaustive))),
        ArchitectureAllowOnlyContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Text(SourceFact, value.Source), Values(AllowedFact, value.Allowed), Values(AllowedTypesFact, value.AllowedTypes))),
        ArchitectureCycleContract value => Create(value.Reason, value.IgnoredViolations, Facts(Values("layers", value.Layers))),
        ArchitectureMethodBodyContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Text(SourceFact, value.Source), Values("forbidden_calls", value.ForbiddenCalls))),
        ArchitectureAsmdefContract value => Create(value.Reason, Array.Empty<ArchitectureIgnoredViolation>(), Facts(
            Values("source_assemblies", value.SourceAssemblies), Flag("forbidden_editor_refs", value.ForbiddenEditorRefs),
            Values("forbidden_asmdef_prefixes", value.ForbiddenAsmdefPrefixes))),
        ArchitectureIndependenceContract value => Create(value.Reason, value.IgnoredViolations, Facts(Values("layers", value.Layers))),
        ArchitectureAssemblyIndependenceContract value => Create(value.Reason, value.IgnoredViolations, Facts(Values("assemblies", value.Assemblies))),
        ArchitectureAssemblyDependencyContract value => Source(value, Values(ForbiddenFact, value.Forbidden), EnumValue(DependencyDepthFact, value.DependencyDepth)),
        ArchitectureAssemblyAllowOnlyContract value => Source(value, Values(AllowedFact, value.Allowed), EnumValue(DependencyDepthFact, value.DependencyDepth)),
        ArchitecturePackageDependencyContract value => Source(value, Values(ForbiddenFact, value.Forbidden), EnumValue(DependencyDepthFact, value.DependencyDepth)),
        ArchitecturePackageAllowOnlyContract value => Source(value, Values(AllowedFact, value.Allowed), EnumValue(DependencyDepthFact, value.DependencyDepth)),
        ArchitectureFrameworkReferenceContract value => Source(value, Values(ForbiddenFact, value.Forbidden)),
        ArchitectureFrameworkReferenceAllowOnlyContract value => Source(value, Values(AllowedFact, value.Allowed)),
        ArchitectureProjectMetadataContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Values("projects", value.Projects), Values("project_sets", value.ProjectSets), Map("required_properties", value.RequiredProperties),
            Map("forbidden_properties", value.ForbiddenProperties), Values("allowed_friend_assemblies", value.AllowedFriendAssemblies),
            Values("forbidden_project_references", value.ForbiddenProjectReferences))),
        ArchitectureProtectedContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Values("protected", value.Protected), Values("allowed_importers", value.AllowedImporters), Values(AllowedTypesFact, value.AllowedTypes))),
        ArchitectureExternalDependencyContract value => Source(value, Values(ForbiddenFact, value.Forbidden)),
        ArchitectureExternalAllowOnlyContract value => Source(value, Values(AllowedFact, value.Allowed), Values(AllowedTypesFact, value.AllowedTypes)),
        ArchitectureAcyclicSiblingContract value => Create(value.Reason, value.IgnoredViolations, Facts(Values("ancestors", value.Ancestors))),
        ArchitectureModuleContainerContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Text("container", value.Container), Text("profile", value.Profile),
            Values("allowed_container_root_types", value.AllowedContainerRootTypes),
            Values("allowed_module_root_types", value.AllowedModuleRootTypes))),
        ArchitectureTypePlacementContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Object("types_matching", TypeMatcherFacts(value.TypesMatching)),
            Objects("exclude_types_matching", value.ExcludeTypesMatching.Select(item => Object("type_matcher", TypeMatcherFacts(item)))),
            Values("must_reside_in_layers", value.MustResideInLayers), Values("must_reside_in_namespaces", value.MustResideInNamespaces),
            Values("must_reside_in_projects", value.MustResideInProjects), Values("must_reside_in_assemblies", value.MustResideInAssemblies),
            Text("required_name_suffix", value.RequiredNameSuffix), Text("required_name_prefix", value.RequiredNamePrefix),
            Text("forbidden_name_suffix", value.ForbiddenNameSuffix), Text("forbidden_name_prefix", value.ForbiddenNamePrefix))),
        ArchitectureLayoutConventionContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Object("files_matching", LayoutMatcherFacts(value.FilesMatching)),
            Objects("exclude_files_matching", value.ExcludeFilesMatching.Select(item => Object("file_matcher", LayoutMatcherFacts(item)))),
            Text("require_type_kind", value.RequireTypeKind), Text("forbid_type_kind", value.ForbidTypeKind),
            Text("required_name_suffix", value.RequiredNameSuffix), Text("required_name_prefix", value.RequiredNamePrefix),
            Text("forbidden_name_suffix", value.ForbiddenNameSuffix), Text("forbidden_name_prefix", value.ForbiddenNamePrefix),
            Flag("require_type_name_matches_file_name", value.RequireTypeNameMatchesFileName), Number("max_declarations_per_type", value.MaxDeclarationsPerType),
            ObjectFromItems("require_matching_interface", Text("name_prefix", value.RequireMatchingInterface?.NamePrefix)),
            ObjectFromItems("all_declarations", Values("allowed_type_kinds", value.AllDeclarations?.AllowedTypeKinds),
                Values("allowed_roles", value.AllDeclarations?.AllowedRoles), Flag("require_abstract_classes", value.AllDeclarations?.RequireAbstractClasses)))),
        ArchitecturePublicApiSurfaceContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Values("assemblies", value.Assemblies), Values("declared_api", value.DeclaredApi),
            Object("surface_selector", PublicApiSelectorFacts(value.SurfaceSelector)), Text("api_snapshot", value.ApiSnapshot),
            Text("api_comparison", value.ApiComparison), Flag("forbid_public_constants_unless_declared", value.ForbidPublicConstantsUnlessDeclared),
            Values("allowed_public_constants", value.AllowedPublicConstants),
            Objects("resolved_snapshot_entries", value.ResolvedSnapshotEntries.Select(entry => ObjectFromItems("entry",
                Text("assembly", entry.AssemblyName), Text("signature", entry.Signature)))))),
        ArchitectureContractSurfaceExposureContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            ObjectFromItems("source",
                Values("assemblies", value.Source.Assemblies), Values("projects", value.Source.Projects),
                Object("types_matching", PublicApiSelectorFacts(value.Source.TypesMatching)),
                Text("public_api_surface", value.Source.PublicApiSurface)),
            Objects(ForbiddenFact, value.Forbidden.Select(selector =>
                Object("selector", PublicApiSelectorFacts(selector)))))),
        ArchitectureVersionedContractSurfaceIsolationContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Objects("surfaces", value.Surfaces.Select(surface => ObjectFromItems("surface",
                Text("id", surface.Id), Object("types_matching", PublicApiSelectorFacts(surface.TypesMatching))))),
            Text("source_surface", value.SourceSurface), Values("forbidden_surfaces", value.ForbiddenSurfaces))),
        ArchitectureAttributeUsageContract value => Create(value.Reason, value.IgnoredViolations, Scoped(value.Attributes, value.AttributePrefixes,
            new ScopeRestrictions(
                new ScopeRestrictionSet(value.AllowedOnlyInLayers, value.AllowedOnlyInNamespaces, value.AllowedOnlyInProjects, value.AllowedOnlyInAssemblies),
                new ScopeRestrictionSet(value.ForbiddenInLayers, value.ForbiddenInNamespaces, value.ForbiddenInProjects, value.ForbiddenInAssemblies)),
            "attributes", "attribute_prefixes")),
        ArchitectureInheritanceContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Values("source_layers", value.SourceLayers), Values("source_namespaces", value.SourceNamespaces),
            Values("forbidden_base_types", value.ForbiddenBaseTypes), Values("forbidden_base_type_prefixes", value.ForbiddenBaseTypePrefixes))),
        ArchitectureInterfaceImplementationContract value => Create(value.Reason, value.IgnoredViolations, Scoped(value.Interfaces, value.InterfacePrefixes,
            new ScopeRestrictions(
                new ScopeRestrictionSet(value.AllowedOnlyInLayers, value.AllowedOnlyInNamespaces, value.AllowedOnlyInProjects, value.AllowedOnlyInAssemblies),
                new ScopeRestrictionSet(value.ForbiddenInLayers, value.ForbiddenInNamespaces, value.ForbiddenInProjects, value.ForbiddenInAssemblies)),
            "interfaces", "interface_prefixes")),
        ArchitectureCompositionContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Values("forbidden_apis", value.ForbiddenApis), Values("allowed_only_in_layers", value.AllowedOnlyInLayers),
            Values("allowed_only_in_namespaces", value.AllowedOnlyInNamespaces), Values("allowed_only_in_projects", value.AllowedOnlyInProjects),
            Values("allowed_only_in_assemblies", value.AllowedOnlyInAssemblies), Values("allowed_only_in_assembly_sets", value.AllowedOnlyInAssemblySets),
            Objects("allowed_only_in_types", value.AllowedOnlyInTypes.Select(item => ObjectFromItems("type",
                Text("assembly", item.Assembly), Text("type", item.Type)))))),
        ArchitectureContextDependencyContract value => Create(value.Reason, value.IgnoredViolations, Array.Empty<ArchitecturePolicyContextContractFact>()),
        ArchitectureContextAllowOnlyContract value => Create(value.Reason, value.IgnoredViolations, Array.Empty<ArchitecturePolicyContextContractFact>()),
        ArchitectureCoverageContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            Text("scope", value.Scope),
            Objects("roots", value.Roots.Select(item => ObjectFromItems("root", Text(NamespaceFact, item.Namespace),
                Text("namespace_suffix", item.NamespaceSuffix), Values("include", item.Include), Values("exclude", item.Exclude)))),
            Objects("between", value.Between.Select(item => ObjectFromItems("boundary", Values("layers", item)))),
            Values("contract_ids", value.ContractIds),
            Objects("exclude", value.Exclude.Select(item => ObjectFromItems("exclusion", Text(NamespaceFact, item.Namespace),
                Text("namespace_suffix", item.NamespaceSuffix), Text("project", item.Project), Text("assembly", item.Assembly),
                Text("contract_id", item.ContractId), Text("role", item.Role), Map("metadata", item.Metadata), Values("between", item.Between), Text("reason", item.Reason)))),
            Objects("optional_inputs", value.OptionalInputs.Select(item => ObjectFromItems("input", Text("contract_id", item.ContractId),
                Text("input", item.Input), Text(LayerFact, item.Layer), Text("reason", item.Reason)))))),
        ArchitectureMetricBudgetContract value => Create(string.Empty, value.IgnoredViolations, Facts(
            Text("metric", value.Metric), Number("minimum", value.Minimum), Number("maximum", value.Maximum),
            Text("baseline_mode", value.BaselineMode), Number("max_delta", value.MaxDelta))),
        ArchitecturePortBoundaryContract value => Create(value.Reason, value.IgnoredViolations, Facts(
            ObjectFromItems("target_context", Map("metadata", value.TargetContext.Metadata)))),
        _ => throw new InvalidOperationException($"Policy-context projection does not support contract type '{contract.GetType().FullName}'."),
    };

    private static ArchitecturePolicyContextContractProjection Source(ArchitectureSourceExpandableContractBase contract, params ArchitecturePolicyContextContractFact?[] additional)
    {
        List<ArchitecturePolicyContextContractFact?> facts =
        [
            Text(SourceFact, contract.Source), Values("sources", contract.Sources), Values("source_sets", contract.SourceSets),
            Values("exclude_sources", contract.ExcludedSources), Values("exclude_source_sets", contract.ExcludedSourceSets),
        ];
        facts.AddRange(additional);
        return Create(contract.Reason, contract.IgnoredViolations, Facts(facts));
    }

    // Plain (non-record) structs: these exist only to bundle constructor arguments for Scoped
    // below, so they deliberately carry no synthesized equality/ToString/Deconstruct members.
    private readonly struct ScopeRestrictionSet(
        IEnumerable<string> layers,
        IEnumerable<string> namespaces,
        IEnumerable<string> projects,
        IEnumerable<string> assemblies)
    {
        public IEnumerable<string> Layers { get; } = layers;

        public IEnumerable<string> Namespaces { get; } = namespaces;

        public IEnumerable<string> Projects { get; } = projects;

        public IEnumerable<string> Assemblies { get; } = assemblies;
    }

    private readonly struct ScopeRestrictions(ScopeRestrictionSet allowed, ScopeRestrictionSet forbidden)
    {
        public ScopeRestrictionSet Allowed { get; } = allowed;

        public ScopeRestrictionSet Forbidden { get; } = forbidden;
    }

    private static ArchitecturePolicyContextContractFact[] Scoped(
        IEnumerable<string> subjects,
        IEnumerable<string> subjectPrefixes,
        ScopeRestrictions restrictions,
        string subjectsName,
        string prefixesName) => Facts(
        Values(subjectsName, subjects), Values(prefixesName, subjectPrefixes), Values("allowed_only_in_layers", restrictions.Allowed.Layers),
        Values("allowed_only_in_namespaces", restrictions.Allowed.Namespaces), Values("allowed_only_in_projects", restrictions.Allowed.Projects),
        Values("allowed_only_in_assemblies", restrictions.Allowed.Assemblies), Values("forbidden_in_layers", restrictions.Forbidden.Layers),
        Values("forbidden_in_namespaces", restrictions.Forbidden.Namespaces), Values("forbidden_in_projects", restrictions.Forbidden.Projects),
        Values("forbidden_in_assemblies", restrictions.Forbidden.Assemblies));

    private static ArchitecturePolicyContextContractProjection Create(
        string reason,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        IReadOnlyList<ArchitecturePolicyContextContractFact> facts) => new(reason, ignoredViolations, facts);

    private static ArchitecturePolicyContextContractFact[] TypeMatcherFacts(ArchitectureTypeMatcher matcher) => Facts(
        Text("name_suffix", matcher.NameSuffix), Text("name_prefix", matcher.NamePrefix), Text(NamespaceFact, matcher.Namespace),
        Text(LayerFact, matcher.Layer), Text("base_type", matcher.BaseType), Text("implements_interface", matcher.ImplementsInterface),
        Text("has_attribute", matcher.HasAttribute));

    private static ArchitecturePolicyContextContractFact[] PublicApiSelectorFacts(ArchitecturePublicApiSurfaceSelector? selector) => selector is null
        ? Array.Empty<ArchitecturePolicyContextContractFact>()
        : Facts(Text("name_suffix", selector.NameSuffix), Text("name_prefix", selector.NamePrefix), Text(NamespaceFact, selector.Namespace),
            Text(LayerFact, selector.Layer), Text("base_type", selector.BaseType), Text("implements_interface", selector.ImplementsInterface),
            Text("has_attribute", selector.HasAttribute), Text("role", selector.Role));

    private static ArchitecturePolicyContextContractFact[] LayoutMatcherFacts(ArchitectureLayoutFileMatcher matcher) => Facts(
        Text("folder_segment", matcher.FolderSegment), Text("namespace_segment", matcher.NamespaceSegment),
        Text("file_name_suffix", matcher.FileNameSuffix), Text("file_name_prefix", matcher.FileNamePrefix), Text("when", matcher.When));

    private static ArchitecturePolicyContextContractFact? OrderedLayers(IEnumerable<string> layers, HashSet<string>? optionalLayers = null) => Objects(
        "layers", layers.Select(layer => ObjectFromItems(LayerFact, Text("name", layer), Flag("optional", optionalLayers?.Contains(layer) ?? false))));

    private static ArchitecturePolicyContextContractFact? OrderedLayers(IEnumerable<ArchitectureTemplateLayer> layers) => Objects(
        "layers", layers.Select(layer => ObjectFromItems(LayerFact, Text("name", layer.Name), Flag("optional", layer.Optional))));

    private static ArchitecturePolicyContextContractFact? Text(string name, string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : new ArchitecturePolicyContextContractFact(name, [value], Array.Empty<ArchitecturePolicyContextContractFact>());

    private static ArchitecturePolicyContextContractFact? EnumValue<T>(string name, T value) where T : struct, Enum =>
        Text(name, value.ToString().ToLowerInvariant());

    private static ArchitecturePolicyContextContractFact? Number(string name, int? value) => value is null
        ? null
        : Text(name, value.Value.ToString(CultureInfo.InvariantCulture));

    private static ArchitecturePolicyContextContractFact? Flag(string name, bool? value)
    {
        if (value is null)
        {
            return null;
        }

        return Text(name, value.Value ? "true" : "false");
    }

    private static ArchitecturePolicyContextContractFact? Values(string name, IEnumerable<string>? values)
    {
        string[] projected = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
        return projected.Length == 0 ? null : new ArchitecturePolicyContextContractFact(name, projected, Array.Empty<ArchitecturePolicyContextContractFact>());
    }

    private static ArchitecturePolicyContextContractFact? Map(string name, IReadOnlyDictionary<string, string> values) => Objects(
        name, values.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => ObjectFromItems(PropertyFact, Text("name", item.Key), Text("value", item.Value))));

    private static ArchitecturePolicyContextContractFact? Map(string name, IReadOnlyDictionary<string, object> values) => Objects(
        name, values.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => ObjectFromItems(PropertyFact, Text("name", item.Key), Text("value", Display(item.Value)))));

    private static ArchitecturePolicyContextContractFact? Object(string name, ArchitecturePolicyContextContractFact[] items) =>
        items.Length == 0 ? null : new ArchitecturePolicyContextContractFact(name, Array.Empty<string>(), items);

    private static ArchitecturePolicyContextContractFact? ObjectFromItems(string name, params ArchitecturePolicyContextContractFact?[] items) =>
        Object(name, Facts(items));

    private static ArchitecturePolicyContextContractFact? Objects(string name, IEnumerable<ArchitecturePolicyContextContractFact?> items) =>
        Object(name, items.Where(item => item is not null).Cast<ArchitecturePolicyContextContractFact>().ToArray());

    private static ArchitecturePolicyContextContractFact[] Facts(IEnumerable<ArchitecturePolicyContextContractFact?> facts) =>
        facts.Where(fact => fact is not null).Cast<ArchitecturePolicyContextContractFact>().ToArray();

    private static ArchitecturePolicyContextContractFact[] Facts(params ArchitecturePolicyContextContractFact?[] facts) => Facts((IEnumerable<ArchitecturePolicyContextContractFact?>)facts);

    private static string Display(object? value) => value switch
    {
        null => "null",
        string text => text,
        IEnumerable values => "[" + string.Join(", ", values.Cast<object?>().Select(Display)) + "]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

internal sealed record ArchitecturePolicyContextContractProjection(
    string Reason,
    IReadOnlyList<ArchitectureIgnoredViolation> IgnoredViolations,
    IReadOnlyList<ArchitecturePolicyContextContractFact> Facts);
