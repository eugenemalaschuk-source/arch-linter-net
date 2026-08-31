using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureContractExecutor : IArchitectureContractExecutor
{
    private const string CoverageFamily = "coverage";
    private const string MetricBudgetFamily = "metric_budgets";
    private const string AsmdefFamily = "asmdef";

    public ArchitectureContractExecutionResult Execute(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        bool includeAsmdefContracts = true,
        ValidationTiming? timing = null)
    {
        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        session.PrepareRuleInputCoverageDeferral(mode);

        var accumulator = new ExecutionAccumulator();
        List<ArchitectureViolation> coverageViolations = new();
        List<ArchitectureCoverageSummary> coverageSummaries = new();

        // Iterating the catalog's families rather than a hardcoded per-family list means a new
        // violations-or-cycles-shaped family (added to ArchitectureContractCatalog.Build plus a
        // registered handler) is dispatched here with no executor changes. "coverage" (separate
        // output bucket + summary) and "asmdef" (CLI-controlled inclusion toggle) are genuine
        // runtime decisions, not god-executor ceremony, so they stay as small special cases.
        foreach (string family in session.Catalog.FamiliesInOrder)
        {
            session.Context.CancellationToken.ThrowIfCancellationRequested();

            if (family == CoverageFamily)
            {
                ExecuteCoverageFamily(session, mode, handlerRegistry, timing, coverageViolations, coverageSummaries, accumulator.ResultCounts);
                continue;
            }

            if (family == MetricBudgetFamily)
            {
                ExecuteMetricBudgetFamily(session, mode, timing, accumulator);
                continue;
            }

            if (family == AsmdefFamily && !includeAsmdefContracts)
            {
                continue;
            }

            ExecuteStandardFamily(session, mode, family, handlerRegistry, timing, accumulator);
        }

        // Topology is an opt-in document control rather than a contracts.<mode> family. Evaluate
        // it once per requested validation mode after ordinary contract checks have populated the
        // shared reference graph, then transport its native applicability evidence through the
        // same result boundary as every other v0.8 control.
        ArchitectureTopologyEvaluator.Result topology = ArchitectureTopologyEvaluator.EvaluateForValidation(session);
        if (topology.Violations.Count > 0)
        {
            int identityCursor = session.FindingIdentityCursor;
            ArchitectureViolation[] violations = session.AttachFindingIdentities(topology.Violations, identityCursor).ToArray();
            accumulator.Findings.Violations.AddRange(violations);
            AddResultCount(accumulator.ResultCounts, ArchitectureTopologyEvaluator.Family, violations.Length);
            session.Context.ProfilingCounters.RecordContractFamilyResults(ArchitectureTopologyEvaluator.Family, violations.Length);
        }

        return new ArchitectureContractExecutionResult(
            accumulator.Findings.Violations,
            accumulator.Findings.Cycles,
            coverageViolations,
            coverageSummaries)
        {
            CycleFindings = accumulator.Findings.CycleFindings,
            ContractFamilyResultCounts = accumulator.ResultCounts,
            ApplicabilityExpectedEntries = accumulator.ApplicabilityExpected.Concat(topology.ExpectedEntries).ToArray(),
            ApplicabilityRecords = accumulator.ApplicabilityRecords.Concat(topology.Records).ToArray(),
        };
    }

    private static void ExecuteMetricBudgetFamily(
        ArchitectureAnalysisSession session,
        string mode,
        ValidationTiming? timing,
        ExecutionAccumulator accumulator)
    {
        StandardFamilyFindings findings = accumulator.Findings;
        IDictionary<string, int> resultCounts = accumulator.ResultCounts;
        List<ArchitectureApplicabilityExpectedEntry> applicabilityExpected = accumulator.ApplicabilityExpected;
        List<ArchitectureApplicabilityRecord> applicabilityRecords = accumulator.ApplicabilityRecords;
        int[] budgetCount = [0];
        using (timing?.MeasureContractFamily(MetricBudgetFamily, () => budgetCount[0]))
        {
            ArchitectureMetricBudgetContract[] contracts = session.Catalog
                .ContractsFor(mode, MetricBudgetFamily)
                .OfType<ArchitectureMetricBudgetContract>()
                .ToArray();
            foreach (ArchitectureMetricBudgetContract _ in contracts)
            {
                session.Context.CancellationToken.ThrowIfCancellationRequested();
                budgetCount[0]++;
                session.Context.ProfilingCounters.RecordContractExecution();
            }

            if (contracts.Length == 0)
            {
                return;
            }

            int identityCursor = session.FindingIdentityCursor;
            ArchitectureMetricBudgetEvaluationResult result = session.CheckMetricBudgetContracts(contracts);
            Dictionary<string, ArchitectureMetricBudgetContract> contractsById = contracts
                .Where(contract => contract.Id is not null)
                .ToDictionary(contract => contract.Id!, StringComparer.Ordinal);
            ArchitectureViolation[] violations = session.AttachFindingIdentities(result.Violations, identityCursor)
                .Select(violation => contractsById.TryGetValue(violation.ContractId ?? string.Empty, out ArchitectureMetricBudgetContract? contract)
                    ? session.Document.Provenance.Enrich(violation, contract)
                    : violation)
                .ToArray();
            findings.Violations.AddRange(violations);
            applicabilityExpected.AddRange(result.ApplicabilityExpectedEntries);
            applicabilityRecords.AddRange(result.ApplicabilityRecords);

            AddResultCount(resultCounts, MetricBudgetFamily, violations.Length);
            session.Context.ProfilingCounters.RecordContractFamilyResults(MetricBudgetFamily, violations.Length);
        }
    }

    private static void ExecuteCoverageFamily(
        ArchitectureAnalysisSession session,
        string mode,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        List<ArchitectureViolation> coverageViolations,
        List<ArchitectureCoverageSummary> coverageSummaries,
        IDictionary<string, int> resultCounts)
    {
        int[] coverageCount = [0];
        using (timing?.MeasureContractFamily(CoverageFamily, () => coverageCount[0]))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, CoverageFamily))
            {
                session.Context.CancellationToken.ThrowIfCancellationRequested();
                coverageCount[0]++;
                session.Context.ProfilingCounters.RecordContractExecution();
                int identityCursor = session.FindingIdentityCursor;
                int resultCount = 0;
                ArchitectureViolation[] violations = session.AttachFindingIdentities(
                        handlerRegistry.Execute(CoverageFamily, session, contract).Violations,
                        identityCursor)
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract))
                    .ToArray();
                coverageViolations.AddRange(violations);
                resultCount += violations.Length;

                ArchitectureCoverageSummary? summary =
                    session.BuildCoverageSummary((ArchitectureCoverageContract)contract);
                if (summary != null)
                {
                    coverageSummaries.Add(summary);
                    resultCount++;
                }

                AddResultCount(resultCounts, CoverageFamily, resultCount);
                session.Context.ProfilingCounters.RecordContractFamilyResults(CoverageFamily, resultCount);
            }
        }
    }

    private static void ExecuteStandardFamily(
        ArchitectureAnalysisSession session,
        string mode,
        string family,
        IArchitectureContractHandlerRegistry handlerRegistry,
        ValidationTiming? timing,
        ExecutionAccumulator accumulator)
    {
        StandardFamilyFindings findings = accumulator.Findings;
        IDictionary<string, int> resultCounts = accumulator.ResultCounts;
        List<ArchitectureApplicabilityExpectedEntry> applicabilityExpected = accumulator.ApplicabilityExpected;
        List<ArchitectureApplicabilityRecord> applicabilityRecords = accumulator.ApplicabilityRecords;
        int[] count = [0];
        using (timing?.MeasureContractFamily(family, () => count[0]))
        {
            foreach (IArchitectureContract contract in session.Catalog.ContractsFor(mode, family))
            {
                session.Context.CancellationToken.ThrowIfCancellationRequested();
                count[0]++;
                session.Context.ProfilingCounters.RecordContractExecution();
                int identityCursor = session.FindingIdentityCursor;
                ArchitectureHandlerResult result = handlerRegistry.Execute(family, session, contract);
                ArchitectureViolation[] violations = session.AttachFindingIdentities(result.Violations, identityCursor)
                    .Select(violation => session.Document.Provenance.Enrich(violation, contract))
                    .ToArray();
                findings.Violations.AddRange(violations);
                string cycleIdPrefix = contract.Id is null ? string.Empty : $"[{contract.Id}] ";
                int cycleCount = 0;
                foreach (string cycle in result.Cycles)
                {
                    findings.Cycles.Add(cycle);
                    cycleCount++;
                    string normalizedPath = cycleIdPrefix.Length > 0 && cycle.StartsWith(cycleIdPrefix, StringComparison.Ordinal)
                        ? cycle[cycleIdPrefix.Length..]
                        : cycle;
                    findings.CycleFindings.Add(session.Document.Provenance.Enrich(
                        new ArchitectureCycleFinding(contract.Name, contract.Id, normalizedPath),
                        contract));
                }

                AddResultCount(resultCounts, family, violations.Length + cycleCount);
                session.Context.ProfilingCounters.RecordContractFamilyResults(family, violations.Length + cycleCount);
                applicabilityExpected.AddRange(result.ApplicabilityExpectedEntries);
                applicabilityRecords.AddRange(result.ApplicabilityRecords);
            }
        }
    }

    private static void AddResultCount(IDictionary<string, int> resultCounts, string family, int count)
    {
        resultCounts.TryGetValue(family, out int current);
        resultCounts[family] = current + count;
    }

    private sealed class StandardFamilyFindings
    {
        public List<ArchitectureViolation> Violations { get; } = new();

        public List<string> Cycles { get; } = new();

        public List<ArchitectureCycleFinding> CycleFindings { get; } = new();
    }

    private sealed class ExecutionAccumulator
    {
        public StandardFamilyFindings Findings { get; } = new();

        public Dictionary<string, int> ResultCounts { get; } = new(StringComparer.Ordinal);

        public List<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpected { get; } = new();

        public List<ArchitectureApplicabilityRecord> ApplicabilityRecords { get; } = new();
    }
}
