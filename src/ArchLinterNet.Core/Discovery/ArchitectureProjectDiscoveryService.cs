using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Discovery;

public interface IArchitectureProjectDiscoveryService
{
    ProjectDiscoveryResult ResolveAndApply(
        ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs);
}

public sealed class ArchitectureProjectDiscoveryService : IArchitectureProjectDiscoveryService
{
    public ProjectDiscoveryResult ResolveAndApply(
        ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs)
    {
        ProjectDiscoveryResult discovery = ArchitectureProjectDiscovery.ResolveFromDocument(
            document, repositoryRoot, resolveAssemblyOutputs);
        ApplyDiscoveryResult(document.Analysis, discovery);
        return discovery;
    }

    private static void ApplyDiscoveryResult(ArchitectureAnalysisConfiguration analysis, ProjectDiscoveryResult discovery)
    {
        bool seedAssemblies = analysis.TargetAssemblies.Count == 0 && discovery.TargetAssemblyNames.Count > 0;

        if (seedAssemblies)
        {
            analysis.TargetAssemblies = discovery.TargetAssemblyNames.ToList();
            analysis.AssemblySearchPaths = analysis.AssemblySearchPaths
                .Concat(discovery.AssemblySearchPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (analysis.SourceRoots.Count == 0 && discovery.SourceRoots.Count > 0)
        {
            analysis.SourceRoots = discovery.SourceRoots.ToList();
        }
    }
}
