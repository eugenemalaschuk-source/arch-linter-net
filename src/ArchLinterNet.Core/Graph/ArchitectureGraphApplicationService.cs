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
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        if (request.Mode is not (ModeStrict or ModeAudit or "all"))
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

        if (selectedIds != null)
        {
            HashSet<string> availableIds = CollectAvailableContractIds(document, request.Mode);
            List<string> unknownIds = selectedIds.Where(id => !availableIds.Contains(id)).ToList();

            if (unknownIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                    $"Available IDs in {request.Mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
            }
        }

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
            document,
            request.PolicyPath,
            request.ConditionSetName,
            selectedContractIds: selectedIds,
            enableUnmatchedIgnoreTracking: false);

        IArchitectureContractRunner runner = setup.Runner;

        violations = new List<ArchitectureViolation>();
        violations.AddRange(runner.CheckConfiguration(strict: request.Mode != ModeAudit));

        bool includeStrict = request.Mode is ModeStrict or "all";
        bool includeAudit = request.Mode is ModeAudit or "all";

        if (includeStrict)
        {
            violations.AddRange(
                contractExecutor.Execute(runner.Session, ModeStrict, handlerRegistry, includeAsmdefContracts: false)
                    .Violations);
        }

        if (includeAudit)
        {
            violations.AddRange(
                contractExecutor.Execute(runner.Session, ModeAudit, handlerRegistry, includeAsmdefContracts: false)
                    .Violations);
        }

        return runner.Session;
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string mode)
    {
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        if (mode == "all")
        {
            HashSet<string> ids = new(catalog.AvailableContractIds(ModeStrict), StringComparer.OrdinalIgnoreCase);
            ids.UnionWith(catalog.AvailableContractIds(ModeAudit));
            return ids;
        }

        return catalog.AvailableContractIds(mode);
    }
}
