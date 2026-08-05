using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Resolution.Abstractions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRunnerSetupServiceFakeDependencyTests
{
    private sealed class FakeRepositoryRootResolver : IArchitectureRepositoryRootResolver
    {
        public bool WasCalled { get; private set; }

        public string Resolve()
        {
            WasCalled = true;
            return "/fake/repository/root";
        }

        public string ResolveFrom(string policyPath)
        {
            WasCalled = true;
            return "/fake/repository/root";
        }
    }

    private sealed class FakeProjectDiscoveryService : IArchitectureProjectDiscoveryService
    {
        public bool WasCalled { get; private set; }

        // Issue #375 regression: lets a test cancel from inside "discovery" (as opposed to
        // passing an already-cancelled token) to prove BuildRunnerCore observes cancellation that
        // occurs during this call rather than only before it starts.
        public Action? OnResolveAndApply { get; set; }

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            OnResolveAndApply?.Invoke();
            return ProjectDiscoveryResult.Empty;
        }
    }

    private sealed class FakeAssemblyResolutionService : IArchitectureAssemblyResolutionService
    {
        public bool WasCalled { get; private set; }

        public ResolutionResult Resolve(
            ArchitectureContractDocument document,
            string repositoryRoot,
            ProjectDiscoveryResult discovery,
            bool resolveAssemblyOutputs,
            string? mode,
            HashSet<string>? selectedContractIds,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return new ResolutionResult(
                new[] { typeof(FakeAssemblyResolutionService).Assembly },
                new[] { "fake-missing-assembly-marker" },
                new[] { "fake-probing-path-marker" });
        }

        public ResolutionResult ResolvePostBuild(
            ArchitectureContractDocument document,
            string repositoryRoot,
            ProjectDiscoveryResult discovery,
            bool resolveAssemblyOutputs,
            string? mode,
            HashSet<string>? selectedContractIds,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? expectedArtifactContentDigests = null)
        {
            return Resolve(document, repositoryRoot, discovery, resolveAssemblyOutputs, mode, selectedContractIds,
                cancellationToken);
        }
    }

    [Test]
    public void BuildRunner_FakeSetupDependencies_DriveRunnerWithoutTouchingFileSystem()
    {
        // Faking repository-root resolution, project discovery, and assembly resolution together
        // means BuildRunner never touches a real file system, never globs for projects, and never
        // probes for or loads a real assembly — proving these setup dependencies are independently
        // replaceable, not just that one of them can be swapped while the others still do real I/O.
        var document = new ArchitectureContractDocument { Version = 1, Name = "Test" };
        var fakeRepositoryRoot = new FakeRepositoryRootResolver();
        var fakeProjectDiscovery = new FakeProjectDiscoveryService();
        var fakeAssemblyResolution = new FakeAssemblyResolutionService();

        var runnerSetupService = new ArchitectureRunnerSetupService(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            fakeRepositoryRoot,
            new ConditionSetResolutionService(),
            fakeProjectDiscovery,
            fakeAssemblyResolution);

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(document, policyPath: "unused-by-fakes.arch.yml");

        Assert.That(fakeRepositoryRoot.WasCalled, Is.True);
        Assert.That(fakeProjectDiscovery.WasCalled, Is.True);
        Assert.That(fakeAssemblyResolution.WasCalled, Is.True);
        Assert.That(setup.RepositoryRoot, Is.EqualTo("/fake/repository/root"));

        // Prove the fakes' results actually reached the runner's analysis context, not just that
        // they were invoked — the context is what every contract check reads.
        ArchitectureAnalysisContext context = setup.Runner.Session.Context;
        Assert.That(context.RepositoryRoot, Is.EqualTo("/fake/repository/root"));
        Assert.That(context.TargetAssemblies, Has.Member(typeof(FakeAssemblyResolutionService).Assembly));
        Assert.That(context.MissingAssemblyNames, Has.Member("fake-missing-assembly-marker"));
        Assert.That(context.AssemblyProbingPaths, Has.Member("fake-probing-path-marker"));
    }

    // Issue #375 PR #416 review: proves cancellation observed *during* project discovery (not an
    // already-cancelled token checked only before BuildRunner starts) stops the call before
    // assembly resolution ever runs — i.e. the token genuinely reaches and is honored by the
    // discovery/resolution loop, not just checked once up front.
    [Test]
    public void BuildRunner_CancelledDuringDiscovery_ThrowsBeforeAssemblyResolutionRuns()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Test" };
        var fakeRepositoryRoot = new FakeRepositoryRootResolver();
        var fakeProjectDiscovery = new FakeProjectDiscoveryService();
        var fakeAssemblyResolution = new FakeAssemblyResolutionService();
        using CancellationTokenSource cts = new();
        fakeProjectDiscovery.OnResolveAndApply = () => cts.Cancel();

        var runnerSetupService = new ArchitectureRunnerSetupService(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            fakeRepositoryRoot,
            new ConditionSetResolutionService(),
            fakeProjectDiscovery,
            fakeAssemblyResolution);

        Assert.Throws<OperationCanceledException>(() => runnerSetupService.BuildRunner(
            document, policyPath: "unused-by-fakes.arch.yml", cancellationToken: cts.Token));

        Assert.Multiple(() =>
        {
            Assert.That(fakeProjectDiscovery.WasCalled, Is.True);
            Assert.That(fakeAssemblyResolution.WasCalled, Is.False);
        });
    }
}
