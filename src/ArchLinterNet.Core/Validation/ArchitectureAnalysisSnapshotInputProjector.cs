using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Projects the immutable input/review paths already owned by a snapshot. None of these methods
// materializes a runner or stores lifecycle state; prepared metadata remains usable for lazy and
// cache-hit snapshots whose setup has not been materialized.
internal static class ArchitectureAnalysisSnapshotInputProjector
{
    internal static IReadOnlyList<string> GetPolicyImportPaths(
        ArchitectureContractDocument document,
        string repositoryRoot) => document.Provenance.Sources
        .Select(source => Path.GetFullPath(Path.Combine(repositoryRoot, source.SourcePath)))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    internal static IReadOnlyList<string> GetResolvedAssemblyPaths(
        ArchitectureRunnerSetup? setup,
        IReadOnlyList<string> preparedArtifactPaths) => GetSelectedAssemblyArtifactPaths(setup, preparedArtifactPaths)
        .Concat(setup?.Runner.Session.Context.TargetAssemblies
            .Select(SafeAssemblyLocation)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetFullPath(path!))
            ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    internal static IReadOnlyList<string> GetDiscoveredProjectPaths(
        ArchitectureRunnerSetup? setup,
        IReadOnlyList<string> preparedProjectPaths) =>
        setup?.Runner.Session.Context.DiscoveredProjectPaths ?? preparedProjectPaths;

    internal static IReadOnlyList<string> GetConsumedInputPaths(
        BuildStatePreflightResult preflight,
        ArchitectureRunnerSetup? setup)
    {
        IReadOnlyList<string> sessionInputs = setup is null
            ? Array.Empty<string>()
            : setup.Runner.Session.Context.GetConsumedInputPaths()
                .Concat(setup.Runner.Session.SourceFileFactIndex.ConsumedSourceInputPaths)
                .ToArray();
        return preflight.ConsumedInputPaths
            .Concat(sessionInputs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<string> GetProfileInputPaths(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ArchitectureRunnerSetup? setup,
        IReadOnlyList<string> preparedArtifactPaths,
        IReadOnlyList<string> preparedProjectPaths) => GetPolicyImportPaths(document, repositoryRoot)
        .Concat(GetResolvedAssemblyPaths(setup, preparedArtifactPaths)
            .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
        .Concat(GetDiscoveredProjectPaths(setup, preparedProjectPaths))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static IReadOnlyList<string> GetCacheProjectPaths(
        ArchitectureContractDocument document,
        string repositoryRoot,
        ArchitectureRunnerSetup? setup,
        IReadOnlyList<string> preparedProjectPaths)
    {
        IReadOnlyList<string> discoveredPaths = GetDiscoveredProjectPaths(setup, preparedProjectPaths);
        if (discoveredPaths.Count > 0 || document.Analysis.Projects.Count == 0)
        {
            return discoveredPaths;
        }

        return document.Analysis.Projects
            .Select(path => Path.GetFullPath(Path.Combine(repositoryRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetSelectedAssemblyArtifactPaths(
        ArchitectureRunnerSetup? setup,
        IReadOnlyList<string> preparedArtifactPaths) =>
        setup?.Runner.Session.Context.SelectedAssemblyArtifactPaths ?? preparedArtifactPaths;

    private static string? SafeAssemblyLocation(Assembly assembly)
    {
        try
        {
            return assembly.Location;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
