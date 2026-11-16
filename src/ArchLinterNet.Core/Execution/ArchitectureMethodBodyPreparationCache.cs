using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ArchLinterNet.Core.Execution;

internal sealed record ArchitectureMethodBodyPreparation(
    IReadOnlyList<string> SourceFilePaths,
    CSharpCompilation? Compilation,
    string? SourceAssemblyHint,
    string? FallbackReason);

// Immutable source/build/Roslyn inputs are shared only for the lifetime of one analysis session.
// The session treats repository inputs as a snapshot; callers should create a new session after
// changing source files or project metadata.
// Rule patterns, execution contexts, ignores, diagnostics, and IL matches stay with each checker call.
internal sealed class ArchitectureMethodBodyPreparationCache : IDisposable
{
    private const string DefaultParseOptionsIdentity = "CSharpParseOptions.Default";

    private readonly Lock _gate = new();

    private readonly Dictionary<SourceInventoryKey, Lazy<SourceInventory>> _sourceInventories = new();

    private readonly Dictionary<SourceInventoryKey, Lazy<ArchitectureDiscoveredProject?>> _owners = new();

    private readonly Dictionary<ProjectContextKey, Lazy<ArchitectureProjectRoslynResolution>> _projectContexts = new();

    private readonly Dictionary<SyntaxTreeKey, Lazy<IReadOnlyList<SyntaxTree>>> _syntaxTrees = new();

    private readonly Dictionary<CompilationKey, Lazy<CSharpCompilation>> _compilations = new();

    private readonly IArchitectureFileSystem _fileSystem;

    private readonly IArchitectureAssemblyLoader _assemblyLoader;

    private readonly ArchitectureProjectRoslynContextResolver _projectRoslynContextResolver = new();

    private bool _disposed;

    internal ArchitectureMethodBodyPreparationCache(
        IArchitectureFileSystem? fileSystem = null,
        IArchitectureAssemblyLoader? assemblyLoader = null)
    {
        _fileSystem = fileSystem ?? ArchitectureFileSystem.Real;
        _assemblyLoader = assemblyLoader ?? ArchitectureAssemblyLoader.Real;
    }

    public ArchitectureMethodBodyPreparation Prepare(
        string repositoryRoot,
        ArchitectureLayer sourceLayer,
        string[]? sourceRoots,
        IReadOnlyList<string>? preprocessorSymbols,
        ProjectDiscoveryResult? projectDiscovery,
        string buildConfiguration,
        CancellationToken cancellationToken,
        BenchmarkWorkObserver? benchmarkWorkObserver)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inventoryKey = SourceInventoryKey.Create(repositoryRoot, sourceLayer, sourceRoots);
        SourceInventory inventory = GetOrCreate(
            _sourceInventories,
            inventoryKey,
            () => new SourceInventory(ArchitectureSourceScanner.FindMatchingSourceFileInputs(
                repositoryRoot,
                sourceLayer,
                sourceRoots,
                _fileSystem,
                cancellationToken,
                benchmarkWorkObserver)));
        cancellationToken.ThrowIfCancellationRequested();

        if (inventory.SourceFilePaths.Count == 0)
        {
            return new ArchitectureMethodBodyPreparation(Array.Empty<string>(), null, null, null);
        }

        ArchitectureDiscoveredProject? owner = null;
        string? sourceAssemblyHint = null;
        string? fallbackReason = null;
        string? ownerProjectPath = null;
        IReadOnlyList<string>? explicitReferencePaths = null;

