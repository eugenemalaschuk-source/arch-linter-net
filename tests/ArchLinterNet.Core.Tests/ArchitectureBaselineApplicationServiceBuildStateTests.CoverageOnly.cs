using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureBaselineApplicationServiceBuildStateTests
{
    [Test]
    public void Verify_EnsureBuiltProjectOnlyPolicy_SkipsBuildStatePreparation()
    {
        var document = CreateDocument();
        document.Contracts = new ArchitectureContractGroups
        {
            StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
            {
                new() { Name = "Project metadata" },
            },
            StrictCoverage = new List<ArchitectureCoverageContract>
            {
                new() { Name = "Project coverage", Scope = "project" },
            },
        };
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { FixtureProject() },
        };
        var runner = new FakeContractRunner(CreateSession(document, discovery));
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = runner,
            PreparationToReturn = new ArchitectureRunnerPreparation(
                "/fake/repository/root",
                PreprocessorSymbols: null,
                ProjectDiscovery: discovery,
                ResolveAssemblyOutputs: false,
                SelectedAssemblyArtifactPaths: Array.Empty<string>(),
                CapturedArtifactContentDigests: new Dictionary<string, string>(),
                MissingAssemblyNames: Array.Empty<string>(),
                IsMetadataReferenceClosureComplete: false),
        };
        var preparationService = new FakeBuildStatePreparationService();
        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
            NoRestore = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.Zero);
            Assert.That(runnerSetupService.MaterializePreparedRunnerCallCount, Is.Zero);
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(1));
        });
    }
}
