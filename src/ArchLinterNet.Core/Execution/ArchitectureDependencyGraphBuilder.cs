using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Builds a normalized ArchitectureDependencyGraph as a *view* over data the engine already computes
// for validation (ArchitectureReferenceGraph, ArchitectureCoverageInventory, the assembly-dependency
// lookup, and the violation collection from a normal contract run) rather than running a second,
// independent analysis pass that could drift from what `validate` reports.
internal static class ArchitectureDependencyGraphBuilder
{
    public static ArchitectureDependencyGraph Build(
        ArchitectureAnalysisSession session,
        ArchitectureGraphLevel level,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(violations);

        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds = new(StringComparer.Ordinal);
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds = new();

        Func<string, string?> resolveId = level switch
        {
            ArchitectureGraphLevel.Namespace => BuildNamespaceIdResolver(session, nodeKinds),
            ArchitectureGraphLevel.Type => BuildTypeIdResolver(session, nodeKinds, edgeContractIds),
            ArchitectureGraphLevel.Assembly => BuildAssemblyIdResolver(session, nodeKinds, edgeContractIds),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown graph level."),
        };

        if (level == ArchitectureGraphLevel.Namespace)
        {
            SeedNamespaceEdges(session, nodeKinds, edgeContractIds);
        }

        if (level != ArchitectureGraphLevel.Assembly)
        {
            SeedExternalNodesAndEdges(session, resolveId, nodeKinds, edgeContractIds);
        }

        OverlayViolations(violations, level, resolveId, nodeKinds, edgeContractIds);

        return ToGraph(nodeKinds, edgeContractIds);
    }

    private static void SeedNamespaceEdges(
        ArchitectureAnalysisSession session,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        ArchitectureCoverageInventory inventory = session.BuildCoverageInventory(session.Document);

        foreach (ArchitectureCoverageNamespaceEntry entry in inventory.Namespaces)
        {
            nodeKinds[entry.Namespace] = ArchitectureGraphNodeKind.Namespace;
        }

        foreach (ArchitectureCoverageDependencyEdge edge in inventory.DependencyEdges)
        {
            GetOrAddEdgeSet(edgeContractIds, edge.SourceNamespace, edge.TargetNamespace);
        }
    }

    // Every declared external dependency group becomes a node, and every first-party reference
    // matching a group's pattern becomes an edge, regardless of whether any contract currently
    // checks that group. This reuses the same two detectors CheckExternalContract composes for
    // validation — ArchitectureExternalDependencyViolationFinder (field/property/parameter/base-type
    // references) and ArchitectureExternalDependencyIlScanner (method-body references, the common
    // case for something like a bare `JsonSerializer.Serialize(...)` call) — run unconditionally
    // over every declared group and every first-party type, with no ignored_violations applied,
    // rather than only when an active contract happens to raise a violation. That keeps `graph`/
    // `explain` showing the real dependency (with an empty ContractIds) even when it is currently
    // allowed or untracked by any contract.
    private static void SeedExternalNodesAndEdges(
        ArchitectureAnalysisSession session,
        Func<string, string?> resolveId,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        Dictionary<string, ArchitectureExternalDependencyGroup> externalGroups = session.Document.ExternalDependencies;
        if (externalGroups.Count == 0)
        {
            return;
        }

        foreach (string groupName in externalGroups.Keys)
        {
            nodeKinds[groupName] = ArchitectureGraphNodeKind.External;
        }

        Type[] allTypes = session.TypeIndex.AllTypes();
        ArchitectureContractExecutionContext executionContext = new(
            "graph-seed", null, Array.Empty<ArchitectureIgnoredViolation>(), enableUnmatchedIgnoreTracking: false, null, null);
        ArchitectureExternalDependencyIlScanner ilScanner = new();

        foreach ((string groupName, ArchitectureExternalDependencyGroup group) in externalGroups)
        {
            IEnumerable<ArchitectureViolation> matches = ArchitectureExternalDependencyViolationFinder.FindViolations(
                    groupName, allTypes, group, executionContext)
                .Concat(ilScanner.FindMethodBodyViolations(allTypes, groupName, group, executionContext));

            foreach (ArchitectureViolation match in matches)
            {
                string? sourceId = resolveId(match.SourceType);
                if (sourceId != null)
                {
                    GetOrAddEdgeSet(edgeContractIds, sourceId, groupName);
                }
            }
        }
    }