        if (projectDiscovery is { DiscoveredProjects.Count: > 0 })
        {
            owner = GetOrCreate(
                _owners,
                inventoryKey,
                () => ResolveOwningProject(
                    repositoryRoot,
                    projectDiscovery.DiscoveredProjects,
                    inventory.SourceFiles,
                    cancellationToken,
                    benchmarkWorkObserver));

            if (owner is null)
            {
                fallbackReason = "no single discovered project owns this contract's source files (files span zero or multiple discovered project directories)";
            }
            else
            {
                sourceAssemblyHint = owner.AssemblyName;
                string resolvedProjectPath = Path.GetFullPath(Path.Combine(repositoryRoot, owner.Path));
                ownerProjectPath = resolvedProjectPath;
                var projectContextKey = ProjectContextKey.Create(
                    resolvedProjectPath, buildConfiguration, owner.TargetFrameworks);

                ArchitectureProjectRoslynResolution resolution = GetOrCreate(
                    _projectContexts,
                    projectContextKey,
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        benchmarkWorkObserver?.RecordMethodBodyBuildContextResolution();
                        ArchitectureProjectRoslynResolution result =
                            _projectRoslynContextResolver.Resolve(resolvedProjectPath, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        return result;
                    });

                if (resolution.Succeeded)
                {
                    explicitReferencePaths = resolution.Context!.ReferenceAssemblyPaths;
                }
                else
                {
                    fallbackReason =
                        $"project '{owner.Path}' could not be evaluated for project-aware Roslyn analysis: {resolution.FailureReason}";
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool hasExplicitReferencePaths = explicitReferencePaths is { Count: > 0 };
        IReadOnlyList<string> referencePaths = hasExplicitReferencePaths
            ? explicitReferencePaths!.Where(_fileSystem.FileExists).ToArray()
            : RoslynCompilationFactory.GetDefaultReferenceAssemblyPaths(_fileSystem, _assemblyLoader);

        SyntaxTreeKey syntaxTreeKey = new(
            inventory.ContentIdentity,
            DefaultParseOptionsIdentity,
            CreateSymbolsIdentity(preprocessorSymbols));

        IReadOnlyList<SyntaxTree> syntaxTrees = GetOrCreate(
            _syntaxTrees,
            syntaxTreeKey,
            () => RoslynCompilationFactory.ParseMethodBodySourceFiles(
                inventory.SourceFiles,
                preprocessorSymbols,
                cancellationToken,
                benchmarkWorkObserver));
        CompilationKey compilationKey = new(
            syntaxTreeKey,
            CreateStringListIdentity(referencePaths),
            CreateBuildContextIdentity(ownerProjectPath, buildConfiguration, owner?.TargetFrameworks),
            hasExplicitReferencePaths);

        CSharpCompilation compilation = GetOrCreate(
            _compilations,
            compilationKey,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                CSharpCompilation result = RoslynCompilationFactory.CreateMethodBodyCompilation(
                    "ArchitectureSourceScanner", syntaxTrees, referencePaths, _fileSystem);
                benchmarkWorkObserver?.RecordMethodBodyCompilation(result.SyntaxTrees.Length);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            });

        return new ArchitectureMethodBodyPreparation(
            inventory.SourceFilePaths, compilation, sourceAssemblyHint, fallbackReason);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _sourceInventories.Clear();
            _owners.Clear();
            _projectContexts.Clear();
            _syntaxTrees.Clear();
            _compilations.Clear();
        }
    }

