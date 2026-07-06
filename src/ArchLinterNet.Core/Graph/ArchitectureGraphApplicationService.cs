using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed class ArchitectureGraphApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    IArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor)
    : IArchitectureGraphApplicationService
{
    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        if (request.Mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict', 'audit', or 'all'.", nameof(request));
        }

        ArchitectureAnalysisSession session = BuildSession(request, out List<ArchitectureViolation> violations);

        ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(session, request.Level, violations);
        return new ArchitectureGraphOutcome(graph);
    }

    internal ArchitectureAnalysisSession BuildSession(
        ArchitectureGraphRequest request,
        out List<ArchitectureViolation> violations)
    {
        ArchitectureContractDocument document = runnerSetupService.LoadDocument(request.PolicyPath);

        HashSet<string>? selectedIds = request.ContractIds is { Count: > 0 }
            ? new HashSet<string>(request.ContractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
            document,
            request.PolicyPath,
            request.ConditionSetName,
            selectedContractIds: selectedIds,
            enableUnmatchedIgnoreTracking: false);

        IArchitectureContractRunner runner = setup.Runner;

        violations = new List<ArchitectureViolation>();
        violations.AddRange(runner.CheckConfiguration(strict: request.Mode != "audit"));

        bool includeStrict = request.Mode is "strict" or "all";
        bool includeAudit = request.Mode is "audit" or "all";

        if (includeStrict)
        {
            violations.AddRange(
                contractExecutor.Execute(runner.Session, "strict", handlerRegistry, includeAsmdefContracts: false)
                    .Violations);
        }

        if (includeAudit)
        {
            violations.AddRange(
                contractExecutor.Execute(runner.Session, "audit", handlerRegistry, includeAsmdefContracts: false)
                    .Violations);
        }

        return runner.Session;
    }
}
