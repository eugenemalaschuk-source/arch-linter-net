using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;

namespace ArchLinterNet.Core.Execution;

public interface IArchitectureAssemblyResolutionService
{
    ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds);
}

public sealed class ArchitectureAssemblyResolutionService : IArchitectureAssemblyResolutionService
{
    public ResolutionResult Resolve(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ProjectDiscoveryResult discovery,
        bool resolveAssemblyOutputs,
        string? mode,
        HashSet<string>? selectedContractIds)
    {
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

        return document.Analysis.TargetAssemblies.Count == 0 && projectCoverageCanReportUnresolvedProjects
            ? new ResolutionResult(Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>())
            : ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
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
}
