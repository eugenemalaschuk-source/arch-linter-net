using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Same fake-service-composition style as ArchitectureValidationApplicationServiceFakeCompositionTests:
// ArchitectureBaselineApplicationService's four collaborators are all interfaces, so baseline
// generation's request validation, configuration-violation short-circuit, and strict/audit mode
// selection can be proven without a real runner, executor, or YAML serializer.
[TestFixture]
public sealed class ArchitectureBaselineApplicationServiceFakeCompositionTests
{
    private sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
    {
        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
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
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner : IArchitectureContractRunner
    {
        public FakeContractRunner(ArchitectureAnalysisSession session)
        {
            Session = session;
        }

        public List<ArchitectureViolation> ConfigurationViolationsToReturn { get; set; } = new();

        public ArchitectureAnalysisSession Session { get; }

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }
            = Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

        public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; set; }
            = Array.Empty<ArchitectureBaselineCandidate>();

        public List<ArchitectureViolation> CheckConfiguration()
        {
            return CheckConfiguration(strict: true);
        }

        public List<ArchitectureViolation> CheckConfiguration(bool strict)
        {
            return ConfigurationViolationsToReturn;
        }

        public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency()
        {
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
        public List<string> ModesReceived { get; } = new();

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session,
            string mode,
            IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true,
            ValidationTiming? timing = null)
        {
            ModesReceived.Add(mode);
            return new ArchitectureContractExecutionResult(
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<string>(),
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<ArchitectureCoverageSummary>());
        }
    }

    private sealed class FakeBaselineGenerator : IArchitectureBaselineGenerator
    {
        public bool WasCalled { get; private set; }

        public string ReasonReceived { get; private set; } = string.Empty;

        public string YamlToReturn { get; set; } = "fake-baseline-yaml";

        public ArchitectureBaselineDocument Generate(
            ArchitectureContractDocument policyDocument,
            IReadOnlyList<ArchitectureBaselineCandidate> candidates,
            string reason = "generated baseline")
        {
            WasCalled = true;
            ReasonReceived = reason;
            return new ArchitectureBaselineDocument { Version = 1 };
        }

        public string Serialize(ArchitectureBaselineDocument document)
        {
            return YamlToReturn;
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
    public void Generate_FakeCollaborators_ProducesBaselineWithoutRealInfrastructure()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document));
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator);

        BaselineGenerationOutcome outcome = applicationService.Generate(
            new BaselineGenerationRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                Mode = "all",
                Reason = "fake reason",
            });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.Yaml, Is.EqualTo("fake-baseline-yaml"));
        Assert.That(baselineGenerator.WasCalled, Is.True);
        Assert.That(baselineGenerator.ReasonReceived, Is.EqualTo("fake reason"));
        Assert.That(contractExecutor.ModesReceived, Is.EquivalentTo(new[] { "strict", "audit" }));
    }

    [Test]
    public void Generate_ConfigurationViolationsPresent_ShortCircuitsBeforeExecutorOrGenerator()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
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
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator);

        BaselineGenerationOutcome outcome = applicationService.Generate(
            new BaselineGenerationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "all" });

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.ConfigurationViolations, Has.Count.EqualTo(1));
        Assert.That(contractExecutor.ModesReceived, Is.Empty);
        Assert.That(baselineGenerator.WasCalled, Is.False);
    }

    [Test]
    public void Generate_InvalidMode_ThrowsWithoutCallingAnyCollaborator()
    {
        var runnerSetupService = new FakeRunnerSetupService();
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator);

        Assert.That(
            () => applicationService.Generate(
                new BaselineGenerationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "not-a-real-mode" }),
            Throws.ArgumentException);

        Assert.That(contractExecutor.ModesReceived, Is.Empty);
        Assert.That(baselineGenerator.WasCalled, Is.False);
    }
}
