using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitecturePolicyDocumentLoader : IArchitecturePolicyDocumentLoader
{
    private static readonly string[] _implementedCoverageScopes =
        { "namespace", "rule_input", "project", "assembly", "dependency_edge" };

    private readonly IArchitectureFileSystem _fileSystem;

    public ArchitecturePolicyDocumentLoader()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitecturePolicyDocumentLoader(IArchitectureFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public ArchitectureContractDocument Load(string policyPath)
    {
        if (!_fileSystem.FileExists(policyPath))
        {
            throw new FileNotFoundException($"Architecture contract file not found: {policyPath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = _fileSystem.ReadAllText(policyPath);
        ArchitectureContractDocument? document = deserializer.Deserialize<ArchitectureContractDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
        }

        AssignFallbackIds(document);
        ValidateDuplicateIds(document);
        ValidateAcyclicSiblingContracts(document);
        ValidateLayerNamespaces(document);
        ValidateCoverageNamespaces(document);
        ValidateImplementedCoverageScopes(document);
        ValidateAssemblyIndependenceContracts(document);
        ValidateAssemblyDependencyContracts(document);
        ValidateAssemblyAllowOnlyContracts(document);
        ValidatePackageDependencyContracts(document);
        ValidatePackageAllowOnlyContracts(document);
        ValidateProjectMetadataContracts(document);
        ValidateTypePlacementContracts(document);
        ValidatePublicApiSurfaceContracts(document);
        ValidateAttributeUsageContracts(document);
        ValidateInheritanceContracts(document);
        ValidateInterfaceImplementationContracts(document);
        ValidateCompositionContracts(document);

        return document;
    }

    public static string NormalizeToContractId(string name)
    {
        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace(" -> ", "-to-");
        normalized = Regex.Replace(normalized, @"[^a-z0-9-]", "-");
        normalized = Regex.Replace(normalized, "-{2,}", "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    private static void AssignFallbackIds(ArchitectureContractDocument document)
    {
        foreach (IArchitectureContract contract in GetAllContracts(document))
        {
            if (string.IsNullOrEmpty(contract.Id))
            {
                contract.Id = NormalizeToContractId(contract.Name);
            }
        }
    }

    private static void ValidateDuplicateIds(ArchitectureContractDocument document)
    {
        IEnumerable<IArchitectureContract>[] groups =
        [
            document.Contracts.Strict,
            document.Contracts.Audit,
            document.Contracts.StrictLayers,
            document.Contracts.AuditLayers,
            document.Contracts.StrictAllowOnly,
            document.Contracts.AuditAllowOnly,
            document.Contracts.StrictCycles,
            document.Contracts.AuditCycles,
            document.Contracts.StrictMethodBody,
            document.Contracts.AuditMethodBody,
            document.Contracts.StrictAsmdef,
            document.Contracts.AuditAsmdef,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictAssemblyIndependence,
            document.Contracts.AuditAssemblyIndependence,
            document.Contracts.StrictAssemblyDependency,
            document.Contracts.AuditAssemblyDependency,
            document.Contracts.StrictAssemblyAllowOnly,
            document.Contracts.AuditAssemblyAllowOnly,
            document.Contracts.StrictPackageDependency,
            document.Contracts.AuditPackageDependency,
            document.Contracts.StrictPackageAllowOnly,
            document.Contracts.AuditPackageAllowOnly,
            document.Contracts.StrictProjectMetadata,
            document.Contracts.AuditProjectMetadata,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictExternalAllowOnly,
            document.Contracts.AuditExternalAllowOnly,
            document.Contracts.StrictLayerTemplates,
            document.Contracts.AuditLayerTemplates,
            document.Contracts.StrictAcyclicSiblings,
            document.Contracts.AuditAcyclicSiblings,
            document.Contracts.StrictTypePlacement,
            document.Contracts.AuditTypePlacement,
            document.Contracts.StrictPublicApiSurface,
            document.Contracts.AuditPublicApiSurface,
            document.Contracts.StrictAttributeUsage,
            document.Contracts.AuditAttributeUsage,
            document.Contracts.StrictInheritance,
            document.Contracts.AuditInheritance,
            document.Contracts.StrictInterfaceImplementation,
            document.Contracts.AuditInterfaceImplementation,
            document.Contracts.StrictComposition,
            document.Contracts.AuditComposition,
            document.Contracts.StrictCoverage,
            document.Contracts.AuditCoverage,
        ];

        foreach (var group in groups)
        {
            var duplicates = group
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate contract IDs found: {string.Join(", ", duplicates)}. Each contract ID must be unique within its contract type and mode group.");
            }
        }
    }

    private static void ValidateAcyclicSiblingContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureAcyclicSiblingContract contract in document.Contracts.StrictAcyclicSiblings
                     .Concat(document.Contracts.AuditAcyclicSiblings))
        {
            if (contract.Ancestors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Acyclic sibling contract '{contract.Name}' has an empty ancestors list. At least one ancestor namespace is required.");
            }

            for (int i = 0; i < contract.Ancestors.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(contract.Ancestors[i]))
                {
                    throw new InvalidOperationException(
                        $"Acyclic sibling contract '{contract.Name}' has a blank or empty ancestor at index {i}. Each ancestor must be a non-empty namespace prefix.");
                }
            }
        }
    }

    private static void ValidateLayerNamespaces(ArchitectureContractDocument document)
    {
        foreach (KeyValuePair<string, ArchitectureLayer> pair in document.Layers)
        {
            ArchitectureLayer layer = pair.Value;

            if (!string.IsNullOrWhiteSpace(layer.Namespace))
            {
                _ = layer.GlobPattern;
            }
        }
    }

    private static void ValidateAssemblyIndependenceContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureAssemblyIndependenceContract contract in document.Contracts.StrictAssemblyIndependence
                     .Concat(document.Contracts.AuditAssemblyIndependence))
        {
            foreach (string assemblyName in contract.Assemblies)
            {
                if (!targetAssemblies.Contains(assemblyName))
                {
                    throw new InvalidOperationException(
                        $"Assembly independence contract '{contract.Name}' references assembly '{assemblyName}' " +
                        "that is not declared in 'analysis.target_assemblies'. Every assembly listed in " +
                        "'strict_assembly_independence'/'audit_assembly_independence' must be a declared target assembly.");
                }
            }
        }
    }

    private static void ValidateAssemblyDependencyContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureAssemblyDependencyContract contract in document.Contracts.StrictAssemblyDependency
                     .Concat(document.Contracts.AuditAssemblyDependency))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Assembly dependency contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_assembly_dependency'/'audit_assembly_dependency' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive assembly-reference-path " +
                    "resolution is a planned follow-up.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Assembly dependency contract '{contract.Name}' references source assembly '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                    "'strict_assembly_dependency'/'audit_assembly_dependency' must be a declared target assembly.");
            }

            foreach (string assemblyName in contract.Forbidden)
            {
                if (!targetAssemblies.Contains(assemblyName))
                {
                    throw new InvalidOperationException(
                        $"Assembly dependency contract '{contract.Name}' references forbidden assembly '{assemblyName}' " +
                        "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                        "'strict_assembly_dependency'/'audit_assembly_dependency' must be a declared target assembly.");
                }
            }
        }
    }

    private static void ValidateAssemblyAllowOnlyContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitectureAssemblyAllowOnlyContract contract in document.Contracts.StrictAssemblyAllowOnly
                     .Concat(document.Contracts.AuditAssemblyAllowOnly))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Assembly allow-only contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_assembly_allow_only'/'audit_assembly_allow_only' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive assembly-reference-path " +
                    "resolution is a planned follow-up.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Assembly allow-only contract '{contract.Name}' references source assembly '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                    "'strict_assembly_allow_only'/'audit_assembly_allow_only' must be a declared target assembly.");
            }

            foreach (string assemblyName in contract.Allowed)
            {
                if (!targetAssemblies.Contains(assemblyName))
                {
                    throw new InvalidOperationException(
                        $"Assembly allow-only contract '{contract.Name}' references allowed assembly '{assemblyName}' " +
                        "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                        "'strict_assembly_allow_only'/'audit_assembly_allow_only' must be a declared target assembly.");
                }
            }
        }
    }

    private static void ValidatePackageDependencyContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePackageDependencyContract contract in document.Contracts.StrictPackageDependency
                     .Concat(document.Contracts.AuditPackageDependency))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Package dependency contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_package_dependency'/'audit_package_dependency' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive package-reference " +
                    "resolution is not supported.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Package dependency contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_package_dependency'/'audit_package_dependency' contract must be a declared target assembly.");
            }
        }
    }

    private static void ValidatePackageAllowOnlyContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePackageAllowOnlyContract contract in document.Contracts.StrictPackageAllowOnly
                     .Concat(document.Contracts.AuditPackageAllowOnly))
        {
            if (contract.DependencyDepth != DependencyDepthMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Package allow-only contract '{contract.Name}' declares 'dependency_depth: transitive', which is " +
                    "not supported yet. 'strict_package_allow_only'/'audit_package_allow_only' only support " +
                    "'dependency_depth: direct' (the default) in this release; transitive package-reference " +
                    "resolution is not supported.");
            }

            if (!targetAssemblies.Contains(contract.Source))
            {
                throw new InvalidOperationException(
                    $"Package allow-only contract '{contract.Name}' references source '{contract.Source}' " +
                    "that is not declared in 'analysis.target_assemblies'. The 'source' of a " +
                    "'strict_package_allow_only'/'audit_package_allow_only' contract must be a declared target assembly.");
            }
        }
    }

    private static void ValidateProjectMetadataContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureProjectMetadataContract contract in document.Contracts.StrictProjectMetadata
                     .Concat(document.Contracts.AuditProjectMetadata))
        {
            if (contract.Projects.Count == 0 || contract.Projects.All(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Project metadata contract '{contract.Name}' declares no usable 'projects'. " +
                    "Declare at least one discovered project path, or the contract will never match anything.");
            }

            bool hasExpectation = contract.RequiredProperties.Count > 0
                || contract.ForbiddenProperties.Count > 0
                || contract.AllowedFriendAssemblies is not null
                || HasNonBlankEntry(contract.ForbiddenProjectReferences);

            if (!hasExpectation)
            {
                throw new InvalidOperationException(
                    $"Project metadata contract '{contract.Name}' declares no metadata expectation. " +
                    "Declare required_properties, forbidden_properties, allowed_friend_assemblies, or " +
                    "forbidden_project_references.");
            }
        }
    }

    private static void ValidateTypePlacementContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureTypePlacementContract contract in document.Contracts.StrictTypePlacement
                     .Concat(document.Contracts.AuditTypePlacement))
        {
            ArchitectureTypeMatcher matcher = contract.TypesMatching;
            bool hasSelectorField = !string.IsNullOrEmpty(matcher.NameSuffix)
                || !string.IsNullOrEmpty(matcher.NamePrefix)
                || !string.IsNullOrEmpty(matcher.Namespace)
                || !string.IsNullOrEmpty(matcher.Layer)
                || !string.IsNullOrEmpty(matcher.BaseType)
                || !string.IsNullOrEmpty(matcher.ImplementsInterface)
                || !string.IsNullOrEmpty(matcher.HasAttribute);

            if (!hasSelectorField)
            {
                throw new InvalidOperationException(
                    $"Type placement contract '{contract.Name}' declares no usable types_matching selector field " +
                    "(name_suffix/name_prefix/namespace/layer/base_type/implements_interface/has_attribute). " +
                    "An empty or omitted selector would match every loaded type, turning a role-specific rule into " +
                    "a global one. Declare at least one selector field, or check for a typo'd field name.");
            }

            bool hasPlacementExpectation = contract.MustResideInLayers.Count > 0
                || contract.MustResideInNamespaces.Count > 0
                || contract.MustResideInProjects.Count > 0
                || contract.MustResideInAssemblies.Count > 0;

            bool hasNamingExpectation = !string.IsNullOrEmpty(contract.RequiredNameSuffix)
                || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
                || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
                || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix);

            if (!hasPlacementExpectation && !hasNamingExpectation)
            {
                throw new InvalidOperationException(
                    $"Type placement contract '{contract.Name}' declares a types_matching selector but no placement " +
                    "(must_reside_in_layers/must_reside_in_namespaces/must_reside_in_projects/must_reside_in_assemblies) " +
                    "or naming (required_name_suffix/required_name_prefix/forbidden_name_suffix/forbidden_name_prefix) " +
                    "expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }

    private static void ValidatePublicApiSurfaceContracts(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);

        foreach (ArchitecturePublicApiSurfaceContract contract in document.Contracts.StrictPublicApiSurface
                     .Concat(document.Contracts.AuditPublicApiSurface))
        {
            if (contract.Assemblies.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Public API surface contract '{contract.Name}' declares no 'assemblies'. " +
                    "A contract with nothing to scan is a configuration error; declare at least one target assembly.");
            }

            foreach (string assemblyName in contract.Assemblies)
            {
                if (!targetAssemblies.Contains(assemblyName))
                {
                    throw new InvalidOperationException(
                        $"Public API surface contract '{contract.Name}' references assembly '{assemblyName}' " +
                        "that is not declared in 'analysis.target_assemblies'. Every assembly referenced by " +
                        "'strict_public_api_surface'/'audit_public_api_surface' must be a declared target assembly, " +
                        "otherwise a typo'd assembly name would silently disable the contract instead of failing loudly.");
                }
            }
        }
    }

    private static void ValidateAttributeUsageContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureAttributeUsageContract contract in document.Contracts.StrictAttributeUsage
                     .Concat(document.Contracts.AuditAttributeUsage))
        {
            if (contract.Attributes.Count == 0 && contract.AttributePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Attribute usage contract '{contract.Name}' declares no 'attributes' or 'attribute_prefixes'. " +
                    "A contract with nothing to match against is a configuration error; declare at least one " +
                    "fully-qualified attribute type name or attribute type-name/namespace prefix.");
            }

            bool hasAllowedOnlyExpectation = contract.AllowedOnlyInLayers.Count > 0
                || contract.AllowedOnlyInNamespaces.Count > 0
                || contract.AllowedOnlyInProjects.Count > 0
                || contract.AllowedOnlyInAssemblies.Count > 0;

            bool hasForbiddenExpectation = contract.ForbiddenInLayers.Count > 0
                || contract.ForbiddenInNamespaces.Count > 0
                || contract.ForbiddenInProjects.Count > 0
                || contract.ForbiddenInAssemblies.Count > 0;

            if (!hasAllowedOnlyExpectation && !hasForbiddenExpectation)
            {
                throw new InvalidOperationException(
                    $"Attribute usage contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "or forbidden_in_layers/forbidden_in_namespaces/forbidden_in_projects/forbidden_in_assemblies " +
                    "location expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }

    private static void ValidateInheritanceContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureInheritanceContract contract in document.Contracts.StrictInheritance
                     .Concat(document.Contracts.AuditInheritance))
        {
            if (contract.SourceLayers.Count == 0 && contract.SourceNamespaces.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Inheritance contract '{contract.Name}' declares no 'source_layers' or 'source_namespaces'. " +
                    "An empty source surface would silently check no types; declare at least one source layer " +
                    "or namespace prefix.");
            }

            if (contract.ForbiddenBaseTypes.Count == 0 && contract.ForbiddenBaseTypePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Inheritance contract '{contract.Name}' declares no 'forbidden_base_types' or " +
                    "'forbidden_base_type_prefixes'. A contract with nothing to match against is a configuration " +
                    "error; declare at least one fully-qualified base type name or base type-name/namespace prefix.");
            }
        }
    }

    private static void ValidateInterfaceImplementationContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureInterfaceImplementationContract contract in document.Contracts.StrictInterfaceImplementation
                     .Concat(document.Contracts.AuditInterfaceImplementation))
        {
            if (contract.Interfaces.Count == 0 && contract.InterfacePrefixes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Interface implementation contract '{contract.Name}' declares no 'interfaces' or " +
                    "'interface_prefixes'. A contract with nothing to match against is a configuration error; " +
                    "declare at least one fully-qualified interface name or interface type-name/namespace prefix.");
            }

            bool hasAllowedOnlyExpectation = contract.AllowedOnlyInLayers.Count > 0
                || contract.AllowedOnlyInNamespaces.Count > 0
                || contract.AllowedOnlyInProjects.Count > 0
                || contract.AllowedOnlyInAssemblies.Count > 0;

            bool hasForbiddenExpectation = contract.ForbiddenInLayers.Count > 0
                || contract.ForbiddenInNamespaces.Count > 0
                || contract.ForbiddenInProjects.Count > 0
                || contract.ForbiddenInAssemblies.Count > 0;

            if (!hasAllowedOnlyExpectation && !hasForbiddenExpectation)
            {
                throw new InvalidOperationException(
                    $"Interface implementation contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "or forbidden_in_layers/forbidden_in_namespaces/forbidden_in_projects/forbidden_in_assemblies " +
                    "location expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }

    private static void ValidateCompositionContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureCompositionContract contract in document.Contracts.StrictComposition
                     .Concat(document.Contracts.AuditComposition))
        {
            if (!HasNonBlankEntry(contract.ForbiddenApis))
            {
                throw new InvalidOperationException(
                    $"Composition contract '{contract.Name}' declares no 'forbidden_apis'. A contract with " +
                    "nothing to match against is a configuration error; declare at least one forbidden API " +
                    "selector (member name, Type.Member name, fully qualified member, or namespace/type prefix).");
            }

            bool hasAllowedOnlyExpectation = HasNonBlankEntry(contract.AllowedOnlyInLayers)
                || HasNonBlankEntry(contract.AllowedOnlyInNamespaces)
                || HasNonBlankEntry(contract.AllowedOnlyInProjects)
                || HasNonBlankEntry(contract.AllowedOnlyInAssemblies);

            if (!hasAllowedOnlyExpectation)
            {
                throw new InvalidOperationException(
                    $"Composition contract '{contract.Name}' declares no " +
                    "allowed_only_in_layers/allowed_only_in_namespaces/allowed_only_in_projects/allowed_only_in_assemblies " +
                    "composition boundary. Declare at least one, or every call site in the codebase would be " +
                    "considered outside the boundary.");
            }
        }
    }

    private static bool HasNonBlankEntry(IEnumerable<string> values)
    {
        return values.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    // Limited to the contract families ArchitectureContractRunner's GetReferencedLayerNames
    // actually maps to document.Layers keys. Asmdef (source_assemblies, not a layer namespace),
    // acyclic_sibling (ancestors are namespace prefixes, not layer keys), and layer_template are
    // intentionally excluded: layer_template's expanded ArchitectureLayerContract instances carry
    // synthetic IDs ("<template>/<container>") distinct from the authored template ID, and their
    // Layers entries are concrete namespaces rather than document.Layers keys, so neither the ID
    // nor the field values resolve the way rule-input coverage expects. Referencing one of these
    // families is therefore rejected below as an unknown contract ID rather than silently
    // producing zero findings.
    private static HashSet<string> CollectLayerBearingContractIds(ArchitectureContractDocument document)
    {
        IEnumerable<IArchitectureContract>[] groups =
        [
            document.Contracts.Strict,
            document.Contracts.Audit,
            document.Contracts.StrictLayers,
            document.Contracts.AuditLayers,
            document.Contracts.StrictAllowOnly,
            document.Contracts.AuditAllowOnly,
            document.Contracts.StrictCycles,
            document.Contracts.AuditCycles,
            document.Contracts.StrictMethodBody,
            document.Contracts.AuditMethodBody,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictExternalAllowOnly,
            document.Contracts.AuditExternalAllowOnly,
            document.Contracts.StrictTypePlacement,
            document.Contracts.AuditTypePlacement,
            document.Contracts.StrictAttributeUsage,
            document.Contracts.AuditAttributeUsage,
            document.Contracts.StrictInheritance,
            document.Contracts.AuditInheritance,
            document.Contracts.StrictInterfaceImplementation,
            document.Contracts.AuditInterfaceImplementation,
            document.Contracts.StrictComposition,
            document.Contracts.AuditComposition,
        ];

        return new HashSet<string>(
            groups.SelectMany(group => group).Select(c => c.Id).Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }

    private static void ValidateImplementedCoverageScopes(ArchitectureContractDocument document)
    {
        List<ArchitectureCoverageContract> unsupported = document.Contracts.StrictCoverage
            .Concat(document.Contracts.AuditCoverage)
            .Where(contract => !_implementedCoverageScopes.Contains(contract.Scope, StringComparer.Ordinal))
            .ToList();

        if (unsupported.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", unsupported.Select(contract => $"{contract.Name} ({contract.Scope})"));
        throw new InvalidOperationException(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', 'assembly', or " +
            $"'dependency_edge' are implemented right now. Unsupported coverage contract scopes: {details}.");
    }
}
