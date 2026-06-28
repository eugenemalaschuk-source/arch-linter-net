using System.Reflection;
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

        ValidateImplementedCoverageScopes(document);

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
        ValidationTiming? timing = null,
        string? mode = null)
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
            bool resolveAssemblyOutputs = document.Analysis.TargetAssemblies.Count == 0;
            ProjectDiscoveryResult discovery = ArchitectureProjectDiscovery.ResolveFromDocument(
                document, repositoryRoot, resolveAssemblyOutputs);
            ApplyDiscoveryResult(document.Analysis, discovery);

            // A scope: project coverage contract needs every discovered project to reach
            // CheckProjectCoverageContract — including ones whose build output is missing, stale,
            // or ambiguous and therefore never resolved to a target assembly — so it can classify
            // them as "unknown"/"unresolved project" instead of nothing at all. Skip the
            // no-assemblies-resolved hard-fail in that case rather than erroring out before the
            // coverage engine ever runs.
            bool projectCoverageCanReportUnresolvedProjects =
                discovery.DiscoveredProjects.Count > 0
                && HasProjectScopeCoverageContract(document, mode, selectedContractIds);

            if (resolveAssemblyOutputs && document.Analysis.TargetAssemblies.Count == 0
                && discovery.Diagnostics.Count > 0 && !projectCoverageCanReportUnresolvedProjects)
            {
                string details = string.Join("; ", discovery.Diagnostics.Select(d => d.Message));
                throw new InvalidOperationException(
                    $"Architecture YAML must define analysis.target_assemblies. Project discovery did not resolve any assemblies: {details}");
            }

            ResolutionResult resolution = document.Analysis.TargetAssemblies.Count == 0 && projectCoverageCanReportUnresolvedProjects
                ? new ResolutionResult(Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>())
                : ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
            ProjectDiscoveryResult? attemptedDiscovery = ReferenceEquals(discovery, ProjectDiscoveryResult.Empty)
                ? null
                : discovery;
            ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
                resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths, discovery.Diagnostics, attemptedDiscovery);
            runner = new ArchitectureContractRunner(context, document, selectedContractIds,
                enableUnmatchedIgnoreTracking, preprocessorSymbols: symbols);
        }

        return new ArchitectureRunnerSetup(repositoryRoot, runner);
    }

    // mode is null for callers (e.g. ArchitectureBaselineService with request.Mode "all") that
    // don't pin a single mode up front; in that case both strict and audit project-scope coverage
    // contracts are considered, mirroring how such callers later execute both modes themselves.
    private static bool HasProjectScopeCoverageContract(
        ArchitectureContractDocument document, string? mode, HashSet<string>? selectedContractIds)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        IEnumerable<IArchitectureContract> coverageContracts = mode != null
            ? catalog.ContractsFor(mode, "coverage")
            : catalog.ContractsFor("strict", "coverage").Concat(catalog.ContractsFor("audit", "coverage"));

        return coverageContracts
            .OfType<ArchitectureCoverageContract>()
            .Any(contract => string.Equals(contract.Scope, "project", StringComparison.Ordinal)
                && IsContractIdSelected(contract.Id, selectedContractIds));
    }

    private static bool IsContractIdSelected(string? contractId, HashSet<string>? selectedContractIds)
    {
        return selectedContractIds == null || selectedContractIds.Count == 0
            || (contractId != null && selectedContractIds.Contains(contractId));
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

    private static readonly string[] _implementedCoverageScopes = { "namespace", "rule_input", "project", "assembly" };

    private static void ValidateImplementedCoverageScopes(ArchitectureContractDocument document)
    {
        List<ArchitectureCoverageContract> unsupported = document.Contracts.StrictCoverage
            .Concat(document.Contracts.AuditCoverage)
            .Where(contract => !_implementedCoverageScopes.Contains(contract.Scope, StringComparer.Ordinal))
            .ToList();

        if (unsupported.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", unsupported.Select(contract => $"{contract.Name} ({contract.Scope})"));
        throw new InvalidOperationException(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', or 'assembly' are " +
            $"implemented right now. Unsupported coverage contract scopes: {details}. Dependency_edge " +
            "coverage remains reserved for a follow-up issue.");
    }
}
