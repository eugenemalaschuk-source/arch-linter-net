using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// The declared-location test shared by every family that expresses "this type must (not) live
// here": type_placement, attribute_usage, interface_implementation and composition all resolve
// layers/namespace patterns/assembly names the same way, and must keep agreeing.
internal static class CheckerLocationAllowance
{
    public static bool IsAllowedLocation(
        string actualNamespace,
        string actualAssemblyName,
        IReadOnlyList<ArchitectureLayer> allowedLayers,
        IReadOnlyList<string> allowedNamespacePatterns,
        HashSet<string> allowedAssemblyNames)
    {
        if (allowedLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, actualNamespace)))
        {
            return true;
        }

        if (allowedNamespacePatterns.Any(pattern =>
                ArchitectureLayerResolver.MatchesNamespacePattern(actualNamespace, pattern)))
        {
            return true;
        }

        return allowedAssemblyNames.Contains(actualAssemblyName);
    }

    // Resolves an "allowed only in ..." / "must reside in ..." assembly set from the contract's
    // literal assembly names plus the assembly names of its named projects.
    public static HashSet<string> ResolveAssemblyNames(
        ArchitectureCheckerContext context, List<string> assemblyNames, List<string> projectNames)
    {
        HashSet<string> resolved = new(assemblyNames, StringComparer.Ordinal);
        foreach (string resolvedAssemblyName in context.ResolveProjectAssemblyNames(projectNames))
        {
            resolved.Add(resolvedAssemblyName);
        }

        return resolved;
    }

    public static string DescribeLocation(
        IReadOnlyList<string> layers,
        IReadOnlyList<string> namespaces,
        IReadOnlyList<string> projects,
        IReadOnlyList<string> assemblies)
    {
        List<string> parts = new();
        if (layers.Count > 0)
        {
            parts.Add($"layers: [{string.Join(", ", layers)}]");
        }

        if (namespaces.Count > 0)
        {
            parts.Add($"namespaces: [{string.Join(", ", namespaces)}]");
        }

        if (projects.Count > 0)
        {
            parts.Add($"projects: [{string.Join(", ", projects)}]");
        }

        if (assemblies.Count > 0)
        {
            parts.Add($"assemblies: [{string.Join(", ", assemblies)}]");
        }

        return string.Join("; ", parts);
    }
}
