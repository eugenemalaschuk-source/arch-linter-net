using System.Diagnostics;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.History.Enrichment;

internal sealed class WorktreeHistoryDotNetFactProvider : IHistoryDotNetFactProvider
{
    public HistoryDotNetFactMaterialization Materialize(string repositoryPath, string resolvedTo, string policyPath)
    {
        string repositoryRoot = Path.GetFullPath(repositoryPath);
        VerifyWorktree(repositoryRoot, resolvedTo);
        string resolvedPolicyPath = Path.GetFullPath(Path.IsPathRooted(policyPath)
            ? policyPath
            : Path.Combine(repositoryRoot, policyPath));
        if (!string.Equals(Path.GetDirectoryName(resolvedPolicyPath), repositoryRoot, StringComparison.OrdinalIgnoreCase)
            && !resolvedPolicyPath.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new HistoryDotNetEnrichmentUnavailableException("policy_repository_mismatch");
        }

        ArchitectureContractDocument document;
        try
        {
            document = new ArchitecturePolicyDocumentLoader().Load(resolvedPolicyPath);
        }
        catch (Exception)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("policy_load_failed");
        }

        ProjectDiscoveryResult discovery;
        try
        {
            discovery = new ArchitectureProjectDiscoveryService().ResolveAndApply(document, repositoryRoot, resolveAssemblyOutputs: true);
        }
        catch (Exception)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("project_discovery_failed");
        }

        if (discovery.Diagnostics.Count > 0 || discovery.DiscoveredProjects.Count == 0)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("project_discovery_failed");
        }

        // Git cleanliness proves only tracked source state. The receipt/fingerprint authority is
        // what proves ignored build output was produced from that same source state.
        BuildStatePreflightResult preflight = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            repositoryRoot,
            discovery,
            new BuildStateResolvedAssemblies(Array.Empty<System.Reflection.Assembly>(), Array.Empty<string>())
            {
                ResolvedAssemblyPaths = discovery.ResolvedAssemblyPaths
            },
            BuildPreparationMode.Ordinary,
            RequestedConfiguration: document.Analysis.Configuration,
            RequestedTargetFramework: document.Analysis.TargetFramework));
        if (preflight.Blocked)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("build_state_unavailable");
        }

        ResolutionResult resolution;
        try
        {
            resolution = new ArchitectureAssemblyResolutionService().ResolvePostBuild(
                document, repositoryRoot, discovery, resolveAssemblyOutputs: true, mode: null, selectedContractIds: null);
        }
        catch (Exception)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("build_state_unavailable");
        }

        if (resolution.MissingAssemblyNames.Count > 0 || resolution.ResolvedAssemblies.Count == 0)
        {
            resolution.IsolatedLoadScope?.Dispose();
            throw new HistoryDotNetEnrichmentUnavailableException("build_state_unavailable");
        }

        using ArchitectureAnalysisContext context = new(
            repositoryRoot,
            resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames,
            resolution.AssemblyProbingPaths,
            discovery.Diagnostics,
            discovery,
            resolution.IsolatedLoadScope,
            resolution.SelectedAssemblyArtifactPaths);
        ArchitectureContractRunner runner = new(context, document);
        return new HistoryDotNetFactMaterialization(BuildPathIndex(runner.Session.SourceFileFactIndex.AllFacts, discovery));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> BuildPathIndex(
        IReadOnlyList<ArchitectureDeclaredTypeFact> facts,
        ProjectDiscoveryResult discovery)
    {
        IReadOnlyDictionary<string, string> projectsByAssembly = discovery.DiscoveredProjects
            .OrderBy(project => project.Path, StringComparer.Ordinal)
            .GroupBy(project => project.AssemblyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Path, StringComparer.Ordinal);
        return facts
            .Where(fact => fact.SourceFilePath is not null)
            .GroupBy(fact => fact.SourceFilePath!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<HistoryDotNetTypeContext>)group
                    .OrderBy(fact => fact.AssemblyName, StringComparer.Ordinal)
                    .ThenBy(fact => fact.FullTypeName, StringComparer.Ordinal)
                    .Select(fact => new HistoryDotNetTypeContext(
                        projectsByAssembly.GetValueOrDefault(fact.AssemblyName, string.Empty),
                        fact.AssemblyName,
                        fact.Namespace,
                        fact.FullTypeName,
                        fact.SimpleTypeName,
                        fact.TypeKind,
                        fact.IsAbstract))
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static void VerifyWorktree(string repositoryPath, string resolvedTo)
    {
        string head = RunGit(repositoryPath, "rev-parse", "--verify", "HEAD").Trim();
        if (!string.Equals(head, resolvedTo, StringComparison.Ordinal))
        {
            throw new HistoryDotNetEnrichmentUnavailableException("revision_mismatch");
        }

        if (RunGit(repositoryPath, "status", "--porcelain=v1", "--untracked-files=all").Length > 0)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("worktree_dirty");
        }
    }

    private static string RunGit(string repositoryPath, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repositoryPath
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException();
            string output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new HistoryDotNetEnrichmentUnavailableException("worktree_verification_failed");
            }

            return output;
        }
        catch (HistoryDotNetEnrichmentUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("worktree_verification_failed");
        }
    }
}
