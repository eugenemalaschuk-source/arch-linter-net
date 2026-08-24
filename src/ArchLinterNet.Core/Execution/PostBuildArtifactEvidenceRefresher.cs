using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// Keeps Core.Validation on the existing Execution seam while converting the successful build's
// receipt diagnostics into a fresh metadata-only closure for lazy runner materialization.
internal static class PostBuildArtifactEvidenceRefresher
{
    internal static ArchitectureRunnerPreparation Refresh(
        ArchitectureContractDocument document,
        ArchitectureRunnerPreparation preparation,
        BuildStatePreflightResult preflight,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> receiptVerifiedPaths = preflight.Diagnostics
            .Where(diagnostic => diagnostic.State == BuildStatePreflightState.Current
                && !string.IsNullOrWhiteSpace(diagnostic.Evidence.ExpectedOutputPath))
            .GroupBy(diagnostic => diagnostic.Evidence.AssemblyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => Path.GetFullPath(group.First().Evidence.ExpectedOutputPath!),
                StringComparer.Ordinal);
        if (receiptVerifiedPaths.Count == 0)
        {
            return preparation with
            {
                CapturedArtifactContentDigests = PreparedArtifactEvidence.CaptureDigests(
                    preparation.SelectedAssemblyArtifactPaths, cancellationToken)
            };
        }

        Dictionary<string, string> resolvedPaths = new(
            preparation.ProjectDiscovery.ResolvedAssemblyPaths, StringComparer.Ordinal);
        foreach ((string assemblyName, string path) in receiptVerifiedPaths)
        {
            resolvedPaths[assemblyName] = path;
        }

        HashSet<string> receiptVerifiedProjectPaths = preparation.ProjectDiscovery.DiscoveredProjects
            .Where(project => receiptVerifiedPaths.ContainsKey(project.AssemblyName))
            .Select(project => Path.GetFullPath(Path.Combine(preparation.RepositoryRoot, project.Path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ProjectDiscoveryResult postBuildDiscovery = preparation.ProjectDiscovery with
        {
            ResolvedAssemblyPaths = resolvedPaths,
            Diagnostics = preparation.ProjectDiscovery.Diagnostics
                .Where(diagnostic => diagnostic.Kind != "stale project build output"
                    || !receiptVerifiedProjectPaths.Contains(Path.GetFullPath(diagnostic.Subject)))
                .ToArray()
        };
        IReadOnlyList<string> roots = document.Analysis.TargetAssemblies
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .Where(resolvedPaths.ContainsKey)
            .Select(name => resolvedPaths[name])
            .ToArray();
        IReadOnlyList<string> missing = preparation.MissingAssemblyNames
            .Where(name => !receiptVerifiedPaths.ContainsKey(name))
            .ToArray();
        (IReadOnlyList<string> closure, bool closureComplete) =
            PreparedArtifactEvidence.BuildMetadataReferenceClosure(roots, postBuildDiscovery, cancellationToken);

        return preparation with
        {
            ProjectDiscovery = postBuildDiscovery,
            SelectedAssemblyArtifactPaths = closure,
            CapturedArtifactContentDigests = PreparedArtifactEvidence.CaptureDigests(closure, cancellationToken),
            MissingAssemblyNames = missing,
            IsMetadataReferenceClosureComplete = closureComplete
        };
    }
}
