using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, contract.Source);
        Type[] sourceTypes = TypeIndex.FindTypesInLayer(sourceLayer);

        List<ArchitectureViolation> violations = new();
        bool transitive = contract.DependencyDepth == DependencyDepthMode.Transitive;
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string forbiddenLayerName in contract.Forbidden)
        {
            ArchitectureLayer forbiddenLayer =
                ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, forbiddenLayerName);
            if (transitive)
            {
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindTransitiveNamespaceViolations(sourceTypes,
                    forbiddenLayer, contract.AllowedTypes, Context.TargetAssemblies, executionContext, ReferenceGraph));
            }
            else
            {
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes, forbiddenLayer,
                    contract.AllowedTypes, executionContext, ReferenceGraph));
            }
        }

        if (contract.ForbiddenLegacyRuntime)
        {
            foreach (string forbiddenNamespace in Document.LegacyRuntimeLayers)
            {
                if (transitive)
                {
                    violations.AddRange(ArchitectureNamespaceViolationFinder.FindTransitiveNamespaceViolations(sourceTypes,
                        new ArchitectureLayer { Namespace = forbiddenNamespace },
                        contract.AllowedTypes, Context.TargetAssemblies, executionContext, ReferenceGraph));
                }
                else
                {
                    violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes,
                        new ArchitectureLayer { Namespace = forbiddenNamespace },
                        contract.AllowedTypes, executionContext, ReferenceGraph));
                }
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();

        var effectiveLayers = new List<(string name, ArchitectureLayer layer, Type[] types)>();

        foreach (string layerEntry in contract.Layers)
        {
            ArchitectureLayer layer = ResolveLayerEntry(contract, layerEntry);
            Type[] types = TypeIndex.FindTypesInLayer(layer);

            if (types.Length == 0)
            {
                if (contract.OptionalLayers.Contains(layerEntry))
                {
                    continue;
                }

                if (contract.TemplateName != null)
                {
                    violations.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        ArchitectureLayerResolver.DescribeLayer(layer),
                        "empty layer namespace",
                        new[] { $"Required layer '{layerEntry}' namespace '{layer.Namespace}' contains no types in loaded assemblies." })
                    {
                        TemplateName = contract.TemplateName,
                        ContainerNamespace = contract.ContainerNamespace
                    });
                }
            }

            effectiveLayers.Add((layerEntry, layer, types));
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        for (int sourceIndex = 0; sourceIndex < effectiveLayers.Count; sourceIndex++)
        {
            var (_, _, sourceTypes) = effectiveLayers[sourceIndex];

            for (int forbiddenIndex = 0; forbiddenIndex < sourceIndex; forbiddenIndex++)
            {
                var (_, forbiddenLayer, _) = effectiveLayers[forbiddenIndex];
                foreach (ArchitectureViolation v in ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                    sourceTypes, forbiddenLayer, Array.Empty<string>(), executionContext, ReferenceGraph))
                {
                    violations.Add(v with
                    {
                        TemplateName = contract.TemplateName,
                        ContainerNamespace = contract.ContainerNamespace
                    });
                }
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        if (contract.Exhaustive && contract.ContainerNamespace != null)
        {
            HashSet<string> expectedNamespaces = new(
                effectiveLayers.Select(l => l.layer.Namespace),
                StringComparer.Ordinal);

            foreach (string childNs in TypeIndex.FindDirectChildNamespaces(contract.ContainerNamespace).OrderBy(ns => ns, StringComparer.Ordinal))
            {
                if (expectedNamespaces.Contains(childNs))
                {
                    continue;
                }

                Type[] childTypes = TypeIndex.FindTypesInNamespace(childNs);

                if (childTypes.Length > 0)
                {
                    violations.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        contract.ContainerNamespace,
                        "unmapped sibling namespace",
                        new[] { $"Namespace '{childNs}' contains types but is not mapped into any declared layer in template '{contract.TemplateName}'." })
                    {
                        TemplateName = contract.TemplateName,
                        ContainerNamespace = contract.ContainerNamespace
                    });
                }
            }
        }

        return violations;
    }

    private ArchitectureLayer ResolveLayerEntry(
        ArchitectureLayerContract contract,
        string layerEntry)
    {
        if (contract.TemplateName != null)
        {
            return new ArchitectureLayer { Namespace = layerEntry };
        }

        return ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerEntry);
    }

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(Context.TargetAssemblies, sourceLayer);

        var allowedLayers = contract.Allowed
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, layerName))
            .Append(sourceLayer)
            .ToList();

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureViolation> violations = sourceTypes
            .Select(type =>
            {
                string sourceFullName = ArchitectureTypeNames.SafeFullName(type);
                string[] forbiddenRefs = ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(refType => new
                    {
                        FullName = ArchitectureTypeNames.SafeFullName(refType),
                        Namespace = ArchitectureTypeNames.SafeNamespace(refType)
                    })
                    .Where(r => !string.IsNullOrEmpty(r.FullName))
                    .Where(r => !contract.AllowedTypes.Contains(r.FullName))
                    .Where(r => ArchitectureLayerResolver.IsProjectType(Document, r.Namespace))
                    .Where(r => !ArchitectureNamespaceViolationFinder.IsInAnyAllowedLayer(r.Namespace, allowedLayers))
                    .Where(r => !executionContext.IsIgnored(sourceFullName, r.FullName))
                    .Select(r => r.FullName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray();
                return new ArchitectureViolation(
                    contract.Name, contract.Id, sourceFullName, "outside allowed layers", forbiddenRefs);
            })
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToList();

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return Array.Empty<string>();
        }

        var contractLayers = contract.Layers.ToHashSet(StringComparer.Ordinal);
        var graph = contractLayers.ToDictionary(
            layer => layer,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, sourceLayerName);
            Type[] sourceTypes = TypeIndex.FindTypesInLayer(sourceLayer);

            foreach (Type sourceType in sourceTypes)
            {
                string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);

                foreach (Type referencedType in ReferenceGraph.GetReferencedTypes(sourceType))
                {
                    string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                    string referencedNamespace = ArchitectureTypeNames.SafeNamespace(referencedType);
                    string? referencedLayerName =
                        ArchitectureLayerResolver.ResolveContainingLayer(Document, referencedNamespace, contractLayers);

                    if (referencedLayerName == null || referencedLayerName == sourceLayerName)
                    {
                        continue;
                    }

                    if (executionContext.IsIgnored(sourceTypeName, referencedTypeName))
                    {
                        continue;
                    }

                    graph[sourceLayerName].Add(referencedLayerName);
                }
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return ArchitectureCycleDetector.FindCycles(graph);
    }

    public IReadOnlyCollection<string> CheckAcyclicSiblingContract(ArchitectureAcyclicSiblingContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return Array.Empty<string>();
        }

        List<string> allCycles = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string ancestor in contract.Ancestors)
        {
            Dictionary<string, List<Type>> siblingGroups =
                ArchitectureSiblingGraphBuilder.BuildSiblingGroups(Context.TargetAssemblies, ancestor);

            if (siblingGroups.Count <= 1)
            {
                continue;
            }

            Dictionary<string, HashSet<string>> graph = new(StringComparer.Ordinal);

            foreach (string siblingName in siblingGroups.Keys)
            {
                graph[siblingName] = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (KeyValuePair<string, List<Type>> sourceEntry in siblingGroups)
            {
                string sourceSibling = sourceEntry.Key;

                foreach (Type sourceType in sourceEntry.Value)
                {
                    string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);

                    foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                    {
                        string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                        string? referencedSibling = ResolveSiblingGroup(siblingGroups, referencedTypeName, ancestor);

                        if (referencedSibling == null || referencedSibling == sourceSibling)
                        {
                            continue;
                        }

                        if (executionContext.IsIgnored(sourceTypeName, referencedTypeName))
                        {
                            continue;
                        }

                        graph[sourceSibling].Add(referencedSibling);
                    }
                }
            }

            IReadOnlyCollection<string> ancestorCycles = ArchitectureCycleDetector.FindCycles(graph);

            allCycles.AddRange(
                ancestorCycles.Select(c => $"{ancestor}: {c}"));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return allCycles;
    }

    private static string? ResolveSiblingGroup(
        Dictionary<string, List<Type>> siblingGroups,
        string typeName,
        string ancestorNamespace)
    {
        string prefix = ancestorNamespace + ".";

        int dotIndex = typeName.LastIndexOf('.');
        if (dotIndex < 0)
        {
            return null;
        }

        string ns = typeName[..dotIndex];

        if (!ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string remainder = ns[prefix.Length..];
        int childDotIndex = remainder.IndexOf('.');
        string child = childDotIndex < 0 ? remainder : remainder[..childDotIndex];

        return siblingGroups.ContainsKey(child) ? child : null;
    }

    public List<ArchitectureViolation> CheckMethodBodyContract(ArchitectureMethodBodyContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, contract.Source);

        string[]? sourceRoots = Document.Analysis.SourceRoots.Count > 0
            ? Document.Analysis.SourceRoots.ToArray()
            : null;

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        IReadOnlyList<ArchitectureViolation> roslynViolations = ArchitectureSourceScanner
            .FindMethodBodyViolations(Context.RepositoryRoot, sourceLayer.Namespace,
                contract.ForbiddenCalls, executionContext, sourceRoots: sourceRoots,
                sourceLayer: sourceLayer, preprocessorSymbols: PreprocessorSymbols)
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = ArchitectureIlMethodBodyScanner.FindMethodBodyViolations(
            Context.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            executionContext,
            sourceLayer: sourceLayer)
            .ToList();

        List<ArchitectureViolation> violations = ArchitectureNamespaceViolationFinder.MergeMethodBodyViolations(contract.Name, contract.Id, roslynViolations, ilViolations);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return ArchitectureAsmdefScanner.FindAsmdefViolations(contract.Name, contract.Id, Context.RepositoryRoot, contract)
            .ToList();
    }

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, sourceLayerName);
            Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(Context.TargetAssemblies, sourceLayer);

            foreach (string forbiddenLayerName in contract.Layers)
            {
                if (string.Equals(sourceLayerName, forbiddenLayerName, StringComparison.Ordinal))
                {
                    continue;
                }

                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, forbiddenLayerName);
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), executionContext));
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        HashSet<string> allowedTypes = new(contract.AllowedTypes, StringComparer.Ordinal);

        HashSet<string> allLayerNames = new(Document.Layers.Keys, StringComparer.Ordinal);

        List<ArchitectureLayer> allowedImporterLayers = contract.AllowedImporters
            .Select(name => ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, name))
            .ToList();

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string protectedLayerName in contract.Protected)
        {
            ArchitectureLayer protectedLayer =
                ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, protectedLayerName);

            foreach (Assembly assembly in Context.TargetAssemblies)
            {
                foreach (Type sourceType in ArchitectureTypeScanner.GetLoadableTypes(assembly))
                {
                    string sourceTypeFullName = ArchitectureTypeNames.SafeFullName(sourceType);
                    if (string.IsNullOrEmpty(sourceTypeFullName))
                    {
                        continue;
                    }

                    string sourceNs = ArchitectureTypeNames.SafeNamespace(sourceType);

                    if (!string.IsNullOrEmpty(sourceNs) &&
                        ArchitectureLayerResolver.MatchesNamespace(protectedLayer, sourceNs))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(sourceNs) &&
                        allowedImporterLayers.Any(l => ArchitectureLayerResolver.MatchesNamespace(l, sourceNs)))
                    {
                        continue;
                    }

                    string? sourceLayerName = ArchitectureLayerResolver.ResolveContainingLayer(
                        Document, sourceNs, allLayerNames);

                    List<string> matchingRefs = new();
                    HashSet<string> matchedNamespacePrefixes = new(StringComparer.Ordinal);

                    foreach (Type refType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                    {
                        string refFullName = ArchitectureTypeNames.SafeFullName(refType);
                        if (string.IsNullOrEmpty(refFullName))
                        {
                            continue;
                        }

                        ArchitectureNamespaceMatch protectedMatch = ArchitectureLayerResolver.MatchNamespace(
                            protectedLayer, ArchitectureTypeNames.SafeNamespace(refType));
                        if (!protectedMatch.Matched)
                        {
                            continue;
                        }

                        if (allowedTypes.Contains(sourceTypeFullName))
                        {
                            continue;
                        }

                        if (executionContext.IsIgnored(sourceTypeFullName, refFullName))
                        {
                            continue;
                        }

                        matchingRefs.Add(refFullName);
                        if (!string.IsNullOrEmpty(protectedMatch.MatchedNamespacePrefix))
                        {
                            matchedNamespacePrefixes.Add(protectedMatch.MatchedNamespacePrefix);
                        }
                    }

                    if (matchingRefs.Count == 0)
                    {
                        continue;
                    }

                    string[] normalizedRefs = matchingRefs
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray();

                    violations.Add(new ArchitectureViolation(
                        contract.Name, contract.Id,
                        sourceTypeFullName,
                        $"protected layer '{protectedLayerName}' (allowed importers: [{string.Join(", ", contract.AllowedImporters)}])",
                        normalizedRefs)
                    {
                        MatchedNamespacePrefixes = matchedNamespacePrefixes.Count > 0
                            ? matchedNamespacePrefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToArray()
                            : null,
                        SourceLayer = sourceLayerName,
                        TargetLayer = protectedLayerName,
                        AllowedImporters = contract.AllowedImporters
                    });
                }
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckExternalContract(ArchitectureExternalDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(Context.TargetAssemblies, sourceLayer);
        List<ArchitectureViolation> violations = new();

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (string externalGroupName in contract.Forbidden)
        {
            if (!Document.ExternalDependencies.TryGetValue(externalGroupName, out ArchitectureExternalDependencyGroup? externalGroup))
            {
                continue;
            }

            violations.AddRange(ArchitectureExternalDependencyViolationFinder.FindViolations(
                externalGroupName,
                sourceTypes,
                externalGroup,
                executionContext));

            violations.AddRange(ArchitectureExternalDependencyIlScanner.FindMethodBodyViolations(
                sourceTypes,
                externalGroupName,
                externalGroup,
                executionContext));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
