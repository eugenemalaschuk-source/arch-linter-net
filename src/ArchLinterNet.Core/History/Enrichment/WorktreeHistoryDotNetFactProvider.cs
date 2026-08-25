using System.Diagnostics;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.History.Enrichment;

internal sealed partial class WorktreeHistoryDotNetFactProvider : IHistoryDotNetFactProvider
{
    private const string BuildStateUnavailableReason = "build_state_unavailable";

    private const string PortableRelativePolicyPathPattern =
        @"^(?:(?!\.\.(?:[/\\]|$)|\.(?:[/\\]|$))[^<>:""|?*\u0000-\u001F])+$";

    [GeneratedRegex(PortableRelativePolicyPathPattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PortableRelativePolicyPathRegex();

    public HistoryDotNetFactMaterialization Materialize(string repositoryPath, string resolvedTo, string policyPath)
    {
        string repositoryRoot = Path.GetFullPath(repositoryPath);
        VerifyWorktree(repositoryRoot, resolvedTo);
        string resolvedPolicyPath = ResolveRepositoryPolicyPath(repositoryRoot, policyPath);

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

        string? requestedTargetFramework = string.IsNullOrWhiteSpace(document.Analysis.TargetFramework)
            ? null
            : document.Analysis.TargetFramework;

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
            RequestedTargetFramework: requestedTargetFramework));
        if (preflight.Blocked)
        {
            throw new HistoryDotNetEnrichmentUnavailableException(BuildStateUnavailableReason);
        }

        IReadOnlyDictionary<string, string> verifiedArtifactDigests = RequireVerifiedArtifactDigests(preflight, discovery);

        ResolutionResult resolution;
        try
        {
            resolution = new ArchitectureAssemblyResolutionService().ResolvePostBuild(
                document, repositoryRoot, discovery, resolveAssemblyOutputs: true, mode: null, selectedContractIds: null,
                expectedArtifactContentDigests: verifiedArtifactDigests);
        }
        catch (Exception)
        {
            throw new HistoryDotNetEnrichmentUnavailableException(BuildStateUnavailableReason);
        }

        if (resolution.MissingAssemblyNames.Count > 0 || resolution.ResolvedAssemblies.Count == 0)
        {
            resolution.IsolatedLoadScope?.Dispose();
            throw new HistoryDotNetEnrichmentUnavailableException(BuildStateUnavailableReason);
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
        IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> facts =
            BuildPathIndex(runner.Session.SourceFileFactIndex.AllFacts, discovery);
        VerifyWorktree(repositoryRoot, resolvedTo);
        return new HistoryDotNetFactMaterialization(facts);
    }

    private static string ResolveRepositoryPolicyPath(string repositoryRoot, string policyPath)
    {
        string candidate = Path.GetFullPath(Path.IsPathRooted(policyPath)
            ? policyPath
            : Path.Combine(repositoryRoot, policyPath));
        string relative = Path.GetRelativePath(repositoryRoot, candidate);
        bool outsideRepository = Path.IsPathRooted(relative)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        bool portableRelativePath = !string.Equals(relative, ".", StringComparison.Ordinal)
            && PortableRelativePolicyPathRegex().IsMatch(relative);
        if (outsideRepository || !portableRelativePath)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("policy_repository_mismatch");
        }

        return Path.GetFullPath(Path.Combine(repositoryRoot, relative));
    }

    private static IReadOnlyDictionary<string, string> RequireVerifiedArtifactDigests(
        BuildStatePreflightResult preflight,
        ProjectDiscoveryResult discovery)
    {
        foreach (string path in discovery.ResolvedAssemblyPaths.Values)
        {
            string fullPath = Path.GetFullPath(path);
            if (!preflight.VerifiedArtifactContentDigests.ContainsKey(fullPath))
            {
                throw new HistoryDotNetEnrichmentUnavailableException(BuildStateUnavailableReason);
            }
        }

        return preflight.VerifiedArtifactContentDigests;
    }

    private static Dictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> BuildPathIndex(
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
        string head = RunGit(repositoryPath, readHead: true).Trim();
        if (!string.Equals(head, resolvedTo, StringComparison.Ordinal))
        {
            throw new HistoryDotNetEnrichmentUnavailableException("revision_mismatch");
        }

        if (RunGit(repositoryPath, readHead: false).Length > 0)
        {
            throw new HistoryDotNetEnrichmentUnavailableException("worktree_dirty");
        }
    }

    private static string RunGit(string repositoryPath, bool readHead)
    {
        try
        {
            ProcessStartInfo startInfo = new(ResolveGitExecutablePath())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repositoryPath
            };
            if (readHead)
            {
                startInfo.ArgumentList.Add("rev-parse");
                startInfo.ArgumentList.Add("--verify");
                startInfo.ArgumentList.Add("HEAD");
            }
            else
            {
                startInfo.ArgumentList.Add("status");
                startInfo.ArgumentList.Add("--porcelain=v1");
                startInfo.ArgumentList.Add("--untracked-files=all");
            }

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

    private static string ResolveGitExecutablePath()
    {
        string fileName = OperatingSystem.IsWindows() ? "git.exe" : "git";
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (string directory in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        throw new HistoryDotNetEnrichmentUnavailableException("worktree_verification_failed");
    }
}
