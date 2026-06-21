using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureContractRunner
{
    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

        List<ArchitectureViolation> violations = new();
        bool transitive = contract.DependencyDepth == DependencyDepthMode.Transitive;
        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string forbiddenLayerName in contract.Forbidden)
        {
            ArchitectureLayer forbiddenLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, forbiddenLayerName);
            if (transitive)
            {
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindTransitiveNamespaceViolations(contract.Name, contract.Id, sourceTypes,
                    forbiddenLayer, contract.AllowedTypes, contract.IgnoredViolations, _context.TargetAssemblies, tracker));
            }
            else
            {
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                    contract.AllowedTypes, contract.IgnoredViolations, tracker));
            }
        }

        if (contract.ForbiddenLegacyRuntime)
        {
            foreach (string forbiddenNamespace in _document.LegacyRuntimeLayers)
            {
                if (transitive)
                {
                    violations.AddRange(ArchitectureNamespaceViolationFinder.FindTransitiveNamespaceViolations(contract.Name, contract.Id, sourceTypes,
                        new ArchitectureLayer { Namespace = forbiddenNamespace },
                        contract.AllowedTypes, contract.IgnoredViolations, _context.TargetAssemblies, tracker));
                }
                else
                {
                    violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(contract.Name, contract.Id, sourceTypes,
                        new ArchitectureLayer { Namespace = forbiddenNamespace },
                        contract.AllowedTypes, contract.IgnoredViolations, tracker));
                }
            }
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();

        var effectiveLayers = new List<(string name, ArchitectureLayer layer, Type[] types)>();

        foreach (string layerEntry in contract.Layers)
        {
            ArchitectureLayer layer = ResolveLayerEntry(contract, layerEntry);
            Type[] types = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, layer);

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

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        for (int sourceIndex = 0; sourceIndex < effectiveLayers.Count; sourceIndex++)
        {
            var (_, _, sourceTypes) = effectiveLayers[sourceIndex];

            for (int forbiddenIndex = 0; forbiddenIndex < sourceIndex; forbiddenIndex++)
            {
                var (_, forbiddenLayer, _) = effectiveLayers[forbiddenIndex];
                foreach (ArchitectureViolation v in ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                    contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), contract.IgnoredViolations, tracker))
                {
                    violations.Add(v with
                    {
                        TemplateName = contract.TemplateName,
                        ContainerNamespace = contract.ContainerNamespace
                    });
                }
            }
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);

        if (contract.Exhaustive && contract.ContainerNamespace != null)
        {
            HashSet<string> expectedNamespaces = new(
                effectiveLayers.Select(l => l.layer.Namespace),
                StringComparer.Ordinal);

            foreach (string childNs in FindChildNamespaces(contract.ContainerNamespace).OrderBy(ns => ns, StringComparer.Ordinal))
            {
                if (expectedNamespaces.Contains(childNs))
                {
                    continue;
                }

                Type[] childTypes = ArchitectureTypeScanner.FindTypesInNamespace(
                    _context.TargetAssemblies, childNs);

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

        return ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, layerEntry);
    }

    private HashSet<string> FindChildNamespaces(string containerNamespace)
    {
        string prefix = containerNamespace + ".";
        HashSet<string> children = new(StringComparer.Ordinal);

        foreach (Assembly assembly in _context.TargetAssemblies.Distinct())
        {
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                string ns = ArchitectureTypeNames.SafeNamespace(type);
                if (!ns.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string remainder = ns[prefix.Length..];
                int dotIndex = remainder.IndexOf('.');
                string child = dotIndex < 0 ? remainder : remainder[..dotIndex];
                children.Add($"{prefix}{child}");
            }
        }

        return children;
    }

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

        var allowedLayers = contract.Allowed
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, layerName))
            .Append(sourceLayer)
            .ToList();

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        List<ArchitectureViolation> violations = sourceTypes
            .Select(type => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                ArchitectureTypeNames.SafeFullName(type),
                "outside allowed layers",
                ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(ArchitectureTypeNames.SafeFullName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Where(name => !contract.AllowedTypes.Contains(name))
                    .Where(name => ArchitectureLayerResolver.IsProjectType(_document, name))
                    .Where(name => !ArchitectureNamespaceViolationFinder.IsInAnyAllowedLayer(name, allowedLayers))
                    .Where(name => !ArchitectureIgnoreMatcher.IsIgnored(ArchitectureTypeNames.SafeFullName(type), name,
                        contract.IgnoredViolations, tracker))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray()))
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToList();

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return Array.Empty<string>();
        }

        var contractLayers = contract.Layers.ToHashSet(StringComparer.Ordinal);
        var graph = contractLayers.ToDictionary(
            layer => layer,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, sourceLayerName);
            Type[] sourceTypes =
                ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

            foreach (Type sourceType in sourceTypes)
            {
                string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);

                foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                {
                    string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                    string? referencedLayerName =
                        ArchitectureLayerResolver.ResolveContainingLayer(_document, referencedTypeName, contractLayers);

                    if (referencedLayerName == null || referencedLayerName == sourceLayerName)
                    {
                        continue;
                    }

                    if (ArchitectureIgnoreMatcher.IsIgnored(sourceTypeName, referencedTypeName,
                            contract.IgnoredViolations, tracker))
                    {
                        continue;
                    }

                    graph[sourceLayerName].Add(referencedLayerName);
                }
            }
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return ArchitectureCycleDetector.FindCycles(graph);
    }

    public IReadOnlyCollection<string> CheckAcyclicSiblingContract(ArchitectureAcyclicSiblingContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return Array.Empty<string>();
        }

        List<string> allCycles = new();
        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string ancestor in contract.Ancestors)
        {
            Dictionary<string, List<Type>> siblingGroups =
                ArchitectureSiblingGraphBuilder.BuildSiblingGroups(_context.TargetAssemblies, ancestor);

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

                        if (ArchitectureIgnoreMatcher.IsIgnored(sourceTypeName, referencedTypeName,
                                contract.IgnoredViolations, tracker))
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

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
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
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);

        string[]? sourceRoots = _document.Analysis.SourceRoots.Count > 0
            ? _document.Analysis.SourceRoots.ToArray()
            : null;

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        IReadOnlyList<ArchitectureViolation> roslynViolations = ArchitectureSourceScanner
            .FindMethodBodyViolations(contract.Name, contract.Id, _context.RepositoryRoot, sourceLayer.Namespace,
                contract.ForbiddenCalls, contract.IgnoredViolations, sourceRoots: sourceRoots,
                sourceLayer: sourceLayer, usageTracker: tracker, preprocessorSymbols: _preprocessorSymbols)
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = ArchitectureIlMethodBodyScanner.FindMethodBodyViolations(
            contract.Name,
            contract.Id,
            _context.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            contract.IgnoredViolations,
            sourceLayer: sourceLayer,
            usageTracker: tracker)
            .ToList();

        List<ArchitectureViolation> violations = ArchitectureNamespaceViolationFinder.MergeMethodBodyViolations(contract.Name, contract.Id, roslynViolations, ilViolations);
        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return ArchitectureAsmdefScanner.FindAsmdefViolations(contract.Name, contract.Id, _context.RepositoryRoot, contract)
            .ToList();
    }

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, sourceLayerName);
            Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

            foreach (string forbiddenLayerName in contract.Layers)
            {
                if (string.Equals(sourceLayerName, forbiddenLayerName, StringComparison.Ordinal))
                {
                    continue;
                }

                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, forbiddenLayerName);
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), contract.IgnoredViolations, tracker));
            }
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        HashSet<string> allowedTypes = new(contract.AllowedTypes, StringComparer.Ordinal);

        HashSet<string> allLayerNames = new(_document.Layers.Keys, StringComparer.Ordinal);

        List<ArchitectureLayer> allowedImporterLayers = contract.AllowedImporters
            .Select(name => ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, name))
            .ToList();

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string protectedLayerName in contract.Protected)
        {
            ArchitectureLayer protectedLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, protectedLayerName);

            foreach (Assembly assembly in _context.TargetAssemblies)
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
                        _document, sourceTypeFullName, allLayerNames);

                    List<string> matchingRefs = new();

                    foreach (Type refType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                    {
                        string refFullName = ArchitectureTypeNames.SafeFullName(refType);
                        if (string.IsNullOrEmpty(refFullName))
                        {
                            continue;
                        }

                        if (!ArchitectureLayerResolver.MatchesNamespace(
                                protectedLayer, ArchitectureTypeNames.SafeNamespace(refType)))
                        {
                            continue;
                        }

                        if (allowedTypes.Contains(sourceTypeFullName))
                        {
                            continue;
                        }

                        if (ArchitectureIgnoreMatcher.IsIgnored(
                                sourceTypeFullName, refFullName, contract.IgnoredViolations, tracker))
                        {
                            continue;
                        }

                        matchingRefs.Add(refFullName);
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
                        SourceLayer = sourceLayerName,
                        TargetLayer = protectedLayerName,
                        AllowedImporters = contract.AllowedImporters
                    });
                }
            }
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckExternalContract(ArchitectureExternalDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);
        List<ArchitectureViolation> violations = new();

        ArchitectureIgnoreUsageTracker? tracker = _enableUnmatchedIgnoreTracking && contract.IgnoredViolations.Count > 0 ? new() : null;

        foreach (string externalGroupName in contract.Forbidden)
        {
            if (!_document.ExternalDependencies.TryGetValue(externalGroupName, out ArchitectureExternalDependencyGroup? externalGroup))
            {
                continue;
            }

            violations.AddRange(ArchitectureExternalDependencyViolationFinder.FindViolations(
                contract.Name,
                contract.Id,
                externalGroupName,
                sourceTypes,
                externalGroup,
                contract.IgnoredViolations,
                tracker));
        }

        RecordUnmatchedIgnores(contract.Name, contract.Id, contract.IgnoredViolations, tracker, _unmatchedIgnoredViolations);
        return violations;
    }
}
