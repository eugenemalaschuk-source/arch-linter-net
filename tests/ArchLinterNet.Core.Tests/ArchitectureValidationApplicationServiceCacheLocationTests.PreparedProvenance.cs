using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureValidationApplicationServiceCacheLocationTests
{
    [Test]
    public void CreateSnapshot_MetadataPreflightFailureRetainsPreparedPathsInEvaluationException()
    {
        PreparedProvenanceFixture fixture = CreatePreparedProvenanceFixture();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = CreateDocument(),
            PreparationProvider = _ => fixture.Preparation,
        };
        var preparationService = new FakeBuildStatePreparationService
        {
            ExceptionToThrow = new InvalidOperationException("The metadata preflight failed."),
        };
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        ArchitectureAnalysisEvaluationException exception = Assert.Throws<ArchitectureAnalysisEvaluationException>(() =>
            applicationService.CreateSnapshot(new AnalysisSnapshotRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
            }))!;

        Assert.Multiple(() =>
        {
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(1));
            Assert.That(exception.ResolvedAssemblyPaths, Is.EqualTo(new[] { fixture.AssemblyPath }));
            Assert.That(exception.DiscoveredProjectPaths, Is.EqualTo(new[] { fixture.ProjectPath }));
        });
    }

    [Test]
    public void CreateSnapshot_PostBuildPreflightFailureRetainsPreparedPaths()
    {
        PreparedProvenanceFixture initial = CreatePreparedProvenanceFixture();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = CreateDocument(),
            PreparationProvider = _ => initial.Preparation,
        };
        var preparationService = new FakeBuildStatePreparationService
        {
            ExceptionProvider = call => call == 2
                ? new InvalidOperationException("The post-build receipt preflight failed.")
                : null,
        };
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        ArchitectureAnalysisEvaluationException exception = Assert.Throws<ArchitectureAnalysisEvaluationException>(() =>
            applicationService.CreateSnapshot(new AnalysisSnapshotRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
                PreparationMode = BuildPreparationMode.EnsureBuilt,
            }))!;

        Assert.Multiple(() =>
        {
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1));
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(2));
            Assert.That(exception.ResolvedAssemblyPaths, Is.EqualTo(new[] { initial.AssemblyPath }));
            Assert.That(exception.DiscoveredProjectPaths, Is.EqualTo(new[] { initial.ProjectPath }));
        });
    }

    [Test]
    public void CreateSnapshot_MetadataPreflightCancellationRetainsPreparedCountersAndInputPaths()
    {
        PreparedProvenanceFixture fixture = CreatePreparedProvenanceFixture();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = CreateDocument(),
            PreparationProvider = _ => fixture.Preparation,
        };
        var preparationService = new FakeBuildStatePreparationService
        {
            ExceptionToThrow = new OperationCanceledException("The metadata preflight was cancelled."),
        };
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), preparationService);

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            applicationService.CreateSnapshot(new AnalysisSnapshotRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                CacheLocation = new AnalysisCacheLocation("/fake/cache", AnalysisCacheMode.ExplicitPath),
            }))!;

        ArchitectureAnalysisSnapshotCounters counters =
            exception.Data["ArchLinterNet.AnalysisProfile.Counters"] as ArchitectureAnalysisSnapshotCounters
            ?? throw new AssertionException("Cancellation profile counters were not attached.");
        IReadOnlyList<string> inputPaths =
            exception.Data["ArchLinterNet.AnalysisProfile.InputPaths"] as IReadOnlyList<string>
            ?? throw new AssertionException("Cancellation profile input paths were not attached.");

        Assert.Multiple(() =>
        {
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(1));
            Assert.That(counters.DiscoveredProjectCount, Is.EqualTo(1));
            Assert.That(counters.RetainedAssemblyCount, Is.Zero);
            Assert.That(counters.SelectedAssemblyCount, Is.EqualTo(2));
            Assert.That(inputPaths, Does.Contain(fixture.AssemblyPath));
            Assert.That(inputPaths, Does.Contain(BuildReceiptStore.ReceiptPathFor(fixture.AssemblyPath)));
            Assert.That(inputPaths, Does.Contain(fixture.ProjectPath));
        });
    }

    private static PreparedProvenanceFixture CreatePreparedProvenanceFixture()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-preparation-{Guid.NewGuid():N}");
        string projectPath = Path.Combine(repositoryRoot, "fixture", "Fixture.csproj");
        string assemblyPath = Path.Combine(repositoryRoot, "fixture", "bin", "Debug", "net10.0", "Fixture.dll");
        var discovery = new ProjectDiscoveryResult(
            ["Fixture"], Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = [new ArchitectureDiscoveredProject("fixture/Fixture.csproj", "Fixture", ["net10.0"])],
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Fixture"] = assemblyPath,
            },
        };
        ArchitectureRunnerPreparation preparation = CreatePreparation(
            repositoryRoot, discovery, [assemblyPath], ["MissingFixture"]);
        return new PreparedProvenanceFixture(preparation, projectPath, assemblyPath);
    }

    private sealed record PreparedProvenanceFixture(
        ArchitectureRunnerPreparation Preparation,
        string ProjectPath,
        string AssemblyPath);
}
