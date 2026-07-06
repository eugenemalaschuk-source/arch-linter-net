using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
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

        AddGroup("strict", "strict", "dependency", groups.Strict);
        AddGroup("audit", "audit", "dependency", groups.Audit);
        AddGroup("strict_layers", "strict", "layer", groups.StrictLayers);
        AddGroup("audit_layers", "audit", "layer", groups.AuditLayers);
        AddGroup("strict_allow_only", "strict", "allow_only", groups.StrictAllowOnly);
        AddGroup("audit_allow_only", "audit", "allow_only", groups.AuditAllowOnly);
        AddGroup("strict_cycles", "strict", "cycle", groups.StrictCycles);
        AddGroup("audit_cycles", "audit", "cycle", groups.AuditCycles);
        AddGroup("strict_acyclic_siblings", "strict", "acyclic_sibling", groups.StrictAcyclicSiblings);
        AddGroup("audit_acyclic_siblings", "audit", "acyclic_sibling", groups.AuditAcyclicSiblings);
        AddGroup("strict_method_body", "strict", "method_body", groups.StrictMethodBody);
        AddGroup("audit_method_body", "audit", "method_body", groups.AuditMethodBody);
        AddGroup("strict_independence", "strict", "independence", groups.StrictIndependence);
        AddGroup("audit_independence", "audit", "independence", groups.AuditIndependence);
        AddGroup("strict_protected", "strict", "protected", groups.StrictProtected);
        AddGroup("audit_protected", "audit", "protected", groups.AuditProtected);
        AddGroup("strict_external", "strict", "external", groups.StrictExternal);
        AddGroup("audit_external", "audit", "external", groups.AuditExternal);
        AddGroup("strict_external_allow_only", "strict", "external_allow_only", groups.StrictExternalAllowOnly);
        AddGroup("audit_external_allow_only", "audit", "external_allow_only", groups.AuditExternalAllowOnly);
        AddGroup("strict_asmdef", "strict", "asmdef", groups.StrictAsmdef);
        AddGroup("audit_asmdef", "audit", "asmdef", groups.AuditAsmdef);

        AddGroup("strict_layer_templates", "strict", "layer_template",
            LayerTemplateExpander.Expand(groups.StrictLayerTemplates));
        AddGroup("audit_layer_templates", "audit", "layer_template",
            LayerTemplateExpander.Expand(groups.AuditLayerTemplates));

        return descriptors;
    }

    private List<PolicyConsistencyDiagnostic> FindDuplicateContractIds(
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
        List<PolicyConsistencyDiagnostic> findings = new();

        var forbidPairs = new List<(string Source, string Target, ArchitectureDependencyContract Contract)>();
        foreach (ArchitectureDependencyContract c in Document.Contracts.Strict.Concat(Document.Contracts.Audit))
        {
            foreach (string target in c.Forbidden)
            {
                forbidPairs.Add((c.Source, target, c));
            }
        }

        var allowPairs = new List<(string Source, string Target, ArchitectureAllowOnlyContract Contract)>();
        foreach (ArchitectureAllowOnlyContract c in Document.Contracts.StrictAllowOnly.Concat(Document.Contracts.AuditAllowOnly))
        {
            foreach (string target in c.Allowed)
            {
                allowPairs.Add((c.Source, target, c));
            }
        }

        var conflicts = new List<PolicyConsistencyDiagnostic>();

        foreach (var allow in allowPairs)
        {
            foreach (var forbid in forbidPairs)
            {
                if (!string.Equals(allow.Source, forbid.Source, StringComparison.Ordinal)
                    || !string.Equals(allow.Target, forbid.Target, StringComparison.Ordinal))
                {
                    continue;
                }

                List<string> conflictNames = new() { allow.Contract.Name, forbid.Contract.Name };
                List<string> conflictIds = new();
                if (allow.Contract.Id != null) conflictIds.Add(allow.Contract.Id);
                if (forbid.Contract.Id != null) conflictIds.Add(forbid.Contract.Id);

                conflicts.Add(new PolicyConsistencyDiagnostic(
                    allow.Contract.Name,
                    allow.Contract.Id,
                    "allow-forbid-conflict",
                    $"Allow-only contract '{allow.Contract.Name}' allows '{allow.Source} -> {allow.Target}' " +
                    $"while contract '{forbid.Contract.Name}' forbids the same dependency.",
                    conflictIds,
                    conflictNames,
                    new[] { allow.Source, allow.Target }));
            }
        }

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<PolicyConsistencyDiagnostic> FindIndependenceConflicts()
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        var independenceContracts = Document.Contracts.StrictIndependence
            .Concat(Document.Contracts.AuditIndependence)
            .ToList();

        // Dependency contracts only express forbidding, so they are not a source of "explicit
        // allow" pairs; allow-only and ordered layer contracts are the explicit-allow sources.
        var explicitDependencyPairs = new List<(string Source, string Target, string Name, string? Id)>();

        foreach (ArchitectureAllowOnlyContract c in Document.Contracts.StrictAllowOnly.Concat(Document.Contracts.AuditAllowOnly))
        {
            foreach (string target in c.Allowed)
            {
                explicitDependencyPairs.Add((c.Source, target, c.Name, c.Id));
            }
        }

        foreach (ArchitectureLayerContract c in Document.Contracts.StrictLayers.Concat(Document.Contracts.AuditLayers))
        {
            // Ordered layer contracts express an explicit allowed/ordered dependency between
            // consecutive layers (later layers may depend on earlier ones). Their `Layers`
            // entries are already named top-level layer keys, same as independence.Layers.
            for (int i = 0; i < c.Layers.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    explicitDependencyPairs.Add((c.Layers[i], c.Layers[j], c.Name, c.Id));
                }
            }
        }

        // Expanded layer templates produce the same kind of ordered contract, but their `Layers`
        // entries are concrete container namespaces (e.g. "MyApp.Feature.Domain"), not named
        // layer keys. Resolve each one back to the named top-level layer it belongs to before
        // comparing against independence.Layers, which always lists named layers.
        IReadOnlySet<string> allLayerNames = new HashSet<string>(Document.Layers.Keys, StringComparer.Ordinal);

        IEnumerable<ArchitectureLayerContract> expandedTemplateContracts =
            LayerTemplateExpander.Expand(Document.Contracts.StrictLayerTemplates)
                .Concat(LayerTemplateExpander.Expand(Document.Contracts.AuditLayerTemplates));

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

                    explicitDependencyPairs.Add((resolvedLayers[i]!, resolvedLayers[j]!, c.Name, c.Id));
                }
            }
        }

        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (ArchitectureIndependenceContract independence in independenceContracts)
        {
            HashSet<string> independenceLayers = new(independence.Layers, StringComparer.Ordinal);

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

                List<string> conflictNames = new() { independence.Name, dep.Name };
                List<string> conflictIds = new();
                if (independence.Id != null) conflictIds.Add(independence.Id);
                if (dep.Id != null) conflictIds.Add(dep.Id);

                conflicts.Add(new PolicyConsistencyDiagnostic(
                    independence.Name,
                    independence.Id,
                    "independence-conflict",
                    $"Independence contract '{independence.Name}' requires layers '{dep.Source}' and '{dep.Target}' " +
                    $"to be mutually independent, but contract '{dep.Name}' explicitly allows or orders a dependency between them.",
                    conflictIds,
                    conflictNames,
                    new[] { dep.Source, dep.Target }));
            }
        }

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<PolicyConsistencyDiagnostic> FindProtectedImporterConflicts()
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        var protectedContracts = Document.Contracts.StrictProtected
            .Concat(Document.Contracts.AuditProtected)
            .ToList();

        // Strict forbidden/protected rules that forbid an importer for the same surface:
        // a dependency contract forbidding `importer -> protectedSurface`, or another
        // protected contract over the same surface that does NOT list the importer as allowed.
        var forbiddingDependencies = new List<(string Source, string Target, string Name, string? Id)>();
        foreach (ArchitectureDependencyContract c in Document.Contracts.Strict.Concat(Document.Contracts.Audit))
        {
            foreach (string target in c.Forbidden)
            {
                forbiddingDependencies.Add((c.Source, target, c.Name, c.Id));
            }
        }

        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (ArchitectureProtectedContract protectedContract in protectedContracts)
        {
            foreach (string protectedSurface in protectedContract.Protected)
            {
                foreach (string allowedImporter in protectedContract.AllowedImporters)
                {
                    foreach (var dep in forbiddingDependencies)
                    {
                        if (!string.Equals(dep.Source, allowedImporter, StringComparison.Ordinal)
                            || !string.Equals(dep.Target, protectedSurface, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        List<string> conflictNames = new() { protectedContract.Name, dep.Name };
                        List<string> conflictIds = new();
                        if (protectedContract.Id != null) conflictIds.Add(protectedContract.Id);
                        if (dep.Id != null) conflictIds.Add(dep.Id);

                        conflicts.Add(new PolicyConsistencyDiagnostic(
                            protectedContract.Name,
                            protectedContract.Id,
                            "protected-importer-conflict",
                            $"Protected contract '{protectedContract.Name}' allows '{allowedImporter}' to import " +
                            $"protected surface '{protectedSurface}', but contract '{dep.Name}' forbids that same import.",
                            conflictIds,
                            conflictNames,
                            new[] { protectedSurface, allowedImporter }));
                    }
                }
            }
        }

        // Protected contracts are exhaustive allow-lists: any importer not listed in
        // AllowedImporters is implicitly forbidden from referencing the protected surface.
        // Two protected contracts guarding the same surface with different AllowedImporters
        // sets are therefore in direct conflict over any importer one allows and the other omits.
        for (int i = 0; i < protectedContracts.Count; i++)
        {
            for (int j = 0; j < protectedContracts.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                ArchitectureProtectedContract allowing = protectedContracts[i];
                ArchitectureProtectedContract forbidding = protectedContracts[j];

                if (allowing.Id != null && string.Equals(allowing.Id, forbidding.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IEnumerable<string> sharedSurfaces = allowing.Protected.Intersect(forbidding.Protected, StringComparer.Ordinal);

                foreach (string protectedSurface in sharedSurfaces)
                {
                    foreach (string allowedImporter in allowing.AllowedImporters)
                    {
                        if (forbidding.AllowedImporters.Contains(allowedImporter, StringComparer.Ordinal)
                            || string.Equals(allowedImporter, protectedSurface, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        List<string> conflictNames = new() { allowing.Name, forbidding.Name };
                        List<string> conflictIds = new();
                        if (allowing.Id != null) conflictIds.Add(allowing.Id);
                        if (forbidding.Id != null) conflictIds.Add(forbidding.Id);

                        conflicts.Add(new PolicyConsistencyDiagnostic(
                            allowing.Name,
                            allowing.Id,
                            "protected-importer-conflict",
                            $"Protected contract '{allowing.Name}' allows '{allowedImporter}' to import " +
                            $"protected surface '{protectedSurface}', but protected contract '{forbidding.Name}' " +
                            "guards the same surface without listing that importer as allowed.",
                            conflictIds,
                            conflictNames,
                            new[] { protectedSurface, allowedImporter }));
                    }
                }
            }
        }

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<PolicyConsistencyDiagnostic> FindLayerOverlaps()
    {
        List<PolicyConsistencyDiagnostic> findings = new();

        var internalLayers = Document.Layers
            .Where(kvp => !kvp.Value.External)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        if (internalLayers.Count < 2)
        {
            return findings;
        }

        HashSet<(string, string)> reportedPairs = new();

        foreach (System.Reflection.Assembly assembly in Context.TargetAssemblies.Distinct().OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly)
                .OrderBy(t => ArchitectureTypeNames.SafeFullName(t), StringComparer.Ordinal))
            {
                string ns = ArchitectureTypeNames.SafeNamespace(type);
                if (string.IsNullOrEmpty(ns))
                {
                    continue;
                }

                List<string> matchedLayers = internalLayers
                    .Where(kvp => ArchitectureLayerResolver.MatchesNamespace(kvp.Value, ns))
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (matchedLayers.Count < 2)
                {
                    continue;
                }

                for (int i = 0; i < matchedLayers.Count; i++)
                {
                    for (int j = i + 1; j < matchedLayers.Count; j++)
                    {
                        if (IsContainmentRelationship(internalLayers, matchedLayers[i], matchedLayers[j]))
                        {
                            // One layer is a namespace-prefix container of the other (e.g. a coarse
                            // "core" layer and a nested "core_execution" sub-layer). This is an
                            // intentional hierarchy, not a contradiction, so it is not flagged.
                            continue;
                        }

                        (string, string) pairKey = (matchedLayers[i], matchedLayers[j]);
                        if (!reportedPairs.Add(pairKey))
                        {
                            continue;
                        }

                        string representativeType = ArchitectureTypeNames.SafeFullName(type);

                        findings.Add(new PolicyConsistencyDiagnostic(
                            "<policy-consistency>",
                            null,
                            "layer-overlap",
                            $"Layers '{matchedLayers[i]}' and '{matchedLayers[j]}' both match type '{representativeType}' " +
                            "without an explicit documented allowance.",
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            new[] { matchedLayers[i], matchedLayers[j] })
                        {
                            RepresentativeType = representativeType
                        });
                    }
                }
            }
        }

        return findings
            .OrderBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
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

        HashSet<string> unreachableLayerNames = new(StringComparer.Ordinal);
        foreach (var kvp in Document.Layers)
        {
            if (IsStructurallyUnreachable(kvp.Value))
            {
                unreachableLayerNames.Add(kvp.Key);
            }
        }

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
        if (string.IsNullOrWhiteSpace(layer.Namespace))
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
            _ => Array.Empty<string>()
        };
    }
}
