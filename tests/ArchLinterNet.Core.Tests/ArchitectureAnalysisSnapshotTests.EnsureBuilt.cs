using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureAnalysisSnapshotTests
{
    [Test]
    public void CreateSnapshot_EnsureBuilt_EvaluatesStrictAndAuditFromOnePreparedSnapshot()
    {
        ArchitectureContractDocument document = CreateDocument();
        var runnerSetupService = new EnsureBuiltMetadataRunnerSetupService { DocumentToReturn = document };

        var discovery = new ProjectDiscoveryResult(
            _value3, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject("Fixture.csproj", "Fixture", _value4)
            }
        };
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", Array.Empty<Assembly>(), _value5, Array.Empty<string>(),
            projectDiscovery: discovery);
        var session = new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
        runnerSetupService.RunnerToReturn = new FakeContractRunner(session);

        var contractExecutor = new CountingContractExecutor();
        var buildStatePreparationService = new FakeBuildStatePreparationService();
        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService,
            new FakeContractHandlerRegistry(),
            contractExecutor,
            buildStatePreparationService);

        using ArchitectureAnalysisSnapshot snapshot = applicationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            PreparationMode = BuildPreparationMode.EnsureBuilt,
        });

        // The fixture's non-empty MissingAssemblyNames (_value5) keeps BuildStatePreflightRunner.Run
        // from short-circuiting before reaching IBuildStatePreparationService.Prepare, the real seam
        // CreateSnapshot uses for build/preflight preparation (see BuildSnapshot in
        // ArchitectureValidationApplicationService). Capture the count here, before either mode is
        // evaluated, so the asserts below prove preparation happens once for the whole snapshot
        // rather than once per Evaluate call.
        int prepareCallCountAfterSnapshot = buildStatePreparationService.PrepareCallCount;

        ValidationOutcome strict = snapshot.Evaluate("strict");
        ValidationOutcome audit = snapshot.Evaluate("audit");

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.True);
            Assert.That(audit.Passed, Is.True);
            Assert.That(runnerSetupService.LoadDocumentCallCount, Is.EqualTo(1));
            Assert.That(runnerSetupService.PrepareRunnerCallCount, Is.EqualTo(1),
                "ensure-built preparation must be shared by both mode evaluations");
            Assert.That(runnerSetupService.BuildRunnerCallCount, Is.EqualTo(1),
                "the prepared runner may materialize once, but never once per mode");
            Assert.That(prepareCallCountAfterSnapshot, Is.GreaterThan(0),
                "the fixture's MissingAssemblyNames must be non-empty so this actually exercises " +
                "IBuildStatePreparationService.Prepare instead of the no-op short-circuit path");
            Assert.That(buildStatePreparationService.PrepareCallCount, Is.EqualTo(prepareCallCountAfterSnapshot),
                "build-state preparation must complete during CreateSnapshot; evaluating additional " +
                "modes must not trigger another build/preflight preparation");
            Assert.That(contractExecutor.CallCountByMode, Has.Count.EqualTo(2));
            Assert.That(snapshot.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(snapshot.Counters.ModesEvaluated, Is.EqualTo(2));
        });
    }

    private sealed class EnsureBuiltMetadataRunnerSetupService : IArchitectureRunnerSetupService
    {
        public int BuildRunnerCallCount { get; private set; }

        public int LoadDocumentCallCount { get; private set; }

        public int PrepareRunnerCallCount { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public FakeContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            LoadDocumentCallCount++;
            return DocumentToReturn;
        }

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            BuildRunnerCallCount++;
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            return BuildRunner(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
                enableUnmatchedIgnoreTracking, timing, mode, cancellationToken, maxParallelism);
        }

        public ArchitectureRunnerPreparation PrepareRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            string? mode = null,
            CancellationToken cancellationToken = default)
        {
            PrepareRunnerCallCount++;
            ArchitectureAnalysisContext context = RunnerToReturn.Session.Context;
            return new ArchitectureRunnerPreparation(
                context.RepositoryRoot,
                preprocessorSymbols,
                context.ProjectDiscovery ?? new ProjectDiscoveryResult(
                    Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<ArchitectureProjectDiscoveryDiagnostic>()),
                ResolveAssemblyOutputs: true,
                context.SelectedAssemblyArtifactPaths,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                context.MissingAssemblyNames.ToArray(),
                IsMetadataReferenceClosureComplete: false);
        }

        public ArchitectureRunnerSetup MaterializePreparedRunner(
            ArchitectureContractDocument document,
            ArchitectureRunnerPreparation preparation,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            return BuildRunner(document, "prepared-by-fake", selectedContractIds: selectedContractIds,
                enableUnmatchedIgnoreTracking: enableUnmatchedIgnoreTracking, timing: timing, mode: mode,
                cancellationToken: cancellationToken, maxParallelism: maxParallelism);
        }
    }
}
