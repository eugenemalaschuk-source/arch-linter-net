using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class AcyclicSiblingChecker
{
    public static List<string> Check(
        ArchitectureAcyclicSiblingContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<string> allCycles = new();

        foreach (string ancestor in contract.Ancestors)
        {
            Dictionary<string, List<Type>> siblingGroups =
                ArchitectureSiblingGraphBuilder.BuildSiblingGroups(context.AnalysisContext.TargetAssemblies, ancestor);

            if (siblingGroups.Count <= 1)
            {
                continue;
            }

            Dictionary<string, HashSet<string>> graph =
                BuildSiblingReferenceGraph(siblingGroups, ancestor, executionContext);

            IReadOnlyCollection<string> ancestorCycles = ArchitectureCycleDetector.FindCycles(graph);

            allCycles.AddRange(ancestorCycles.Select(c => $"{ancestor}: {c}"));
        }

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
        string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(sourceType) ?? string.Empty;

        foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
        {
            string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
            string? referencedSibling = ResolveSiblingGroup(siblingGroups, referencedTypeName, ancestor);

            if (referencedSibling == null || referencedSibling == sourceSibling)
            {
                continue;
            }

            if (executionContext.IsIgnored(
                    sourceTypeName,
                    referencedTypeName,
                    sourceAssembly: sourceAssembly,
                    targetAssembly: ArchitectureTypeNames.SafeAssemblyName(referencedType),
                    targetType: referencedTypeName,
                    targetMember: referencedTypeName))
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
}