    private static Func<string, string?> BuildNamespaceIdResolver(
        ArchitectureAnalysisSession session,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds)
    {
        Dictionary<string, string> namespaceByTypeFullName = new(StringComparer.Ordinal);

        foreach (Type type in session.TypeIndex.AllTypes())
        {
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            namespaceByTypeFullName[fullName] = ArchitectureTypeNames.SafeNamespace(type);
        }

        return name => namespaceByTypeFullName.TryGetValue(name, out string? ns) && nodeKinds.ContainsKey(ns)
            ? ns
            : null;
    }

    private static Func<string, string?> BuildTypeIdResolver(
        ArchitectureAnalysisSession session,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        Type[] allTypes = session.TypeIndex.AllTypes();
        HashSet<Type> firstPartyTypes = new(allTypes);
        HashSet<string> typeFullNames = new(StringComparer.Ordinal);

        PopulateTypeNodes(allTypes, nodeKinds, typeFullNames);
        PopulateTypeEdges(session, allTypes, firstPartyTypes, edgeContractIds);

        return name => typeFullNames.Contains(name) ? name : null;
    }

    private static void PopulateTypeNodes(
        Type[] allTypes,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        HashSet<string> typeFullNames)
    {
        foreach (Type type in allTypes)
        {
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            typeFullNames.Add(fullName);
            nodeKinds[fullName] = ArchitectureGraphNodeKind.Type;
        }
    }

    private static void PopulateTypeEdges(
        ArchitectureAnalysisSession session,
        Type[] allTypes,
        HashSet<Type> firstPartyTypes,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        foreach (Type source in allTypes)
        {
            string sourceId = ArchitectureTypeNames.SafeFullName(source);
            if (string.IsNullOrEmpty(sourceId))
            {
                continue;
            }

            foreach (Type target in session.ReferenceGraph.GetReferencedTypes(source))
            {
                if (ReferenceEquals(source, target) || !firstPartyTypes.Contains(target))
                {
                    continue;
                }

                string targetId = ArchitectureTypeNames.SafeFullName(target);
                if (string.IsNullOrEmpty(targetId) || string.Equals(sourceId, targetId, StringComparison.Ordinal))
                {
                    continue;
                }

                GetOrAddEdgeSet(edgeContractIds, sourceId, targetId);
            }
        }
    }

    private static Func<string, string?> BuildAssemblyIdResolver(
        ArchitectureAnalysisSession session,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        Dictionary<string, Assembly> assembliesByName = session.Context.TargetAssemblies
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty)
            .Where(group => !string.IsNullOrEmpty(group.Key))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (string name in assembliesByName.Keys)
        {
            nodeKinds[name] = ArchitectureGraphNodeKind.Assembly;
        }

        foreach ((string sourceId, Assembly assembly) in assembliesByName)
        {
            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                string targetId = referenced.Name ?? string.Empty;
                if (string.IsNullOrEmpty(targetId) || string.Equals(sourceId, targetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!assembliesByName.ContainsKey(targetId))
                {
                    continue;
                }

                GetOrAddEdgeSet(edgeContractIds, sourceId, targetId);
            }
        }

