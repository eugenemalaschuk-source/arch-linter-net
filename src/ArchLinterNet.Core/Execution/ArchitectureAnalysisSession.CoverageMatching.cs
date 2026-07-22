using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private Assembly? ResolveProjectAssembly(ArchitectureDiscoveredProject project)
    {
        return Context.TargetAssemblies.FirstOrDefault(assembly =>
            string.Equals(GetAssemblyName(assembly), project.AssemblyName, StringComparison.Ordinal));
    }

    private static string GetAssemblyName(Assembly assembly)
    {
        return assembly.GetName().Name ?? assembly.FullName ?? assembly.ToString();
    }

    private static string[] GetAssemblyNamespaces(Assembly assembly)
    {
        return ArchitectureTypeScanner.GetLoadableTypes(assembly)
            .Select(ArchitectureTypeNames.SafeNamespace)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetRepresentativeType(Assembly assembly)
    {
        Type? representative = ArchitectureTypeScanner.GetLoadableTypes(assembly)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

        return representative?.FullName ?? representative?.Name ?? GetAssemblyName(assembly);
    }

    private static string[] GetAssemblyForbiddenReferences(Assembly assembly)
    {
        string representativeType = GetRepresentativeType(assembly);

        return string.IsNullOrEmpty(assembly.Location)
            ? new[] { representativeType }
            : new[] { assembly.Location, representativeType };
    }

    private static string GetAssemblyEvidence(Assembly assembly)
    {
        string representativeType = GetRepresentativeType(assembly);

        return string.IsNullOrEmpty(assembly.Location)
            ? representativeType
            : $"{assembly.Location} ({representativeType})";
    }

    private static string GetProjectEvidence(ArchitectureDiscoveredProject project, Assembly resolvedAssembly)
    {
        return $"{project.AssemblyName}: {GetRepresentativeType(resolvedAssembly)}";
    }

    private static bool MatchesAssemblyExclusion(ArchitectureCoverageExclusion exclusion, string assemblyName)
    {
        return !string.IsNullOrWhiteSpace(exclusion.Assembly)
               && string.Equals(exclusion.Assembly, assemblyName, StringComparison.Ordinal);
    }

    private static bool MatchesProjectExclusion(ArchitectureCoverageExclusion exclusion, ArchitectureDiscoveredProject project)
    {
        if (string.IsNullOrWhiteSpace(exclusion.Project))
        {
            return false;
        }

        return string.Equals(exclusion.Project, project.Path, StringComparison.Ordinal)
               || string.Equals(exclusion.Project, Path.GetFileName(project.Path), StringComparison.Ordinal);
    }

    private static bool IsCoveredByDeclaredLayers(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        return inventory.DeclaredLayers.Any(layerEntry =>
            ArchitectureLayerResolver.MatchesNamespace(layerEntry.Layer, namespaceName));
    }

    private static bool IsCoveredByExpandedTemplates(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        return inventory.ExpandedLayerTemplates.Any(expandedTemplate =>
            expandedTemplate.Layers.Any(layerNamespace =>
                ArchitectureLayerResolver.MatchesNamespace(
                    new ArchitectureLayer { Namespace = layerNamespace },
                    namespaceName)));
    }

    // Distinguishes "not included" (no declared layer's raw namespace pattern matches at all) from
    // "included then excluded" (some layer's namespace/namespace_suffix pattern matches, but a
    // layer.Exclude entry subtracted the namespace back out) - see issue #356's acceptance
    // criteria. Only reached once IsCoveredByDeclaredLayers/IsCoveredByExpandedTemplates have both
    // already returned false for namespaceName, so this never overrides a namespace that some
    // other declared layer still legitimately covers.
    //
    // Collects EVERY exclude entry that independently matches namespaceName - not just the first -
    // because overlapping patterns (a broad pattern in one layer/imported fragment plus a narrower
    // one in another) can both legitimately subtract the same namespace, and dropping all but the
    // first would silently lose provenance for the rest (PR #384 review). Order is deterministic
    // (layer name, then exclude-list position) so JSON/Testing API output is stable across runs.
    private static bool TryFindLayerExclusionReasons(
        ArchitectureCoverageInventory inventory,
        string namespaceName,
        out string reason,
        out IReadOnlyList<ArchitecturePolicySourceLocation> policyLocations)
    {
        List<(string LayerName, ArchitectureLayerExclusion Exclusion)> matches = inventory.DeclaredLayers
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .SelectMany(entry => FindMatchingExclusionsForLayer(entry, namespaceName))
            .ToList();

        if (matches.Count == 0)
        {
            reason = string.Empty;
            policyLocations = Array.Empty<ArchitecturePolicySourceLocation>();
            return false;
        }

        reason = string.Join("; ", matches.Select(match => DescribeLayerExclusionMatch(match.LayerName, match.Exclusion)));
        policyLocations = matches
            .Select(match => match.Exclusion.PolicyLocation)
            .Where(location => location is not null)
            .Select(location => location!)
            .ToList();
        return true;
    }

    private static IEnumerable<(string LayerName, ArchitectureLayerExclusion Exclusion)> FindMatchingExclusionsForLayer(
        ArchitectureCoverageLayerEntry layerEntry, string namespaceName)
    {
        ArchitectureLayer layer = layerEntry.Layer;
        if (layer.Exclude.Count == 0
            || !ArchitectureLayerResolver.MatchNamespaceIncludeOnly(layer, namespaceName).Matched)
        {
            return Array.Empty<(string, ArchitectureLayerExclusion)>();
        }

        return layer.Exclude
            .Where(exclusion => ArchitectureLayerResolver.ExclusionMatches(exclusion, namespaceName))
            .Select(exclusion => (layerEntry.Name, exclusion));
    }

    private static string DescribeLayerExclusionMatch(string layerName, ArchitectureLayerExclusion exclusion)
    {
        return string.IsNullOrEmpty(exclusion.NamespaceSuffix)
            ? $"excluded by layer '{layerName}' exclude entry '{exclusion.Namespace}'"
            : $"excluded by layer '{layerName}' exclude entry '{exclusion.Namespace}' " +
              $"(namespace_suffix: {exclusion.NamespaceSuffix})";
    }

    private static bool MatchesNamespaceRoot(ArchitectureCoverageRoot root, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(root.Namespace))
        {
            return false;
        }

        return ArchitectureLayerResolver.MatchesNamespace(
            new ArchitectureLayer
            {
                Namespace = root.Namespace,
                NamespaceSuffix = root.NamespaceSuffix
            },
            namespaceName);
    }

    private static bool MatchesNamespaceExclusion(ArchitectureCoverageExclusion exclusion, string namespaceName)
    {
        if (!string.IsNullOrWhiteSpace(exclusion.Namespace))
        {
            return ArchitectureLayerResolver.MatchesNamespace(
                new ArchitectureLayer
                {
                    Namespace = exclusion.Namespace,
                    NamespaceSuffix = exclusion.NamespaceSuffix
                },
                namespaceName);
        }

        if (!string.IsNullOrWhiteSpace(exclusion.NamespaceSuffix))
        {
            return string.Equals(namespaceName, exclusion.NamespaceSuffix, StringComparison.Ordinal)
                   || namespaceName.EndsWith("." + exclusion.NamespaceSuffix, StringComparison.Ordinal);
        }

        return false;
    }
}
