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
