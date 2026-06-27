using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed record ArchitectureRunnerSetup(string RepositoryRoot, ArchitectureContractRunner Runner);

public static class ArchitectureRunnerFactory
{
    public static ArchitectureContractDocument LoadDocument(
        string policyPath,
        string? baselinePath = null,
        ValidationTiming? timing = null)
    {
        ArchitectureContractDocument document;
        using (timing?.Measure("yaml_loading", indent: 1))
            document = ArchitectureContractLoader.LoadFromPath(policyPath);

        if (baselinePath != null)
        {
            using (timing?.Measure("baseline_loading", indent: 1))
            {
                ArchitectureBaselineDocument baseline = ArchitectureBaselineLoader.LoadFromPath(baselinePath);
                ArchitectureBaselineMerger.MergeAndValidate(document, baseline);
            }
        }

        return document;
    }

    public static ArchitectureRunnerSetup BuildRunner(
        ArchitectureContractDocument document,
        string policyPath,
        string? conditionSetName = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        HashSet<string>? selectedContractIds = null,
        bool enableUnmatchedIgnoreTracking = true,
        ValidationTiming? timing = null)
    {
        string repositoryRoot;
        using (timing?.Measure("root_resolution", indent: 1))
            repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);

        IReadOnlyList<string>? symbols = preprocessorSymbols;
        using (timing?.Measure("condition_set_resolution", indent: 1))
        {
            if (symbols == null &&
                !ConditionSetResolver.TryResolve(document, conditionSetName, out symbols, out string? resolveError))
            {
                throw new InvalidOperationException(resolveError);
            }
        }

        ArchitectureContractRunner runner;
        using (timing?.Measure("assembly_resolution", indent: 1))
        {
            ProjectDiscoveryResult discovery = ArchitectureProjectDiscovery.ResolveFromDocument(document, repositoryRoot);
            ApplyDiscoveryResult(document.Analysis, discovery);

            if (document.Analysis.TargetAssemblies.Count == 0 && discovery.Diagnostics.Count > 0)
            {
                string details = string.Join("; ", discovery.Diagnostics.Select(d => d.Message));
                throw new InvalidOperationException(
                    $"Architecture YAML must define analysis.target_assemblies. Project discovery did not resolve any assemblies: {details}");
            }

            ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
            ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
                resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths, discovery.Diagnostics);
            runner = new ArchitectureContractRunner(context, document, selectedContractIds,
                enableUnmatchedIgnoreTracking, preprocessorSymbols: symbols);
        }

        return new ArchitectureRunnerSetup(repositoryRoot, runner);
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
