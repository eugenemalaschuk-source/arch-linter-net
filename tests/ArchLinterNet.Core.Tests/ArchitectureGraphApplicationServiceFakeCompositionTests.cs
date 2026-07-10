using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Fake-composition tests for ArchitectureGraphApplicationService. The two graph-building
// collaborators (runner setup, contract executor) are interfaces, so this fixture proves the
// composed graph seam — mode validation, contract-id filtering against the real contract catalog,
// strict/audit/all dispatch, and violation aggregation into ArchitectureDependencyGraphBuilder —
// is reachable with fake service composition alone, without touching a file system or a compiler.
[TestFixture]
public sealed class ArchitectureGraphApplicationServiceFakeCompositionTests
{
    private static readonly string[] _knownId = ["dep-known"];
    private static readonly string[] _unknownId = ["ghost-id"];
    private static readonly string[] _strictOnly = ["strict"];
    private static readonly string[] _auditOnly = ["audit"];
    private static readonly string[] _strictThenAudit = ["strict", "audit"];

    [Test]
    public void BuildGraph_InvalidMode_ThrowsArgumentExceptionWithoutTouchingCollaborators()
    {
        var runnerSetupService = new FakeRunnerSetupService();
        var (handlerRegistry, contractExecutor) = CreateExecutionFakes();

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest { PolicyPath = "unused.yml", Mode = "not-a-mode" }),
            Throws.ArgumentException.With.Message.Contains("Invalid mode"));

        Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        Assert.That(contractExecutor.ModesReceived, Is.Empty);
    }

    [Test]
    public void BuildGraph_StrictMode_NoContractIds_ExecutesStrictOnlyAndBuildsGraph()
    {
        ArchitectureContractDocument document = DocumentWithStrictDependencyContract();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        ArchitectureGraphOutcome outcome = service.BuildGraph(
            new ArchitectureGraphRequest { PolicyPath = "unused.yml", Mode = "strict" });

        Assert.That(outcome.Graph, Is.Not.Null);
        Assert.That(contractExecutor.ModesReceived, Is.EqualTo(_strictOnly));
    }

    [Test]
    public void BuildGraph_AuditMode_ExecutesAuditOnly()
    {
        ArchitectureContractDocument document = DocumentWithStrictDependencyContract();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        service.BuildGraph(new ArchitectureGraphRequest { PolicyPath = "unused.yml", Mode = "audit" });

        Assert.That(contractExecutor.ModesReceived, Is.EqualTo(_auditOnly));
    }

    [Test]
    public void BuildGraph_AllMode_ExecutesStrictThenAudit()
    {
        ArchitectureContractDocument document = DocumentWithStrictDependencyContract();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        service.BuildGraph(new ArchitectureGraphRequest { PolicyPath = "unused.yml", Mode = "all" });

        Assert.That(contractExecutor.ModesReceived, Is.EqualTo(_strictThenAudit));
    }

    [Test]
    public void BuildGraph_KnownContractId_StrictMode_PassesFilterAndBuildsGraph()
    {
        ArchitectureContractDocument document = DocumentWithStrictDependencyContract();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        ArchitectureGraphOutcome outcome = service.BuildGraph(new ArchitectureGraphRequest
        {
            PolicyPath = "unused.yml",
            Mode = "strict",
            ContractIds = _knownId,
        });

        Assert.That(outcome.Graph, Is.Not.Null);
        Assert.That(contractExecutor.ModesReceived, Is.EqualTo(_strictOnly));
    }

    [Test]
    public void BuildGraph_UnknownContractId_StrictMode_ThrowsWithAvailableIds()
    {
        ArchitectureContractDocument document = DocumentWithStrictDependencyContract();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest
            {
                PolicyPath = "unused.yml",
                Mode = "strict",
                ContractIds = _unknownId,
            }),
            Throws.InvalidOperationException
                .With.Message.Contains("Unknown contract IDs: ghost-id")
                .And.Message.Contains("dep-known"));

        Assert.That(contractExecutor.ModesReceived, Is.Empty);
    }

    [Test]
    public void BuildGraph_UnknownContractId_AllMode_UnionsStrictAndAuditIdsInError()
    {
        ArchitectureContractDocument document = DocumentWithStrictAndAuditDependencyContracts();
        var (runnerSetupService, handlerRegistry, contractExecutor) = ComposeFor(document);

        var service = new ArchitectureGraphApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor);

        Assert.That(
            () => service.BuildGraph(new ArchitectureGraphRequest
            {
                PolicyPath = "unused.yml",
                Mode = "all",
                ContractIds = _unknownId,
            }),
            Throws.InvalidOperationException
                .With.Message.Contains("dep-known").And.Message.Contains("dep-audit"));

        Assert.That(contractExecutor.ModesReceived, Is.Empty);
    }

    private static ArchitectureContractDocument DocumentWithStrictDependencyContract()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict =
                {
                    new ArchitectureDependencyContract { Name = "known", Id = "dep-known", Source = "core" },
                },
            },
        };
    }

    private static ArchitectureContractDocument DocumentWithStrictAndAuditDependencyContracts()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict =
                {
                    new ArchitectureDependencyContract { Name = "known", Id = "dep-known", Source = "core" },
                },
                Audit =
                {
                    new ArchitectureDependencyContract { Name = "audit", Id = "dep-audit", Source = "core" },
                },
            },
        };
    }

    private static (FakeRunnerSetupService, FakeContractHandlerRegistry, FakeContractExecutor) ComposeFor(
        ArchitectureContractDocument document)
    {
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        runnerSetupService.RunnerToReturn = new FakeContractRunner(CreateEmptySession(document));
        var (handlerRegistry, contractExecutor) = CreateExecutionFakes();
        return (runnerSetupService, handlerRegistry, contractExecutor);
    }

    private static (FakeContractHandlerRegistry, FakeContractExecutor) CreateExecutionFakes()
    {
        return (new FakeContractHandlerRegistry(), new FakeContractExecutor());
    }

    private static ArchitectureAnalysisSession CreateEmptySession(ArchitectureContractDocument document)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<System.Reflection.Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: false,
            preprocessorSymbols: null);
    }

    private sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
    {
        public bool LoadDocumentCalled { get; private set; }

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
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner(ArchitectureAnalysisSession session) : IArchitectureContractRunner
    {
        public ArchitectureAnalysisSession Session { get; } = session;

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }
            = Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

        public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; }
            = Array.Empty<ArchitectureBaselineCandidate>();

        public List<ArchitectureViolation> CheckConfiguration() => CheckConfiguration(strict: true);

        public List<ArchitectureViolation> CheckConfiguration(bool strict) => new();

        public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency() => new();
    }

    private sealed class FakeContractHandlerRegistry : IArchitectureContractHandlerRegistry
    {
        public bool TryGetHandler(string family, out ArchitectureContractChecker? checker)
        {
            checker = null;
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
}
