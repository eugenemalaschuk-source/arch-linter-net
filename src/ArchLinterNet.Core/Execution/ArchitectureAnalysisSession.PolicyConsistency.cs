using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private const string StrictMode = "strict";
    private const string AuditMode = "audit";

    public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency()
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        List<ArchitectureContractDescriptor> descriptors = BuildAllDescriptors();

        findings.AddRange(FindDuplicateContractIds(descriptors));
        findings.AddRange(FindAllowForbidConflicts());
        findings.AddRange(FindIndependenceConflicts());
        findings.AddRange(FindProtectedImporterConflicts());
        findings.AddRange(FindLayerOverlaps());
        findings.AddRange(FindUnreachableContracts(descriptors));

        return findings
            .OrderBy(f => f.CheckKind, StringComparer.Ordinal)
            .ThenBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => f.ContractId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ThenBy(f => f.RepresentativeType ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    private List<ArchitectureContractDescriptor> BuildAllDescriptors()
    {
        List<ArchitectureContractDescriptor> descriptors = new();

        void AddGroup<T>(string group, string mode, string family, IEnumerable<T> contracts)
            where T : IArchitectureContract
        {
            foreach (T contract in contracts)
            {
                descriptors.Add(new ArchitectureContractDescriptor(group, mode, family, contract.Name, contract.Id, contract));
            }
        }

        ArchitectureContractGroups groups = Document.Contracts;

        AddGroup("strict", StrictMode, "dependency", groups.Strict);
        AddGroup("audit", AuditMode, "dependency", groups.Audit);
        AddGroup("strict_layers", StrictMode, "layer", groups.StrictLayers);
        AddGroup("audit_layers", AuditMode, "layer", groups.AuditLayers);
        AddGroup("strict_allow_only", StrictMode, "allow_only", groups.StrictAllowOnly);
        AddGroup("audit_allow_only", AuditMode, "allow_only", groups.AuditAllowOnly);
        AddGroup("strict_cycles", StrictMode, "cycle", groups.StrictCycles);
        AddGroup("audit_cycles", AuditMode, "cycle", groups.AuditCycles);
        AddGroup("strict_acyclic_siblings", StrictMode, "acyclic_sibling", groups.StrictAcyclicSiblings);
        AddGroup("audit_acyclic_siblings", AuditMode, "acyclic_sibling", groups.AuditAcyclicSiblings);
        AddGroup("strict_method_body", StrictMode, "method_body", groups.StrictMethodBody);
        AddGroup("audit_method_body", AuditMode, "method_body", groups.AuditMethodBody);
        AddGroup("strict_independence", StrictMode, "independence", groups.StrictIndependence);
        AddGroup("audit_independence", AuditMode, "independence", groups.AuditIndependence);
        AddGroup("strict_protected", StrictMode, "protected", groups.StrictProtected);
        AddGroup("audit_protected", AuditMode, "protected", groups.AuditProtected);
        AddGroup("strict_external", StrictMode, "external", groups.StrictExternal);
        AddGroup("audit_external", AuditMode, "external", groups.AuditExternal);
        AddGroup("strict_external_allow_only", StrictMode, "external_allow_only", groups.StrictExternalAllowOnly);
        AddGroup("audit_external_allow_only", AuditMode, "external_allow_only", groups.AuditExternalAllowOnly);
        AddGroup("strict_asmdef", StrictMode, "asmdef", groups.StrictAsmdef);
        AddGroup("audit_asmdef", AuditMode, "asmdef", groups.AuditAsmdef);
        AddGroup("strict_type_placement", StrictMode, "type_placement", groups.StrictTypePlacement);
        AddGroup("audit_type_placement", AuditMode, "type_placement", groups.AuditTypePlacement);
        AddGroup("strict_attribute_usage", StrictMode, "attribute_usage", groups.StrictAttributeUsage);
        AddGroup("audit_attribute_usage", AuditMode, "attribute_usage", groups.AuditAttributeUsage);
        AddGroup("strict_inheritance", StrictMode, "inheritance", groups.StrictInheritance);
        AddGroup("audit_inheritance", AuditMode, "inheritance", groups.AuditInheritance);
        AddGroup("strict_interface_implementation", StrictMode, "interface_implementation", groups.StrictInterfaceImplementation);
        AddGroup("audit_interface_implementation", AuditMode, "interface_implementation", groups.AuditInterfaceImplementation);
        AddGroup("strict_composition", StrictMode, "composition", groups.StrictComposition);
        AddGroup("audit_composition", AuditMode, "composition", groups.AuditComposition);

        AddGroup("strict_layer_templates", StrictMode, "layer_template",
            LayerTemplateExpander.Expand(groups.StrictLayerTemplates));
        AddGroup("audit_layer_templates", AuditMode, "layer_template",
            LayerTemplateExpander.Expand(groups.AuditLayerTemplates));

        return descriptors;
    }

    private static List<PolicyConsistencyDiagnostic> FindDuplicateContractIds(
        List<ArchitectureContractDescriptor> descriptors)
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        var byId = descriptors
            .Where(d => !string.IsNullOrEmpty(d.Id))
            .GroupBy(d => d.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byId)
        {
            List<ArchitectureContractDescriptor> conflicting = group
                .OrderBy(d => d.Name, StringComparer.Ordinal)
                .ToList();

            string[] names = conflicting.Select(d => d.Name).ToArray();
            string[] ids = conflicting.Select(d => d.Id!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            findings.Add(new PolicyConsistencyDiagnostic(
                conflicting[0].Name,
                conflicting[0].Id,
                "duplicate-id",
                $"Contract ID '{group.Key}' is shared by multiple contracts: {string.Join(", ", names)}.",
                ids,
                names,
                Array.Empty<string>()));
        }

        return findings;
    }

    private List<PolicyConsistencyDiagnostic> FindAllowForbidConflicts()
    {
        List<(string Source, string Target, ArchitectureDependencyContract Contract)> forbidPairs =
            BuildForbidPairs();
        List<(string Source, string Target, ArchitectureAllowOnlyContract Contract)> allowPairs =
            BuildAllowPairs();

        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (var allow in allowPairs)
        {
            foreach (var forbid in forbidPairs)
            {
                if (!string.Equals(allow.Source, forbid.Source, StringComparison.Ordinal)
                    || !string.Equals(allow.Target, forbid.Target, StringComparison.Ordinal))
                {
                    continue;
                }

                conflicts.Add(CreateAllowForbidConflict(allow, forbid));
            }
        }

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<(string Source, string Target, ArchitectureDependencyContract Contract)> BuildForbidPairs()
    {
        List<(string Source, string Target, ArchitectureDependencyContract Contract)> forbidPairs = new();

        foreach (ArchitectureDependencyContract c in Document.Contracts.Strict.Concat(Document.Contracts.Audit))
        {
            foreach (string target in c.Forbidden)
            {
                forbidPairs.Add((c.Source, target, c));
            }
        }

        return forbidPairs;
    }

    private List<(string Source, string Target, ArchitectureAllowOnlyContract Contract)> BuildAllowPairs()
    {
        List<(string Source, string Target, ArchitectureAllowOnlyContract Contract)> allowPairs = new();

        foreach (ArchitectureAllowOnlyContract c in Document.Contracts.StrictAllowOnly.Concat(Document.Contracts.AuditAllowOnly))
        {
            foreach (string target in c.Allowed)
            {
                allowPairs.Add((c.Source, target, c));
            }
        }

        return allowPairs;
    }

    private static PolicyConsistencyDiagnostic CreateAllowForbidConflict(
        (string Source, string Target, ArchitectureAllowOnlyContract Contract) allow,
        (string Source, string Target, ArchitectureDependencyContract Contract) forbid)
    {
        List<string> conflictNames = new() { allow.Contract.Name, forbid.Contract.Name };
        List<string> conflictIds = new();
        if (allow.Contract.Id != null) conflictIds.Add(allow.Contract.Id);
        if (forbid.Contract.Id != null) conflictIds.Add(forbid.Contract.Id);

        return new PolicyConsistencyDiagnostic(
            allow.Contract.Name,
            allow.Contract.Id,
            "allow-forbid-conflict",
            $"Allow-only contract '{allow.Contract.Name}' allows '{allow.Source} -> {allow.Target}' " +
            $"while contract '{forbid.Contract.Name}' forbids the same dependency.",
            conflictIds,
            conflictNames,
            new[] { allow.Source, allow.Target });
    }

    private List<PolicyConsistencyDiagnostic> FindIndependenceConflicts()
    {
        List<ArchitectureIndependenceContract> independenceContracts = Document.Contracts.StrictIndependence
            .Concat(Document.Contracts.AuditIndependence)
            .ToList();

        List<(string Source, string Target, string Name, string? Id)> explicitDependencyPairs =
            BuildExplicitDependencyPairs();

        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (ArchitectureIndependenceContract independence in independenceContracts)
        {
            conflicts.AddRange(FindIndependenceConflictsFor(independence, explicitDependencyPairs));
        }

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<(string Source, string Target, string Name, string? Id)> BuildExplicitDependencyPairs()
    {
        // Dependency contracts only express forbidding, so they are not a source of "explicit
        // allow" pairs; allow-only and ordered layer contracts are the explicit-allow sources.
        List<(string Source, string Target, string Name, string? Id)> pairs = new();

        pairs.AddRange(CollectAllowOnlyDependencyPairs());
        pairs.AddRange(CollectLayerContractDependencyPairs());
        pairs.AddRange(CollectExpandedTemplateDependencyPairs());

        return pairs;
    }

    private List<(string Source, string Target, string Name, string? Id)> CollectAllowOnlyDependencyPairs()
    {
        List<(string Source, string Target, string Name, string? Id)> pairs = new();

        foreach (ArchitectureAllowOnlyContract c in Document.Contracts.StrictAllowOnly.Concat(Document.Contracts.AuditAllowOnly))
        {
            foreach (string target in c.Allowed)
            {
                pairs.Add((c.Source, target, c.Name, c.Id));
            }
        }

        return pairs;
    }

    private List<(string Source, string Target, string Name, string? Id)> CollectLayerContractDependencyPairs()
    {
        // Ordered layer contracts express an explicit allowed/ordered dependency between
        // consecutive layers (later layers may depend on earlier ones). Their `Layers`
        // entries are already named top-level layer keys, same as independence.Layers.
        List<(string Source, string Target, string Name, string? Id)> pairs = new();

        foreach (ArchitectureLayerContract c in Document.Contracts.StrictLayers.Concat(Document.Contracts.AuditLayers))
        {
            for (int i = 0; i < c.Layers.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    pairs.Add((c.Layers[i], c.Layers[j], c.Name, c.Id));
                }
            }
        }

        return pairs;
    }

    private List<(string Source, string Target, string Name, string? Id)> CollectExpandedTemplateDependencyPairs()
    {
        // Expanded layer templates produce the same kind of ordered contract, but their `Layers`
        // entries are concrete container namespaces (e.g. "MyApp.Feature.Domain"), not named
        // layer keys. Resolve each one back to the named top-level layer it belongs to before
        // comparing against independence.Layers, which always lists named layers.
        IReadOnlySet<string> allLayerNames = new HashSet<string>(Document.Layers.Keys, StringComparer.Ordinal);

        IEnumerable<ArchitectureLayerContract> expandedTemplateContracts =
            LayerTemplateExpander.Expand(Document.Contracts.StrictLayerTemplates)
                .Concat(LayerTemplateExpander.Expand(Document.Contracts.AuditLayerTemplates));

        List<(string Source, string Target, string Name, string? Id)> pairs = new();

        foreach (ArchitectureLayerContract c in expandedTemplateContracts)
        {
            List<string?> resolvedLayers = c.Layers
                .Select(ns => ArchitectureLayerResolver.ResolveContainingLayer(Document, ns, allLayerNames))
                .ToList();

            for (int i = 0; i < resolvedLayers.Count; i++)
            {
                if (resolvedLayers[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    if (resolvedLayers[j] == null)
                    {
                        continue;
                    }

                    pairs.Add((resolvedLayers[i]!, resolvedLayers[j]!, c.Name, c.Id));
                }
            }
        }

        return pairs;
    }

    private static List<PolicyConsistencyDiagnostic> FindIndependenceConflictsFor(
        ArchitectureIndependenceContract independence,
        List<(string Source, string Target, string Name, string? Id)> explicitDependencyPairs)
    {
        HashSet<string> independenceLayers = new(independence.Layers, StringComparer.Ordinal);
        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (var dep in explicitDependencyPairs)
        {
            if (!independenceLayers.Contains(dep.Source) || !independenceLayers.Contains(dep.Target))
            {
                continue;
            }

            if (string.Equals(dep.Source, dep.Target, StringComparison.Ordinal))
            {
                continue;
            }

            conflicts.Add(CreateIndependenceConflict(independence, dep));
        }

        return conflicts;
    }

    private static PolicyConsistencyDiagnostic CreateIndependenceConflict(
        ArchitectureIndependenceContract independence,
        (string Source, string Target, string Name, string? Id) dep)
    {
        List<string> conflictNames = new() { independence.Name, dep.Name };
        List<string> conflictIds = new();
        if (independence.Id != null) conflictIds.Add(independence.Id);
        if (dep.Id != null) conflictIds.Add(dep.Id);

        return new PolicyConsistencyDiagnostic(
            independence.Name,
            independence.Id,
            "independence-conflict",
            $"Independence contract '{independence.Name}' requires layers '{dep.Source}' and '{dep.Target}' " +
            $"to be mutually independent, but contract '{dep.Name}' explicitly allows or orders a dependency between them.",
            conflictIds,
            conflictNames,
            new[] { dep.Source, dep.Target });
    }

    private List<PolicyConsistencyDiagnostic> FindLayerOverlaps()
    {
        List<KeyValuePair<string, ArchitectureLayer>> internalLayers = Document.Layers
            .Where(kvp => !kvp.Value.External)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        if (internalLayers.Count < 2)
        {
            return new List<PolicyConsistencyDiagnostic>();
        }

        List<PolicyConsistencyDiagnostic> findings = new();
        HashSet<(string, string)> reportedPairs = new();

        foreach (System.Reflection.Assembly assembly in Context.TargetAssemblies.Distinct().OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly)
                .OrderBy(t => ArchitectureTypeNames.SafeFullName(t), StringComparer.Ordinal))
            {
                findings.AddRange(FindLayerOverlapsForType(type, internalLayers, reportedPairs));
            }
        }

        return findings
            .OrderBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<PolicyConsistencyDiagnostic> FindLayerOverlapsForType(
        Type type,
        List<KeyValuePair<string, ArchitectureLayer>> internalLayers,
        HashSet<(string, string)> reportedPairs)
    {
        List<string> matchedLayers = internalLayers
            .Where(kvp => MatchesLayer(kvp.Value, type))
            .Select(kvp => kvp.Key)
            .ToList();

        if (matchedLayers.Count < 2)
        {
            return new List<PolicyConsistencyDiagnostic>();
        }

        List<PolicyConsistencyDiagnostic> findings = new();

        for (int i = 0; i < matchedLayers.Count; i++)
        {
            for (int j = i + 1; j < matchedLayers.Count; j++)
            {
                PolicyConsistencyDiagnostic? finding = TryCreateLayerOverlapFinding(
                    type, internalLayers, matchedLayers[i], matchedLayers[j], reportedPairs);

                if (finding != null)
                {
                    findings.Add(finding);
                }
            }
        }

        return findings;
    }

    private static PolicyConsistencyDiagnostic? TryCreateLayerOverlapFinding(
        Type type,
        List<KeyValuePair<string, ArchitectureLayer>> internalLayers,
        string layerNameA,
        string layerNameB,
        HashSet<(string, string)> reportedPairs)
    {
        if (IsContainmentRelationship(internalLayers, layerNameA, layerNameB))
        {
            // One layer is a namespace-prefix container of the other (e.g. a coarse
            // "core" layer and a nested "core_execution" sub-layer). This is an
            // intentional hierarchy, not a contradiction, so it is not flagged.
            return null;
        }

        (string, string) pairKey = (layerNameA, layerNameB);
        if (!reportedPairs.Add(pairKey))
        {
            return null;
        }

        string representativeType = ArchitectureTypeNames.SafeFullName(type);

        return new PolicyConsistencyDiagnostic(
            "<policy-consistency>",
            null,
            "layer-overlap",
            $"Layers '{layerNameA}' and '{layerNameB}' both match type '{representativeType}' " +
            "without an explicit documented allowance.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { layerNameA, layerNameB })
        {
            RepresentativeType = representativeType
        };
    }

    private static bool IsContainmentRelationship(
        List<KeyValuePair<string, ArchitectureLayer>> internalLayers,
        string layerNameA,
        string layerNameB)
    {
        ArchitectureLayer layerA = internalLayers.First(kvp => kvp.Key == layerNameA).Value;
        ArchitectureLayer layerB = internalLayers.First(kvp => kvp.Key == layerNameB).Value;

        return IsNamespaceAncestor(layerA.Namespace, layerB.Namespace)
            || IsNamespaceAncestor(layerB.Namespace, layerA.Namespace);
    }

    private static bool IsNamespaceAncestor(string ancestorPattern, string candidateNamespace)
    {
        if (ancestorPattern.Contains('*', StringComparison.Ordinal)
            || candidateNamespace.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(ancestorPattern, candidateNamespace, StringComparison.Ordinal))
        {
            // Identical namespace patterns are a genuine overlap (e.g. two differently-named
            // layers pointing at the same namespace), not a parent/child containment hierarchy.
            return false;
        }

        return ArchitectureLayerResolver.MatchesPrefix(candidateNamespace, ancestorPattern);
    }

    private List<PolicyConsistencyDiagnostic> FindUnreachableContracts(
        List<ArchitectureContractDescriptor> descriptors)
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        HashSet<string> unreachableLayerNames = new(
            Document.Layers.Where(kvp => IsStructurallyUnreachable(kvp.Value)).Select(kvp => kvp.Key),
            StringComparer.Ordinal);

        if (unreachableLayerNames.Count == 0)
        {
            return findings;
        }

        foreach (ArchitectureContractDescriptor descriptor in descriptors)
        {
            foreach (string layerName in GetReferencedLayerNames(descriptor.Contract))
            {
                if (!unreachableLayerNames.Contains(layerName))
                {
                    continue;
                }

                findings.Add(new PolicyConsistencyDiagnostic(
                    descriptor.Name,
                    descriptor.Id,
                    "unreachable-contract",
                    $"Contract '{descriptor.Name}' references layer '{layerName}' whose namespace pattern is " +
                    "structurally impossible to match.",
                    descriptor.Id != null ? new[] { descriptor.Id } : Array.Empty<string>(),
                    new[] { descriptor.Name },
                    new[] { layerName }));
            }
        }

        return findings
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsStructurallyUnreachable(ArchitectureLayer layer)
    {
        if (string.IsNullOrWhiteSpace(layer.Namespace) && layer.Selector == null)
        {
            return true;
        }

        // A namespace_suffix that duplicates a segment already implied by the namespace pattern's
        // trailing wildcard position can never resolve: the suffix and pattern overlap such that
        // no concrete namespace can satisfy both. We detect the structural impossibility case of
        // a non-glob namespace whose suffix is empty/whitespace-only (distinct from "unset"),
        // which can never match because no namespace ends with a blank segment.
        if (layer.NamespaceSuffix.Length > 0 && layer.NamespaceSuffix.Trim().Length == 0)
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetReferencedLayerNames(IArchitectureContract contract)
    {
        return contract switch
        {
            ArchitectureDependencyContract c => new[] { c.Source }.Concat(c.Forbidden),
            ArchitectureAllowOnlyContract c => new[] { c.Source }.Concat(c.Allowed),
            ArchitectureCycleContract c => c.Layers,
            ArchitectureMethodBodyContract c => new[] { c.Source },
            ArchitectureIndependenceContract c => c.Layers,
            ArchitectureLayerContract c => c.Layers,
            ArchitectureProtectedContract c => c.Protected.Concat(c.AllowedImporters),
            ArchitectureExternalDependencyContract c => new[] { c.Source },
            ArchitectureExternalAllowOnlyContract c => new[] { c.Source },
            ArchitectureTypePlacementContract c => GetTypePlacementReferencedLayerNames(c),
            ArchitectureAttributeUsageContract c => GetAttributeUsageReferencedLayerNames(c),
            ArchitectureInheritanceContract c => c.SourceLayers,
            ArchitectureInterfaceImplementationContract c => GetInterfaceImplementationReferencedLayerNames(c),
            ArchitectureCompositionContract c => c.AllowedOnlyInLayers,
            _ => Array.Empty<string>()
        };
    }

    // Shared by GetReferencedLayerNames (dangling-layer deferral, policy-consistency) and
    // CheckConfiguration's own layer collection, so a typo'd layer name in either
    // types_matching.layer or must_reside_in_layers gets the same "referenced but undeclared
    // layer" / rule-input-coverage-deferral treatment every other layer-bearing contract family
    // gets, instead of surfacing only as an uncaught ArchitectureLayerResolver exception.
    internal static IEnumerable<string> GetTypePlacementReferencedLayerNames(ArchitectureTypePlacementContract contract)
    {
        IEnumerable<string> selectorLayer = string.IsNullOrEmpty(contract.TypesMatching.Layer)
            ? Array.Empty<string>()
            : new[] { contract.TypesMatching.Layer };

        return selectorLayer.Concat(contract.MustResideInLayers);
    }

    // Same rationale as GetTypePlacementReferencedLayerNames: a typo'd allowed_only_in_layers/
    // forbidden_in_layers entry must get the shared "referenced but undeclared layer" /
    // rule-input-coverage-deferral treatment instead of surfacing as an uncaught
    // ArchitectureLayerResolver exception from CheckAttributeUsageContract.
    internal static IEnumerable<string> GetAttributeUsageReferencedLayerNames(ArchitectureAttributeUsageContract contract)
    {
        return contract.AllowedOnlyInLayers.Concat(contract.ForbiddenInLayers);
    }

    // Same rationale as GetAttributeUsageReferencedLayerNames: a typo'd allowed_only_in_layers/
    // forbidden_in_layers entry must get the shared "referenced but undeclared layer" /
    // rule-input-coverage-deferral treatment instead of surfacing as an uncaught
    // ArchitectureLayerResolver exception from CheckInterfaceImplementationContract.
    internal static IEnumerable<string> GetInterfaceImplementationReferencedLayerNames(
        ArchitectureInterfaceImplementationContract contract)
    {
        return contract.AllowedOnlyInLayers.Concat(contract.ForbiddenInLayers);
    }
}
