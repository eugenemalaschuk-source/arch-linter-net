using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Split out of ArchitectureBaselineApplicationServiceBuildStateTests to keep that file under the
// repository's 800-line decomposition limit. Regression coverage for a disposal-ownership
// regression (#897 review) in ArchitectureBaselineCandidateCollector's ResolveSetup branches:
// a materialized runner's disposable context leaked if a build-state preflight threw (including
// cancellation) between runner creation and the outcome being returned to the caller's
// try/finally. Each of ResolvePreparedPostBuildSetup/ResolveOrdinarySetup now disposes its own
// runner on exception before rethrowing; these tests use a fake IArchitectureAssemblyLoadScope to
// detect disposal across all three exception windows plus a cancellation-specific variant.
[TestFixture]
public sealed class ArchitectureBaselineApplicationServiceDisposalTests
{
    internal sealed class FakeAssemblyLoadScope : IArchitectureAssemblyLoadScope
    {
        public bool DisposeCalled { get; private set; }

        public Assembly LoadFrom(string path) => throw new NotSupportedException();

        public void Dispose() => DisposeCalled = true;
    }

    private static (ArchitectureAnalysisSession Session, FakeAssemblyLoadScope LoadScope) CreateSessionWithLoadScopeTracking(
        ArchitectureContractDocument document,
        ProjectDiscoveryResult? projectDiscovery = null,
        IReadOnlyCollection<string>? missingAssemblyNames = null)
    {
        var loadScope = new FakeAssemblyLoadScope();
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<Assembly>(),
            missingAssemblyNames ?? Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery,
            isolatedLoadScope: loadScope);

        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
        return (session, loadScope);
    }

    [Test]
    public void Diff_PreparedPostBuildPreflightThrows_DisposesMaterializedRunnerBeforePropagating()
    {
        var document = ArchitectureBaselineApplicationServiceBuildStateTests.CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { ArchitectureBaselineApplicationServiceBuildStateTests.FixtureProject() },
        };
        (ArchitectureAnalysisSession session, FakeAssemblyLoadScope loadScope) =
            CreateSessionWithLoadScopeTracking(document, discovery, missingAssemblyNames: ["Fixture"]);
        var preparedRunner = new FakeContractRunner(session);
        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnersToReturn = new Queue<IArchitectureContractRunner>([preparedRunner]),
        };
        var preparationService = new ArchitectureBaselineApplicationServiceBuildStateTests.FakeBuildStatePreparationService
        {
            ExceptionToThrow = new InvalidOperationException("preflight boom"),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        InvalidOperationException? thrown = Assert.Throws<InvalidOperationException>(() =>
            applicationService.Diff(new BaselineDiffRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "strict",
                PreparationMode = BuildPreparationMode.Ordinary,
                UsePreparedPostBuildState = true,
                PreparedPostBuildRunner = ArchitectureBaselineApplicationServiceBuildStateTests.CreatePreparedRunner(discovery),
            }));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Is.EqualTo("preflight boom"));
            Assert.That(loadScope.DisposeCalled, Is.True,
                "the runner materialized by MaterializePreparedRunner must be disposed even when the " +
                "post-materialization preflight throws, otherwise it leaks -- CollectCandidatesCore's " +
                "outer finally never sees it since the exception unwinds before ResolveSetup returns.");
        });
    }

    [Test]
    public void Diff_OrdinaryFirstPreflightThrows_DisposesInitialRunnerBeforePropagating()
    {
        var document = ArchitectureBaselineApplicationServiceBuildStateTests.CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { ArchitectureBaselineApplicationServiceBuildStateTests.FixtureProject() },
        };
        (ArchitectureAnalysisSession session, FakeAssemblyLoadScope loadScope) =
            CreateSessionWithLoadScopeTracking(document, discovery, missingAssemblyNames: ["Fixture"]);
        var runner = new FakeContractRunner(session);
        var runnerSetupService = new FakeRunnerSetupService { RunnerToReturn = runner };
        var preparationService = new ArchitectureBaselineApplicationServiceBuildStateTests.FakeBuildStatePreparationService
        {
            ExceptionToThrow = new InvalidOperationException("preflight boom"),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        InvalidOperationException? thrown = Assert.Throws<InvalidOperationException>(() =>
            applicationService.Diff(new BaselineDiffRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "strict",
                NoRestore = true,
            }));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Is.EqualTo("preflight boom"));
            Assert.That(loadScope.DisposeCalled, Is.True,
                "the runner built by BuildRunner must be disposed when the first ordinary-path " +
                "preflight throws before ResolveSetup can hand ownership back to the caller.");
        });
    }

    [Test]
    public void Diff_EnsureBuiltSecondPreflightThrowsAfterPostBuildRerun_DisposesPostBuildRunnerBeforePropagating()
    {
        var document = ArchitectureBaselineApplicationServiceBuildStateTests.CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { ArchitectureBaselineApplicationServiceBuildStateTests.FixtureProject() },
        };
        (ArchitectureAnalysisSession firstSession, FakeAssemblyLoadScope firstLoadScope) =
            CreateSessionWithLoadScopeTracking(document, discovery, missingAssemblyNames: ["Fixture"]);
        (ArchitectureAnalysisSession secondSession, FakeAssemblyLoadScope secondLoadScope) =
            CreateSessionWithLoadScopeTracking(document, discovery, missingAssemblyNames: ["Fixture"]);
        var firstRunner = new FakeContractRunner(firstSession);
        var secondRunner = new FakeContractRunner(secondSession);
        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnersToReturn = new Queue<IArchitectureContractRunner>(
                new IArchitectureContractRunner[] { firstRunner, secondRunner }),
        };
        var preparationService = new ArchitectureBaselineApplicationServiceBuildStateTests.FakeBuildStatePreparationService
        {
            ExceptionToThrow = new InvalidOperationException("preflight boom"),
            ThrowOnCallNumber = 2,
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        InvalidOperationException? thrown = Assert.Throws<InvalidOperationException>(() =>
            applicationService.Diff(new BaselineDiffRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "strict",
                PreparationMode = BuildPreparationMode.EnsureBuilt,
                NoRestore = true,
            }));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Is.EqualTo("preflight boom"));
            Assert.That(preparationService.PrepareCallCount, Is.EqualTo(2));
            Assert.That(firstLoadScope.DisposeCalled, Is.True,
                "the pre-post-build runner is disposed once BuildRunnerForPostBuild succeeds.");
            Assert.That(secondLoadScope.DisposeCalled, Is.True,
                "the post-build runner must also be disposed when the second (post-rerun) preflight " +
                "throws, otherwise it leaks even though the first runner was cleaned up correctly.");
        });
    }

    [Test]
    public void Diff_PreparedPostBuildPreflightCancelled_DisposesMaterializedRunnerBeforePropagatingCancellation()
    {
        // Cancellation reaches ResolveSetup's branches the same way any other preflight exception
        // does (BuildStatePreflightRunner/the preparation service both honor the token and can
        // throw OperationCanceledException) -- this pins that specific exception type alongside the
        // generic-exception coverage above, since a cancelled run must not leak either.
        var document = ArchitectureBaselineApplicationServiceBuildStateTests.CreateDocument();
        var discovery = ProjectDiscoveryResult.Empty with
        {
            DiscoveredProjects = new[] { ArchitectureBaselineApplicationServiceBuildStateTests.FixtureProject() },
        };
        (ArchitectureAnalysisSession session, FakeAssemblyLoadScope loadScope) =
            CreateSessionWithLoadScopeTracking(document, discovery, missingAssemblyNames: ["Fixture"]);
        var preparedRunner = new FakeContractRunner(session);
        var runnerSetupService = new FakeRunnerSetupService
        {
            RunnersToReturn = new Queue<IArchitectureContractRunner>([preparedRunner]),
        };
        var preparationService = new ArchitectureBaselineApplicationServiceBuildStateTests.FakeBuildStatePreparationService
        {
            ExceptionToThrow = new OperationCanceledException(),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), new FakeBaselineLoadingService(), preparationService);

        Assert.Throws<OperationCanceledException>(() =>
            applicationService.Diff(new BaselineDiffRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "strict",
                PreparationMode = BuildPreparationMode.Ordinary,
                UsePreparedPostBuildState = true,
                PreparedPostBuildRunner = ArchitectureBaselineApplicationServiceBuildStateTests.CreatePreparedRunner(discovery),
            }));

        Assert.That(loadScope.DisposeCalled, Is.True,
            "cancellation must dispose the materialized runner just like any other preflight exception.");
    }
}