    private TValue GetOrCreate<TKey, TValue>(
        Dictionary<TKey, Lazy<TValue>> cache,
        TKey key,
        Func<TValue> factory)
        where TKey : notnull
    {
        Lazy<TValue> value;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!cache.TryGetValue(key, out value!))
            {
                value = new Lazy<TValue>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
                cache.Add(key, value);
            }
        }

        try
        {
            TValue result = value.Value;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
            }

            return result;
        }
        catch
        {
            // A cancellation or failed preparation must not poison the session cache. Remove only
            // the entry this caller observed in case another caller has already retried it.
            lock (_gate)
            {
                if (cache.TryGetValue(key, out Lazy<TValue>? current) && ReferenceEquals(current, value))
                {
                    cache.Remove(key);
                }
            }

            throw;
        }
    }

    private static ArchitectureDiscoveredProject? ResolveOwningProject(
        string repositoryRoot,
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects,
        IReadOnlyList<ArchitectureMethodBodySourceFile> matchedFiles,
        CancellationToken cancellationToken,
        BenchmarkWorkObserver? benchmarkWorkObserver)
    {
        benchmarkWorkObserver?.RecordMethodBodyOwnershipLookup();
        List<(ArchitectureDiscoveredProject Project, string Directory)> projectDirectories =
            new(discoveredProjects.Count);
        foreach (ArchitectureDiscoveredProject project in discoveredProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            benchmarkWorkObserver?.RecordMethodBodyOwnershipProjectDirectory();
            projectDirectories.Add((project, NormalizeDirectory(Path.GetFullPath(Path.Combine(
                repositoryRoot, Path.GetDirectoryName(project.Path) ?? string.Empty)))));
        }

        HashSet<string> owningProjectPaths = new(StringComparer.OrdinalIgnoreCase);
        ArchitectureDiscoveredProject? owner = null;
        foreach (ArchitectureMethodBodySourceFile sourceFile in matchedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fileDirectory = NormalizeDirectory(
                Path.GetDirectoryName(Path.GetFullPath(sourceFile.Path)) ?? string.Empty);

            ArchitectureDiscoveredProject? bestMatch = null;
            int bestLength = -1;
            foreach ((ArchitectureDiscoveredProject candidate, string candidateDirectory) in projectDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                benchmarkWorkObserver?.RecordMethodBodyOwnershipComparison();
                if (fileDirectory.StartsWith(candidateDirectory, StringComparison.OrdinalIgnoreCase)
                    && candidateDirectory.Length > bestLength)
                {
                    bestMatch = candidate;
                    bestLength = candidateDirectory.Length;
                }
            }

            if (bestMatch is null) return null;
            owningProjectPaths.Add(bestMatch.Path);
            owner = bestMatch;
        }

        return owningProjectPaths.Count == 1 ? owner : null;
    }

    private static string NormalizeDirectory(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/') + "/";
    }

    private static string CreateSymbolsIdentity(IReadOnlyList<string>? symbols)
    {
        return symbols is { Count: > 0 } ? CreateStringListIdentity(symbols) : string.Empty;
    }

    private static string CreateStringListIdentity(IEnumerable<string> values)
    {
        return string.Join("\0", values.Select(value => $"{value.Length}:{value}"));
    }

    private static string CreateBuildContextIdentity(
        string? projectPath,
        string buildConfiguration,
        IReadOnlyList<string>? targetFrameworks)
    {
        string project = projectPath ?? string.Empty;
        string targetFrameworkIdentity = targetFrameworks is { Count: > 0 }
            ? CreateStringListIdentity(targetFrameworks)
            : string.Empty;
        return $"{project.Length}:{project}|{buildConfiguration.Length}:{buildConfiguration}|{targetFrameworkIdentity}";
    }

    private sealed class SourceInventory
    {
        public SourceInventory(IReadOnlyList<ArchitectureMethodBodySourceFile> sourceFiles)
        {
            SourceFiles = sourceFiles;
            SourceFilePaths = sourceFiles.Select(sourceFile => sourceFile.Path).ToArray();
            ContentIdentity = new SourceContentIdentity(sourceFiles);
        }

        public IReadOnlyList<string> SourceFilePaths { get; }

        public IReadOnlyList<ArchitectureMethodBodySourceFile> SourceFiles { get; }

        public SourceContentIdentity ContentIdentity { get; }
    }

    private sealed class SourceContentIdentity : IEquatable<SourceContentIdentity>
    {
        private readonly IReadOnlyList<SourceFileIdentity> _sourceFiles;

        private readonly int _hashCode;

        public SourceContentIdentity(IReadOnlyList<ArchitectureMethodBodySourceFile> sourceFiles)
        {
            _sourceFiles = sourceFiles
                .Select(sourceFile => new SourceFileIdentity(
                    sourceFile.Path,
                    Convert.ToHexString(sourceFile.Text.GetChecksum().AsSpan())))
                .ToArray();
            HashCode hash = new();
            foreach (SourceFileIdentity sourceFile in _sourceFiles)
            {
                hash.Add(sourceFile.Path, StringComparer.Ordinal);
                hash.Add(sourceFile.ContentChecksum, StringComparer.Ordinal);
            }

            _hashCode = hash.ToHashCode();
        }

        public bool Equals(SourceContentIdentity? other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null || _sourceFiles.Count != other._sourceFiles.Count) return false;

            for (int index = 0; index < _sourceFiles.Count; index++)
            {
                SourceFileIdentity left = _sourceFiles[index];
                SourceFileIdentity right = other._sourceFiles[index];
                if (!string.Equals(left.Path, right.Path, StringComparison.Ordinal)
                    || !string.Equals(left.ContentChecksum, right.ContentChecksum, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceContentIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private readonly record struct SourceFileIdentity(string Path, string ContentChecksum);
    }

    private readonly record struct SourceInventoryKey(
        string RepositoryRoot,
        string SourceRoots,
        string Namespace,
        string NamespaceSuffix,
        string Exclusions)
    {
        public static SourceInventoryKey Create(string repositoryRoot, ArchitectureLayer sourceLayer, string[]? sourceRoots)
        {
            string[] roots = sourceRoots ?? ["src", "tests"];
            string exclusions = CreateStringListIdentity(sourceLayer.Exclude
                .Select(exclusion => CreateStringListIdentity([exclusion.Namespace, exclusion.NamespaceSuffix]))
                .OrderBy(identity => identity, StringComparer.Ordinal));
            return new SourceInventoryKey(
                Path.GetFullPath(repositoryRoot),
                CreateStringListIdentity(roots),
                sourceLayer.Namespace,
                sourceLayer.NamespaceSuffix,
                exclusions);
        }
    }

    private readonly record struct ProjectContextKey(string ProjectPath, string Configuration, string TargetFrameworks)
    {
        public static ProjectContextKey Create(
            string projectPath,
            string configuration,
            IReadOnlyList<string> targetFrameworks)
        {
            return new ProjectContextKey(
                projectPath,
                configuration,
                CreateStringListIdentity(targetFrameworks));
        }
    }

    private readonly record struct SyntaxTreeKey(
        SourceContentIdentity Source,
        string ParseOptions,
        string PreprocessorSymbols);

    private readonly record struct CompilationKey(
        SyntaxTreeKey SyntaxTrees,
        string ReferencePaths,
        string BuildContext,
        bool UsesExplicitReferencePaths);
}
