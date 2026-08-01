using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO.Abstractions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #375 PR #416 review: on the post-build (ensure-built) path, ArchitectureAssemblyResolutionService
// creates an isolated AssemblyLoadContext-backed scope before iterating target assembly names.
// Ownership of that scope transfers to the returned ResolutionResult only on a normal return — an
// exceptional exit (in particular, cancellation observed between two assembly names) must dispose
// it there instead, since nothing downstream is ever constructed to own it otherwise.
[TestFixture]
public sealed class ArchitectureAssemblyResolutionServiceCancellationTests
{
    private static readonly string[] _assemblyNames = { "AssemblyA", "AssemblyB" };
    private static readonly string[] _net10 = { "net10.0" };

    private sealed class FakeIsolatedLoadScope : IArchitectureAssemblyLoadScope
    {
        public bool Disposed { get; private set; }

        public List<string> LoadedPaths { get; } = new();

        public Action? OnLoadFrom { get; set; }

        public Assembly LoadFrom(string path)
        {
            LoadedPaths.Add(path);
            OnLoadFrom?.Invoke();
            return typeof(FakeIsolatedLoadScope).Assembly;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeIsolatedScopeLoader : IArchitectureAssemblyLoader
    {
        public FakeIsolatedLoadScope ScopeToReturn { get; } = new();

        public IReadOnlyList<Assembly> GetLoadedAssemblies() => Array.Empty<Assembly>();

        public Assembly Load(AssemblyName assemblyName) =>
            throw new InvalidOperationException("Not expected on the isolated-loading path.");

        public Assembly LoadFrom(string path) =>
            throw new InvalidOperationException("Not expected on the isolated-loading path.");

        public IArchitectureAssemblyLoadScope CreateIsolatedLoadScope(
            IReadOnlyList<string> probingPaths, IReadOnlyDictionary<string, string> exactAssemblyPaths) =>
            ScopeToReturn;
    }

    [Test]
    public void ResolvePostBuild_CancelledBetweenTwoAssemblyNames_DisposesIsolatedLoadScope()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "AssemblyA", "AssemblyB" },
            },
        };
        var resolvedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AssemblyA"] = "/fake/repo/AssemblyA.dll",
            ["AssemblyB"] = "/fake/repo/AssemblyB.dll",
        };
        ProjectDiscoveryResult discovery = new(
            _assemblyNames, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject("A.csproj", "AssemblyA", _net10),
                new ArchitectureDiscoveredProject("B.csproj", "AssemblyB", _net10),
            },
            ResolvedAssemblyPaths = resolvedPaths,
        };
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/fake/repo/AssemblyA.dll", string.Empty, DateTime.UtcNow);
        fileSystem.AddFile("/fake/repo/AssemblyB.dll", string.Empty, DateTime.UtcNow);
        var loader = new FakeIsolatedScopeLoader();
        using CancellationTokenSource cts = new();
        loader.ScopeToReturn.OnLoadFrom = () => cts.Cancel();

        var service = new ArchitectureAssemblyResolutionService(
            fileSystem, new FakeArchitectureEnvironment(), loader);

        Assert.Throws<OperationCanceledException>(() => service.ResolvePostBuild(
            document, "/fake/repo", discovery, resolveAssemblyOutputs: true, mode: null,
            selectedContractIds: null, cts.Token));

        Assert.Multiple(() =>
        {
            Assert.That(loader.ScopeToReturn.Disposed, Is.True);
            Assert.That(loader.ScopeToReturn.LoadedPaths, Has.Count.EqualTo(1),
                "Cancellation fired inside the first LoadFrom call, so the second assembly name's " +
                "iteration must never have started.");
        });
    }
}
