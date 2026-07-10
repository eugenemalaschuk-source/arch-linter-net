using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Tests;

internal sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
{
    public bool LoadDocumentCalled { get; private set; }

    public bool BuildRunnerCalled { get; private set; }

    public HashSet<string>? SelectedContractIdsReceived { get; private set; }

    public string? ModeReceived { get; private set; }

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
        SelectedContractIdsReceived = selectedContractIds;
        ModeReceived = mode;
        return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
    }
}

internal sealed class FakeContractRunner : IArchitectureContractRunner
{
    public FakeContractRunner(ArchitectureAnalysisSession session)
    {
        Session = session;
    }

    public List<ArchitectureViolation> ConfigurationViolationsToReturn { get; set; } = new();

    public List<bool> StrictArgumentsReceived { get; } = new();

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
        StrictArgumentsReceived.Add(strict);
        return ConfigurationViolationsToReturn;
    }

    public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency()
    {
        return new List<PolicyConsistencyDiagnostic>();
    }
}

internal sealed class FakeContractHandlerRegistry : IArchitectureContractHandlerRegistry
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

internal sealed class FakeContractExecutor : IArchitectureContractExecutor
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

internal sealed class FakeBaselineGenerator : IArchitectureBaselineGenerator
{
    public bool WasCalled { get; private set; }

    public string ReasonReceived { get; private set; } = string.Empty;

    public string YamlToReturn { get; set; } = "fake-baseline-yaml";

    public IReadOnlyList<ArchitectureBaselineComparisonEntry>? EntriesReceived { get; private set; }

    public ArchitectureBaselineDocument Generate(
        ArchitectureContractDocument policyDocument,
        IReadOnlyList<ArchitectureBaselineCandidate> candidates,
        string reason = "generated baseline")
    {
        WasCalled = true;
        ReasonReceived = reason;
        return new ArchitectureBaselineDocument { Version = 1 };
    }

    public ArchitectureBaselineDocument BuildFromEntries(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
    {
        WasCalled = true;
        EntriesReceived = entries;
        return new ArchitectureBaselineDocument { Version = 1 };
    }

    public string Serialize(ArchitectureBaselineDocument document)
    {
        return YamlToReturn;
    }
}

internal sealed class FakeBaselineLoadingService : IArchitectureBaselineLoadingService
{
    public ArchitectureBaselineDocument DocumentToReturn { get; set; } =
        new() { Version = 1, Baseline = new ArchitectureBaselineContractGroups() };

    public void LoadAndMerge(ArchitectureContractDocument document, string baselinePath)
    {
    }

    public ArchitectureBaselineDocument Load(string baselinePath)
    {
        return DocumentToReturn;
    }
}

internal static class ArchitectureBaselineApplicationServiceHelper
{
    public static ArchitectureAnalysisSession CreateEmptySession(ArchitectureContractDocument document)
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
}
