using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
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
        Type[] sourceTypes = FindTypesInLayer(sourceLayer);

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
                    forbiddenLayer, contract.AllowedTypes, Context.TargetAssemblies, executionContext, ReferenceGraph, RoleIndex));
            }
            else
            {
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes, forbiddenLayer,
                    contract.AllowedTypes, executionContext, ReferenceGraph, RoleIndex));
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
                    contract.AllowedTypes, Context.TargetAssemblies, executionContext, ReferenceGraph, RoleIndex));
                }
                else
                {
                    violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes,
                        new ArchitectureLayer { Namespace = forbiddenNamespace },
                    contract.AllowedTypes, executionContext, ReferenceGraph, RoleIndex));
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
            Type[] types = FindTypesInLayer(layer);

            if (types.Length == 0)
            {
                if (contract.OptionalLayers.Contains(layerEntry))
                {
                    continue;
                }

                ArchitectureViolation? emptyLayerViolation = BuildEmptyLayerViolation(contract, layerEntry, layer);
                if (emptyLayerViolation != null)
                {
                    violations.Add(emptyLayerViolation);
                }
            }

            effectiveLayers.Add((layerEntry, layer, types));
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        CollectLayerOrderingViolations(effectiveLayers, contract, executionContext, violations);

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        CollectExhaustiveSiblingViolations(effectiveLayers, contract, violations);

        return violations;
    }

    private static ArchitectureViolation? BuildEmptyLayerViolation(
        ArchitectureLayerContract contract, string layerEntry, ArchitectureLayer layer)
    {
        if (layer.External || contract.TemplateName == null && layer.Selector == null)
        {
            return null;
        }

        string matchDescription = layer.Selector == null
            ? $"namespace '{layer.Namespace}'"
            : $"semantic selector '{ArchitectureLayerResolver.DescribeLayer(layer)}'";

        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            ArchitectureLayerResolver.DescribeLayer(layer),
            layer.Selector == null ? "empty layer namespace" : "empty layer selector",
            new[] { $"Required layer '{layerEntry}' {matchDescription} contains no matching types in loaded assemblies." })
        {
            Payload = new ConfigurationPayload(
                TemplateName: contract.TemplateName,
                ContainerNamespace: contract.ContainerNamespace)
        };
    }

    private void CollectLayerOrderingViolations(
        List<(string name, ArchitectureLayer layer, Type[] types)> effectiveLayers,
        ArchitectureLayerContract contract,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        for (int sourceIndex = 0; sourceIndex < effectiveLayers.Count; sourceIndex++)
        {
            var (_, _, sourceTypes) = effectiveLayers[sourceIndex];

            for (int forbiddenIndex = 0; forbiddenIndex < sourceIndex; forbiddenIndex++)
            {
                var (_, forbiddenLayer, _) = effectiveLayers[forbiddenIndex];
                foreach (ArchitectureViolation v in ArchitectureNamespaceViolationFinder.FindNamespaceViolations(
                    sourceTypes, forbiddenLayer, Array.Empty<string>(), executionContext, ReferenceGraph, RoleIndex))
                {
                    violations.Add(v with
                    {
                        Payload = new ConfigurationPayload(
                            TemplateName: contract.TemplateName,
                            ContainerNamespace: contract.ContainerNamespace)
                    });
                }
            }
        }
    }

    private void CollectExhaustiveSiblingViolations(
        List<(string name, ArchitectureLayer layer, Type[] types)> effectiveLayers,
        ArchitectureLayerContract contract,
        List<ArchitectureViolation> violations)
    {
        if (!contract.Exhaustive || contract.ContainerNamespace == null)
        {
            return;
        }

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
                    Payload = new ConfigurationPayload(
                        TemplateName: contract.TemplateName,
                        ContainerNamespace: contract.ContainerNamespace)
                });
            }
        }
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
        Type[] sourceTypes = FindTypesInLayer(sourceLayer);

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
                        Namespace = ArchitectureTypeNames.SafeNamespace(refType),
                        Type = refType
                    })
                    .Where(r => !string.IsNullOrEmpty(r.FullName))
                    .Where(r => !contract.AllowedTypes.Contains(r.FullName))
                    .Where(r => r.Type != null && IsInAnyDeclaredLayer(r.Type))
                    .Where(r => !ArchitectureNamespaceViolationFinder.IsInAnyAllowedLayer(
                        r.Type!, allowedLayers, RoleIndex))
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
            CollectCycleEdgesForLayer(contract, sourceLayerName, contractLayers, executionContext, graph);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return ArchitectureCycleDetector.FindCycles(graph);
    }

    private void CollectCycleEdgesForLayer(
        ArchitectureCycleContract contract,
        string sourceLayerName,
        HashSet<string> contractLayers,
        ArchitectureContractExecutionContext executionContext,
        Dictionary<string, HashSet<string>> graph)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, sourceLayerName);
        Type[] sourceTypes = FindTypesInLayer(sourceLayer);

        foreach (Type sourceType in sourceTypes)
        {
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);

            foreach (Type referencedType in ReferenceGraph.GetReferencedTypes(sourceType))
            {
                string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                string? referencedLayerName = ResolveContainingLayer(referencedType, contractLayers);

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

            Dictionary<string, HashSet<string>> graph = BuildSiblingReferenceGraph(siblingGroups, ancestor, executionContext);

            IReadOnlyCollection<string> ancestorCycles = ArchitectureCycleDetector.FindCycles(graph);

            allCycles.AddRange(
                ancestorCycles.Select(c => $"{ancestor}: {c}"));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return allCycles;
    }

    private static Dictionary<string, HashSet<string>> BuildSiblingReferenceGraph(
        Dictionary<string, List<Type>> siblingGroups,
        string ancestor,
        ArchitectureContractExecutionContext executionContext)
    {
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
                CollectSiblingCycleEdges(sourceType, sourceSibling, siblingGroups, ancestor, executionContext, graph);
            }
        }

        return graph;
    }

    private static void CollectSiblingCycleEdges(
        Type sourceType,
        string sourceSibling,
        Dictionary<string, List<Type>> siblingGroups,
        string ancestor,
        ArchitectureContractExecutionContext executionContext,
        Dictionary<string, HashSet<string>> graph)
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

        (IReadOnlyList<string>? explicitReferenceAssemblyPaths, ArchitectureViolation? fallbackDiagnostic) =
            ResolveProjectAwareReferenceAssemblyPaths(contract, sourceLayer, sourceRoots);

        IReadOnlyList<ArchitectureViolation> roslynViolations = new ArchitectureSourceScanner()
            .FindMethodBodyViolations(Context.RepositoryRoot, sourceLayer.Namespace,
                contract.ForbiddenCalls, executionContext, sourceRoots: sourceRoots,
                sourceLayer: sourceLayer, preprocessorSymbols: PreprocessorSymbols,
                explicitReferenceAssemblyPaths: explicitReferenceAssemblyPaths)
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = new ArchitectureIlMethodBodyScanner().FindMethodBodyViolations(
            Context.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            executionContext,
            sourceLayer: sourceLayer)
            .ToList();

        List<ArchitectureViolation> violations = ArchitectureNamespaceViolationFinder.MergeMethodBodyViolations(contract.Name, contract.Id, roslynViolations, ilViolations);

        if (fallbackDiagnostic != null)
        {
            violations.Add(fallbackDiagnostic);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // Attempts project-aware reference resolution for a method-body contract's owning discovered
    // project. Returns (null, null) when project discovery isn't configured at all, so behavior for
    // repositories that never opted into analysis.solution/analysis.projects is completely
    // unchanged. Returns a non-null diagnostic only when discovery IS configured but project-aware
    // resolution couldn't be used (no/ambiguous owning project, or Buildalyzer evaluation failed),
    // so the degraded-accuracy fallback is visible rather than silent.
    private (IReadOnlyList<string>? ReferenceAssemblyPaths, ArchitectureViolation? FallbackDiagnostic)
        ResolveProjectAwareReferenceAssemblyPaths(
            ArchitectureMethodBodyContract contract, ArchitectureLayer sourceLayer, string[]? sourceRoots)
    {
        ProjectDiscoveryResult? discovery = Context.ProjectDiscovery;
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return (null, null);
        }

        IReadOnlyList<string> matchedFiles = new ArchitectureSourceScanner()
            .FindMatchingSourceFiles(Context.RepositoryRoot, sourceLayer, sourceRoots);

        if (matchedFiles.Count == 0)
        {
            return (null, null);
        }

        ArchitectureDiscoveredProject? owningProject = ResolveOwningProject(discovery.DiscoveredProjects, matchedFiles);

        if (owningProject == null)
        {
            return (null, BuildFallbackDiagnostic(contract,
                "no single discovered project owns this contract's source files (files span zero or multiple discovered project directories)"));
        }

        string projectAbsolutePath = Path.GetFullPath(Path.Combine(Context.RepositoryRoot, owningProject.Path));
        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(projectAbsolutePath);

        if (!resolution.Succeeded)
        {
            return (null, BuildFallbackDiagnostic(contract,
                $"project '{owningProject.Path}' could not be evaluated for project-aware Roslyn analysis: {resolution.FailureReason}"));
        }

        return (resolution.Context!.ReferenceAssemblyPaths, null);
    }

    private static ArchitectureViolation BuildFallbackDiagnostic(ArchitectureMethodBodyContract contract, string reason)
    {
        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            contract.Source,
            "project-aware analysis fallback",
            new[]
            {
                $"Method-body contract '{contract.Name}' fell back to lightweight Roslyn compilation because {reason}. " +
                "Cross-project/package symbol resolution may be less accurate for this check."
            });
    }

    // A discovered project "owns" a matched source file when that project's directory is the
    // nearest (longest-prefix) ancestor directory among all discovered projects. Project-aware
    // resolution is only attempted when every matched file resolves to exactly the same owning
    // project — spanning zero or multiple projects falls back rather than guessing.
    private ArchitectureDiscoveredProject? ResolveOwningProject(
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects, IReadOnlyList<string> matchedFiles)
    {
        List<(ArchitectureDiscoveredProject Project, string Directory)> projectDirectories = discoveredProjects
            .Select(project => (project, NormalizeDirectory(Path.GetFullPath(Path.Combine(
                Context.RepositoryRoot, Path.GetDirectoryName(project.Path) ?? string.Empty)))))
            .ToList();

        HashSet<string> owningProjectPaths = new(StringComparer.OrdinalIgnoreCase);
        ArchitectureDiscoveredProject? owner = null;

        foreach (string filePath in matchedFiles)
        {
            string fileDirectory = NormalizeDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty);

            ArchitectureDiscoveredProject? bestMatch = null;
            int bestLength = -1;

            foreach ((ArchitectureDiscoveredProject candidate, string candidateDirectory) in projectDirectories)
            {
                if (fileDirectory.StartsWith(candidateDirectory, StringComparison.OrdinalIgnoreCase)
                    && candidateDirectory.Length > bestLength)
                {
                    bestMatch = candidate;
                    bestLength = candidateDirectory.Length;
                }
            }

            if (bestMatch == null)
            {
                return null;
            }

            owningProjectPaths.Add(bestMatch.Path);
            owner = bestMatch;
        }

        return owningProjectPaths.Count == 1 ? owner : null;
    }

    private static string NormalizeDirectory(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/') + "/";
    }

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return new ArchitectureAsmdefScanner().FindAsmdefViolations(contract.Name, contract.Id, Context.RepositoryRoot, contract)
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
            Type[] sourceTypes = FindTypesInLayer(sourceLayer);

            foreach (string forbiddenLayerName in contract.Layers)
            {
                if (string.Equals(sourceLayerName, forbiddenLayerName, StringComparison.Ordinal))
                {
                    continue;
                }

                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, forbiddenLayerName);
                violations.AddRange(ArchitectureNamespaceViolationFinder.FindNamespaceViolations(sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), executionContext, null, RoleIndex));
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
        Type[] sourceTypes = FindTypesInLayer(sourceLayer);
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

            violations.AddRange(new ArchitectureExternalDependencyIlScanner().FindMethodBodyViolations(
                sourceTypes,
                externalGroupName,
                externalGroup,
                executionContext));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckExternalAllowOnlyContract(ArchitectureExternalAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(Document, contract.Name, contract.Source);
        Type[] sourceTypes = FindTypesInLayer(sourceLayer);
        List<ArchitectureViolation> violations = new();

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        var allowedGroups = contract.Allowed.ToHashSet(StringComparer.Ordinal);
        IEnumerable<string> disallowedGroups = Document.ExternalDependencies.Keys
            .Where(name => !allowedGroups.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal);

        string allowedGroupsSuffix = $" (allowed groups: [{string.Join(", ", contract.Allowed)}])";

        foreach (string externalGroupName in disallowedGroups)
        {
            ArchitectureExternalDependencyGroup externalGroup = Document.ExternalDependencies[externalGroupName];

            foreach (ArchitectureViolation violation in ArchitectureExternalDependencyViolationFinder.FindViolations(
                         externalGroupName, sourceTypes, externalGroup, executionContext, contract.AllowedTypes))
            {
                violations.Add(violation with { ForbiddenNamespace = violation.ForbiddenNamespace + allowedGroupsSuffix });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
