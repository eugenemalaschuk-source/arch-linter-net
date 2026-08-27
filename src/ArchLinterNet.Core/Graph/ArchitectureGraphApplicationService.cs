using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed partial class ArchitectureGraphApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    IArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor,
    IBuildStatePreparationService? buildStatePreparationService)
    : IArchitectureGraphApplicationService
{
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

    public ArchitectureGraphApplicationService(
        IArchitectureRunnerSetupService runnerSetupService,
        IArchitectureContractHandlerRegistry handlerRegistry,
        IArchitectureContractExecutor contractExecutor)
        : this(runnerSetupService, handlerRegistry, contractExecutor, buildStatePreparationService: null)
    {
    }

    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        if (request.Mode is not (ModeStrict or ModeAudit or "all"))
        {
            throw new ArgumentException($"Invalid mode: {request.Mode}. Use 'strict', 'audit', or 'all'.", nameof(request));
        }

        ArchitectureAnalysisSession session = BuildSession(
            request,
            out List<ArchitectureViolation> violations,
            out IReadOnlyCollection<Reporting.ArchitectureCoverageSummary> coverageSummaries);

        try
        {
            ArchitectureDependencyGraph graph = ArchitectureDependencyGraphBuilder.Build(
                session, request.Level, violations,
                out IReadOnlyDictionary<(string Source, string Target), IReadOnlyList<ArchitectureViolation>> edgeViolations);
            return new ArchitectureGraphOutcome(graph)
            {
                EdgeViolations = edgeViolations,
                CoverageSummaries = coverageSummaries,
                SourceExpansion = session.Document.SourceExpansion,
                SelectorParticipation = session.SubtractiveMatcherParticipation
            };
        }
        finally
        {
            // BuildGraph materializes the entire public outcome before returning. The session and
            // its isolated post-build load scope do not escape, so this method owns disposal.
            session.Context.Dispose();
        }
    }

    internal ArchitectureAnalysisSession BuildSession(
        ArchitectureGraphRequest request,
        out List<ArchitectureViolation> violations,
        out IReadOnlyCollection<Reporting.ArchitectureCoverageSummary> coverageSummaries)
    {
        ArchitectureContractDocument document;
        try
        {
            document = runnerSetupService.LoadDocument(request.PolicyPath);
        }
        catch (ArchitecturePolicyImportException ex)
        {
            throw new ArchitecturePolicyLoadException(ex.Message, ex.Diagnostic, ex.Category.ToString() ?? "unknown", ex);
        }

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

        if (request.UsePreparedPostBuildState
            && request.RequestedTargetFramework is not null)
        {
            // MaterializePreparedRunner uses the supplied exact artifact paths; retain the
            // effective framework only for shared-framework probing, which must follow the
            // same caller-selected context rather than the policy default.
            document.Analysis.TargetFramework = request.RequestedTargetFramework;
        }

        ArchitectureRunnerSetup setup = request.UsePreparedPostBuildState
            ? runnerSetupService.MaterializePreparedRunner(
                document,
                request.PreparedPostBuildRunner
                    ?? throw new InvalidOperationException("Prepared graph analysis requires validation's receipt-backed artifact selection."),
                selectedContractIds: selectedIds,
                enableUnmatchedIgnoreTracking: false,
                mode: request.Mode == "all" ? null : request.Mode)
            : runnerSetupService.BuildRunner(
                document,
                request.PolicyPath,
                request.ConditionSetName,
                selectedContractIds: selectedIds,
                enableUnmatchedIgnoreTracking: false,
                mode: request.Mode == "all" ? null : request.Mode);

        setup = PrepareBuildStateRunner(request, document, selectedIds, setup);

        IArchitectureContractRunner runner = setup.Runner;

        violations = new List<ArchitectureViolation>();
        coverageSummaries = Array.Empty<Reporting.ArchitectureCoverageSummary>();
        violations.AddRange(runner.CheckConfiguration(strict: request.Mode != ModeAudit));

        bool includeStrict = request.Mode is ModeStrict or "all";
        bool includeAudit = request.Mode is ModeAudit or "all";

        if (includeStrict)
        {
            ArchitectureContractExecutionResult strictExecution =
                contractExecutor.Execute(runner.Session, ModeStrict, handlerRegistry, includeAsmdefContracts: false);
            violations.AddRange(strictExecution.Violations);
            coverageSummaries = coverageSummaries.Concat(strictExecution.CoverageSummaries).ToArray();
        }

        if (includeAudit)
        {
            ArchitectureContractExecutionResult auditExecution =
                contractExecutor.Execute(runner.Session, ModeAudit, handlerRegistry, includeAsmdefContracts: false);
            violations.AddRange(auditExecution.Violations);
            coverageSummaries = coverageSummaries.Concat(auditExecution.CoverageSummaries).ToArray();
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
