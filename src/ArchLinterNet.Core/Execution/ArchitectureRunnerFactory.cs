using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;

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
            ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
            ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
                resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);
            runner = new ArchitectureContractRunner(context, document, selectedContractIds,
                enableUnmatchedIgnoreTracking, preprocessorSymbols: symbols);
        }

        return new ArchitectureRunnerSetup(repositoryRoot, runner);
    }
}
