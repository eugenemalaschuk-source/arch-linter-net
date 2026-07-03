using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// ArchitectureValidatorTests exercises the same "validate a policy" path end-to-end through real
// files, real assemblies, and real Roslyn compilation. ArchitectureValidationApplicationService's
// three collaborators are all interfaces, so this fixture proves the composed application seam
// itself — request validation, outcome aggregation, and severity-gated pass/fail — is reachable
// with fake service composition alone, without ever touching a file system, an assembly, or a
// compiler. New contract-family application-service tests should follow this shape: fake the
// seam's collaborators, not the file system underneath them.
[TestFixture]
public sealed class ArchitectureValidationApplicationServiceFakeCompositionTests
{
    private sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
    {
        public bool LoadDocumentCalled { get; private set; }

        public bool BuildRunnerCalled { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            LoadDocumentCalled = true;
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
            string? mode = null)
        {
            BuildRunnerCalled = true;
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner : IArchitectureContractRunner
    {
        public FakeContractRunner(ArchitectureAnalysisSession session)
        {
            Session = session;
        }

        public bool CheckConfigurationCalled { get; private set; }

        public bool CheckPolicyConsistencyCalled { get; private set; }

        public List<ArchitectureViolation> ConfigurationViolationsToReturn { get; set; } = new();

        public ArchitectureAnalysisSession Session { get; }

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; set; }
            = Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

        public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; }
            = Array.Empty<ArchitectureBaselineCandidate>();

        public List<ArchitectureViolation> CheckConfiguration()
        {
            return CheckConfiguration(strict: true);
        }

        public List<ArchitectureViolation> CheckConfiguration(bool strict)
        {
            CheckConfigurationCalled = true;
            return ConfigurationViolationsToReturn;
        }

        public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency()
        {
            CheckPolicyConsistencyCalled = true;
            return new List<PolicyConsistencyDiagnostic>();
        }
    }

    private sealed class FakeContractHandlerRegistry : IArchitectureContractHandlerRegistry
    {
        public bool TryGetHandler(string family, out IArchitectureContractHandler? handler)
        {
            handler = null;
            return false;
        }

        public ArchitectureHandlerResult Execute(
            string family, ArchitectureAnalysisSession session, IArchitectureContract contract)
        {
            throw new InvalidOperationException("Not expected to be called directly by the application service.");
        }
    }

    private sealed class FakeContractExecutor : IArchitectureContractExecutor
    {
        public bool WasCalled { get; private set; }

        public string? ModeReceived { get; private set; }

        public ArchitectureContractExecutionResult ResultToReturn { get; set; } = new(
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureCoverageSummary>());

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session,
            string mode,
            IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true,
            ValidationTiming? timing = null)
        {
            WasCalled = true;
            ModeReceived = mode;
            return ResultToReturn;
        }
    }

    private static ArchitectureAnalysisSession CreateEmptySession(ArchitectureContractDocument document)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<System.Reflection.Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
    }

    [Test]
    public void Validate_FakeCollaborators_DrivesRunnerAndExecutorWithoutRealInfrastructure()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "off",
                PolicyConsistency = "off",
                Coverage = "off",
            },
        };

        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document));
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();

        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        ValidationOutcome outcome = applicationService.Validate(
            new ValidationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "strict" });

        Assert.That(runnerSetupService.LoadDocumentCalled, Is.True);
        Assert.That(runnerSetupService.BuildRunnerCalled, Is.True);
        Assert.That(runner.CheckConfigurationCalled, Is.True);
        Assert.That(contractExecutor.WasCalled, Is.True);
        Assert.That(contractExecutor.ModeReceived, Is.EqualTo("strict"));
        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
    }

    [Test]
    public void Validate_FakeExecutorReturnsViolations_OutcomeAggregatesConfigurationAndExecutorViolations()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "off",
                PolicyConsistency = "off",
                Coverage = "off",
            },
        };

        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document))
        {
            ConfigurationViolationsToReturn = new List<ArchitectureViolation>
            {
                new("<configuration>", null, "fake-subject", "fake-configuration-violation", new[] { "fake" }),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor
        {
            ResultToReturn = new ArchitectureContractExecutionResult(
                new[] { new ArchitectureViolation("core", null, "contracts", "forbidden dependency", new[] { "fake" }) },
                Array.Empty<string>(),
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<ArchitectureCoverageSummary>()),
        };

        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        ValidationOutcome outcome = applicationService.Validate(
            new ValidationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "strict" });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations, Has.Count.EqualTo(2));
        Assert.That(outcome.Violations, Has.Some.Matches<ArchitectureViolation>(v => v.SourceType == "fake-subject"));
        Assert.That(outcome.Violations, Has.Some.Matches<ArchitectureViolation>(v => v.SourceType == "contracts"));
    }

    [Test]
    public void Validate_InvalidMode_ThrowsWithoutCallingAnyCollaborator()
    {
        var runnerSetupService = new FakeRunnerSetupService();
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();

        var applicationService = new ArchitectureValidationApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        Assert.That(
            () => applicationService.Validate(
                new ValidationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "not-a-real-mode" }),
            Throws.ArgumentException);

        Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        Assert.That(contractExecutor.WasCalled, Is.False);
    }
}
