using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRunnerSetupServiceFakeDependencyTests
{
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
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }

    [Test]
    public void BuildRunner_FakeAssemblyResolutionService_DrivesRunnerWithoutRealResolution()
    {
        // No analysis.target_assemblies and no analysis.solution/projects means the real
        // ArchitectureAssemblyResolver/ArchitectureProjectDiscovery would have nothing to resolve
        // and ArchitectureAssemblyResolutionService would throw. Substituting a fake here proves
        // BuildRunner's setup dependencies are independently replaceable, without needing a real
        // file system, Roslyn compilation, or assembly load to drive the runner.
        string repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-fake-dependency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repoRoot);
        try
        {
            string policyPath = Path.Combine(repoRoot, "policy.arch.yml");
            File.WriteAllText(policyPath, "version: 1\nname: test\n");

            var document = new ArchitectureContractDocument { Version = 1, Name = "Test" };
            var fakeAssemblyResolution = new FakeAssemblyResolutionService();

            var runnerSetupService = new ArchitectureRunnerSetupService(
                new ArchitecturePolicyDocumentLoader(),
                new ArchitectureBaselineLoadingService(),
                new ArchitectureRepositoryRootResolver(),
                new ConditionSetResolutionService(),
                new ArchitectureProjectDiscoveryService(),
                fakeAssemblyResolution);

            ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(document, policyPath);

            Assert.That(fakeAssemblyResolution.WasCalled, Is.True);
            Assert.That(setup.Runner, Is.Not.Null);
        }
        finally
        {
            Directory.Delete(repoRoot, true);
        }
    }
}
