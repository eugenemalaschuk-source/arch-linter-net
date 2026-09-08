using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

// Owns the --ensure-built preparation state machine: graph selection, standard solution
// generation, post-build output resolution, and receipt publication. Child-process execution and
// runtime-specific driver generation live in the purpose-named collaborators beside this class.
internal static class BuildStateRuntimeBuildPreparation
{
    private const string ContractName = "build-state-preflight";

    internal static BuildStatePreflightResult EnsureBuilt(BuildStatePreflightRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        BuildStatePreflightDiagnostic? failure = BuildStateRuntimeBuildProcessExecutor.InvokeGraphBuild(request);
        request.CancellationToken.ThrowIfCancellationRequested();
        if (failure != null)
        {
            return new BuildStatePreflightResult(new[] { failure });
        }

        // The caller's ResolutionResult snapshot predates this build and cannot see assemblies
        // that did not exist yet — re-resolve from the freshly built output before evaluating,
        // otherwise a first-time build would always evaluate as missing-artifact.
        BuildStatePreflightRequest postBuildRequest = request with { Resolution = ResolveBuiltAssemblies(request) };

        BuildStatePreflightResult postBuildEvaluation = BuildStatePreflightEvaluator.Evaluate(postBuildRequest);
        request.CancellationToken.ThrowIfCancellationRequested();
        WriteReceiptsForCurrentArtifacts(postBuildRequest, postBuildEvaluation);

        request.CancellationToken.ThrowIfCancellationRequested();

        // Verified artifacts are re-checked once more from the freshly written receipts so the
        // TOCTOU window between build completion and receipt materialization cannot admit a
        // digest/identity change without aborting the session.
        return BuildStatePreflightEvaluator.Evaluate(postBuildRequest);
    }

    // Building the whole selected graph once — not once per project, and not once per independent
    // root — means generating a single temporary .slnx solution file listing every discovered
    // project and invoking `dotnet build` on it exactly once. A solution build shares one MSBuild
    // graph across every listed entry point: a project referenced by more than one other listed
    // project (or by more than one independent root) is still built exactly once, and multiple
    // otherwise-unconnected project trees are covered by the same single invocation instead of one
    // invocation per tree.
    internal static string WriteTemporaryGraphSolution(BuildStatePreflightRequest request)
    {
        IEnumerable<string> projectEntries = SelectRelevantProjectsWithTransitiveReferences(request)
            .Select(project => BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path))
            .Distinct(StringComparer.Ordinal)
            .Select(absolutePath => $"    <Project Path=\"{System.Security.SecurityElement.Escape(absolutePath)}\" />");

        string content = "<Solution>" + Environment.NewLine
            + "  <Folder Name=\"/build-state-preflight/\">" + Environment.NewLine
            + string.Join(Environment.NewLine, projectEntries) + Environment.NewLine
            + "  </Folder>" + Environment.NewLine
            + "</Solution>" + Environment.NewLine;

