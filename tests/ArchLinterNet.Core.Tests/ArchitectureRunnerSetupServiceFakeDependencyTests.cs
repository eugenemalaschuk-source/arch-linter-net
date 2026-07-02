using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRunnerSetupServiceFakeDependencyTests
{
    private sealed class FakeRepositoryRootResolver : IArchitectureRepositoryRootResolver
    {
        public bool WasCalled { get; private set; }

        public string ResolveFrom(string policyPath)
        {
            WasCalled = true;
            return "/fake/repository/root";
        }
    }

    private sealed class FakeProjectDiscoveryService : IArchitectureProjectDiscoveryService
    {
        public bool WasCalled { get; private set; }

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs)
        {
            WasCalled = true;
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
            HashSet<string>? selectedContractIds)
        {
            WasCalled = true;
            return new ResolutionResult(
                new[] { typeof(FakeAssemblyResolutionService).Assembly },
                new[] { "fake-missing-assembly-marker" },
                new[] { "fake-probing-path-marker" });
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
            new ArchitecturePolicyDocumentLoader(ArchitectureFileSystem.Real),
            new ArchitectureBaselineLoadingService(ArchitectureFileSystem.Real),
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
}