        return name => assembliesByName.ContainsKey(name) ? name : null;
    }

    private static void OverlayViolations(
        IReadOnlyCollection<ArchitectureViolation> violations,
        ArchitectureGraphLevel level,
        Func<string, string?> resolveId,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        foreach (ArchitectureViolation violation in violations)
        {
            if (violation.ContractId == null)
            {
                continue;
            }

            string? sourceId = resolveId(violation.SourceType);
            if (sourceId == null)
            {
                continue;
            }

            OverlayViolation(violation, level, resolveId, nodeKinds, edgeContractIds, sourceId, violation.ContractId);
        }
    }

    private static void OverlayViolation(
        ArchitectureViolation violation,
        ArchitectureGraphLevel level,
        Func<string, string?> resolveId,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string sourceId,
        string contractId)
    {
        if (violation.Payload is ExternalDependencyPayload externalDependency)
        {
            OverlayExternalDependencyViolation(level, nodeKinds, edgeContractIds, sourceId, contractId, externalDependency);
            return;
        }

        if (violation.Payload is ConfigurationPayload { DependencyPaths.Count: > 0 } configuration)
        {
            OverlayConfigurationDependencyPaths(resolveId, edgeContractIds, contractId, configuration);
            return;
        }

        OverlayForbiddenReferenceTargets(violation, resolveId, edgeContractIds, sourceId, contractId);
    }

    private static void OverlayExternalDependencyViolation(
        ArchitectureGraphLevel level,
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string sourceId,
        string contractId,
        ExternalDependencyPayload externalDependency)
    {
        if (level == ArchitectureGraphLevel.Assembly)
        {
            return;
        }

        string externalId = externalDependency.ForbiddenExternalGroup;
        nodeKinds.TryAdd(externalId, ArchitectureGraphNodeKind.External);
        TagEdge(edgeContractIds, sourceId, externalId, contractId);
    }

    private static void OverlayConfigurationDependencyPaths(
        Func<string, string?> resolveId,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string contractId,
        ConfigurationPayload configuration)
    {
        foreach (IReadOnlyCollection<string> path in configuration.DependencyPaths!)
        {
            string[] hops = path.ToArray();
            for (int i = 0; i < hops.Length - 1; i++)
            {
                string? hopSource = resolveId(hops[i]);
                string? hopTarget = resolveId(hops[i + 1]);
                if (hopSource != null && hopTarget != null)
                {
                    TagEdge(edgeContractIds, hopSource, hopTarget, contractId);
                }
            }
        }
    }

    private static void OverlayForbiddenReferenceTargets(
        ArchitectureViolation violation,
        Func<string, string?> resolveId,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string sourceId,
        string contractId)
    {
        List<string> targetIds = violation.ForbiddenReferences
            .Select(resolveId)
            .Where(id => id != null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (targetIds.Count == 0)
        {
            string? fallback = resolveId(violation.ForbiddenNamespace);
            if (fallback != null)
            {
                targetIds.Add(fallback);
            }
        }

        foreach (string targetId in targetIds)
        {
            TagEdge(edgeContractIds, sourceId, targetId, contractId);
        }
    }

    private static HashSet<string> GetOrAddEdgeSet(
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string sourceId,
        string targetId)
    {
        (string sourceId, string targetId) key = (sourceId, targetId);
        if (!edgeContractIds.TryGetValue(key, out HashSet<string>? contractIds))
        {
            contractIds = new HashSet<string>(StringComparer.Ordinal);
            edgeContractIds[key] = contractIds;
        }

        return contractIds;
    }

    private static void TagEdge(
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds,
        string sourceId,
        string targetId,
        string contractId)
    {
        GetOrAddEdgeSet(edgeContractIds, sourceId, targetId).Add(contractId);
    }

    private static ArchitectureDependencyGraph ToGraph(
        Dictionary<string, ArchitectureGraphNodeKind> nodeKinds,
        Dictionary<(string Source, string Target), HashSet<string>> edgeContractIds)
    {
        List<ArchitectureGraphNode> nodes = nodeKinds
            .Select(pair => new ArchitectureGraphNode(pair.Key, pair.Value))
            .OrderBy(node => (int)node.Kind)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        List<ArchitectureGraphEdge> edges = edgeContractIds
            .Select(pair => new ArchitectureGraphEdge(
                pair.Key.Source,
                pair.Key.Target,
                nodeKinds[pair.Key.Source],
                nodeKinds[pair.Key.Target],
                pair.Value.OrderBy(id => id, StringComparer.Ordinal).ToList()))
            .OrderBy(edge => edge.SourceId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
            .ThenBy(edge => (int)edge.SourceKind)
            .ToList();

        return new ArchitectureDependencyGraph(nodes, edges);
    }
}