        string path = Path.Combine(Path.GetTempPath(), $"archlinternet-ensure-built-{Guid.NewGuid():N}.slnx");
        File.WriteAllText(path, content);
        return path;
    }

    // A project discovered only to feed project-scope coverage (never attempted by assembly
    // resolution — see BuildStatePreflightEvaluator.IsRelevantToResolution) has no business being
    // part of the ensure-built build. Build only relevant projects plus whatever they transitively
    // reference — a relevant project's own dependency closure is still needed even if a referenced
    // project isn't independently relevant.
    internal static IReadOnlyCollection<ArchitectureDiscoveredProject> SelectRelevantProjectsWithTransitiveReferences(
        BuildStatePreflightRequest request)
    {
        ArchitectureDiscoveredProject[] seeds = request.ProjectDiscovery.DiscoveredProjects
            .Where(project => BuildStatePreflightEvaluator.IsRelevantToResolution(project, request.Resolution))
            .ToArray();

        // Fall back to the full discovered set only when the resolution snapshot itself is
        // genuinely empty-empty (nothing resolved, nothing reported missing) — the signature of a
        // first-ever build where assembly resolution hasn't had anything to find or report yet,
        // so relevance can't be determined from it at all. `seeds.Length == 0` alone is the wrong
        // condition: a non-empty resolution that simply matches none of the discovered projects
        // must not fall back to building everything.
        if (request.Resolution.ResolvedAssemblies.Count == 0 && request.Resolution.MissingAssemblyNames.Count == 0)
        {
            return request.ProjectDiscovery.DiscoveredProjects;
        }

        Dictionary<string, ArchitectureDiscoveredProject> byPath =
            request.ProjectDiscovery.DiscoveredProjects.ToDictionary(p => p.Path, StringComparer.Ordinal);

        Dictionary<string, ArchitectureDiscoveredProject> selected = new(StringComparer.Ordinal);
        Queue<ArchitectureDiscoveredProject> pending = new(seeds);

        while (pending.Count > 0)
        {
            ArchitectureDiscoveredProject project = pending.Dequeue();
            if (!selected.TryAdd(project.Path, project))
            {
                continue;
            }

            foreach (ArchitectureDiscoveredProjectReference reference in project.ProjectReferences)
            {
                if (byPath.TryGetValue(reference.Path, out ArchitectureDiscoveredProject? referenced)
                    && !selected.ContainsKey(referenced.Path))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return selected.Values;
    }

    private static BuildStateResolvedAssemblies ResolveBuiltAssemblies(BuildStatePreflightRequest request)
    {
        Dictionary<string, string> resolvedPaths = new(StringComparer.Ordinal);
        List<string> missing = new();

        // Only the same set that was actually built (relevant + transitive closure) — a
        // coverage-only project deliberately excluded from the .slnx build was never going to
        // have build output, and re-adding it to MissingAssemblyNames would make the evaluator
        // treat it as relevant again and block the otherwise-successful build.
        foreach (ArchitectureDiscoveredProject project in SelectRelevantProjectsWithTransitiveReferences(request))
        {
            string? projectDirectory = Path.GetDirectoryName(
                BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
            string? assemblyPath = ResolveBuiltAssemblyPath(request, project, projectDirectory);

            if (assemblyPath == null)
            {
                missing.Add(project.AssemblyName);
                continue;
            }

            // Build-state verification needs a physical artifact identity, not a CLR Assembly.
            resolvedPaths[project.AssemblyName] = assemblyPath;
        }

        return new BuildStateResolvedAssemblies(Array.Empty<System.Reflection.Assembly>(), missing)
        {
            ResolvedAssemblyPaths = resolvedPaths
        };
    }

    internal static string? ResolveBuiltAssemblyPath(
        BuildStatePreflightRequest request,
        ArchitectureDiscoveredProject project,
        string? projectDirectory)
    {
        if (projectDirectory == null)
        {
            return null;
        }

        // With no explicit output constraint, preserve the physical artifact selected before the
        // build instead of falling back to the newest file anywhere under bin/.
        if (request.RequestedConfiguration == null
            && request.RequestedTargetFramework == null
            && request.RequestedPlatform == null
            && request.RequestedRuntimeIdentifier == null
            && request.Resolution.ResolvedAssemblyPaths.TryGetValue(project.AssemblyName, out string? preparedPath)
            && IsProjectOutput(projectDirectory, project.AssemblyName, preparedPath))
        {
            return Path.GetFullPath(preparedPath);
        }

        return FindBuiltAssembly(
            projectDirectory, project.AssemblyName, request.RequestedConfiguration, request.RequestedTargetFramework,
            request.RequestedRuntimeIdentifier);
    }

    internal static bool IsProjectOutput(string projectDirectory, string assemblyName, string path)
    {
        string outputDirectory = Path.GetFullPath(Path.Combine(projectDirectory, "bin"));
        string outputPrefix = Path.TrimEndingDirectorySeparator(outputDirectory) + Path.DirectorySeparatorChar;
        return File.Exists(path)
            && string.Equals(Path.GetFileNameWithoutExtension(path), assemblyName, StringComparison.Ordinal)
            && Path.GetFullPath(path).StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase);
    }

    // Standard SDK-style output layout is bin/<Configuration>/<TFM>/<AssemblyName>.dll. When the
    // caller requested a specific configuration and/or target framework, only a candidate whose
    // path segments match those exactly is acceptable. A fallback to the newest candidate is only
    // used when the caller placed no constraint on configuration/TFM/RID.
    private static string? FindBuiltAssembly(
        string projectDirectory, string assemblyName, string? configuration, string? targetFramework, string? runtimeIdentifier)
    {
        string binDirectory = Path.Combine(projectDirectory, "bin");
        if (!Directory.Exists(binDirectory))
        {
            return null;
        }

        string[] candidates = Directory.GetFiles(binDirectory, $"{assemblyName}.dll", SearchOption.AllDirectories);
        if (candidates.Length == 0)
        {
            return null;
        }

        if (configuration == null && targetFramework == null && runtimeIdentifier == null)
        {
            return candidates.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        return candidates.FirstOrDefault(path => MatchesRequestedOutputPath(
            Path.GetRelativePath(binDirectory, path), configuration, targetFramework, runtimeIdentifier));
    }

    private static bool MatchesRequestedOutputPath(string relativePath, string? configuration, string? targetFramework,
        string? runtimeIdentifier)
    {
        string[] segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        bool configurationMatches = configuration == null
            || (segments.Length > 0 && string.Equals(segments[0], configuration, StringComparison.OrdinalIgnoreCase));
        bool targetFrameworkMatches = targetFramework == null
            || (segments.Length > 1 && string.Equals(segments[1], targetFramework, StringComparison.OrdinalIgnoreCase));
        bool runtimeIdentifierMatches = runtimeIdentifier == null
            || segments.Any(segment => string.Equals(segment, runtimeIdentifier, StringComparison.OrdinalIgnoreCase));

        return configurationMatches && targetFrameworkMatches && runtimeIdentifierMatches;
    }

    private static void WriteReceiptsForCurrentArtifacts(
        BuildStatePreflightRequest request, BuildStatePreflightResult evaluation)
    {
        Dictionary<string, ArchitectureDiscoveredProject> projectsByPath =
            request.ProjectDiscovery.DiscoveredProjects.ToDictionary(p => p.Path, StringComparer.Ordinal);

        foreach (BuildStatePreflightDiagnostic diagnostic in evaluation.Diagnostics)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            bool ownsExpectedOutput = diagnostic.State is BuildStatePreflightState.UnverifiableArtifact
                or BuildStatePreflightState.StaleArtifact
                or BuildStatePreflightState.WrongConfiguration
                or BuildStatePreflightState.WrongTargetFramework;

            if (!ownsExpectedOutput
                || diagnostic.Evidence.ExpectedOutputPath == null
                || !projectsByPath.TryGetValue(diagnostic.Evidence.ProjectPath, out ArchitectureDiscoveredProject? project))
            {
                continue;
            }

            string assemblyPath = diagnostic.Evidence.ExpectedOutputPath;
            string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(
                project.Path, request.RepositoryRoot, request.CancellationToken);
            string assemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath, request.CancellationToken);
            EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(
                project.Path, request.RepositoryRoot, request.RequestedConfiguration, request.RequestedTargetFramework,
                request.RequestedPlatform, request.RequestedRuntimeIdentifier, request.CancellationToken);
            EvaluatedBuildInputManifestV1 publicationCheck = EvaluatedBuildInputManifestCollector.Collect(
                project.Path, request.RepositoryRoot, request.RequestedConfiguration, request.RequestedTargetFramework,
                request.RequestedPlatform, request.RequestedRuntimeIdentifier, request.CancellationToken);
            string publicationAssemblyDigest = BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath, request.CancellationToken);

            if (!string.Equals(manifest.Digest, publicationCheck.Digest, StringComparison.Ordinal)
                || !string.Equals(assemblyDigest, publicationAssemblyDigest, StringComparison.Ordinal))
            {
                continue;
            }

            request.CancellationToken.ThrowIfCancellationRequested();

            BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
                project.Path,
                project.AssemblyName,
                request.RequestedConfiguration,
                request.RequestedTargetFramework,
                fingerprint,
                assemblyDigest,
                manifest.Digest,
                manifest.Eligibility,
                manifest.IneligibilityReasons,
                request.RequestedPlatform,
                request.RequestedRuntimeIdentifier));
        }
    }
}
