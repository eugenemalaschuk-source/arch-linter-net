using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

public sealed class ArchitectureBaselineApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    IArchitectureContractHandlerRegistry handlerRegistry,
    IArchitectureContractExecutor contractExecutor,
    IArchitectureBaselineGenerator baselineGenerator,
    IArchitectureBaselineLoadingService baselineLoadingService)
    : IArchitectureBaselineApplicationService
{
    public BaselineGenerationOutcome Generate(BaselineGenerationRequest request)
    {
        (ArchitectureContractDocument document, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselineGenerationOutcome(
                Succeeded: false, Yaml: null, CandidateCount: 0, ConfigurationViolations: configViolations);
        }

        ArchitectureBaselineDocument baseline = baselineGenerator.Generate(document, candidates, request.Reason);
        string yaml = baselineGenerator.Serialize(baseline);

        return new BaselineGenerationOutcome(
            Succeeded: true,
            Yaml: yaml,
            CandidateCount: candidates.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }

    public BaselineUpdateOutcome Update(BaselineUpdateRequest request)
    {
        (ArchitectureContractDocument document, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselineUpdateOutcome(
                Succeeded: false, Yaml: null, PreservedCount: 0, NewCount: 0, ConfigurationViolations: configViolations);
        }

        ArchitectureBaselineDocument existingBaseline = baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        var newEntries = comparison.New
            .Select(e => e with { Reason = request.Reason })
            .ToList();

        List<ArchitectureBaselineComparisonEntry> entries = new(comparison.Frozen.Count + comparison.Resolved.Count
            + comparison.ConfigurationErrors.Count + newEntries.Count);
        entries.AddRange(comparison.Frozen);
        entries.AddRange(comparison.Resolved);
        entries.AddRange(comparison.ConfigurationErrors);
        entries.AddRange(newEntries);

        ArchitectureBaselineDocument updated = baselineGenerator.BuildFromEntries(entries);
        string yaml = baselineGenerator.Serialize(updated);

        return new BaselineUpdateOutcome(
            Succeeded: true,
            Yaml: yaml,
            PreservedCount: comparison.Frozen.Count,
            NewCount: newEntries.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }

    public BaselinePruneOutcome Prune(BaselinePruneRequest request)
    {
        (ArchitectureContractDocument document, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselinePruneOutcome(
                Succeeded: false, Yaml: null, RemovedEntries: Array.Empty<BaselineRemovedEntry>(), ConfigurationViolations: configViolations);
        }

        ArchitectureBaselineDocument existingBaseline = baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        ArchitectureBaselineDocument pruned = baselineGenerator.BuildFromEntries(comparison.Frozen);
        string yaml = baselineGenerator.Serialize(pruned);

        List<BaselineRemovedEntry> removed = comparison.Resolved
            .Select(e => new BaselineRemovedEntry(e, "resolved"))
            .Concat(comparison.ConfigurationErrors.Select(e => new BaselineRemovedEntry(e, "configuration-error")))
            .ToList();

        return new BaselinePruneOutcome(
            Succeeded: true,
            Yaml: yaml,
            RemovedEntries: removed,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }

    public BaselineDiffOutcome Diff(BaselineDiffRequest request)
    {
        (ArchitectureContractDocument document, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselineDiffOutcome(
                Succeeded: false,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: configViolations);
        }

        ArchitectureBaselineDocument existingBaseline = baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        return new BaselineDiffOutcome(
            Succeeded: true,
            New: comparison.New,
            Frozen: comparison.Frozen,
            Resolved: comparison.Resolved,
            ConfigurationErrors: comparison.ConfigurationErrors,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }

    public BaselineVerifyOutcome Verify(BaselineVerifyRequest request)
    {
        (ArchitectureContractDocument document, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselineVerifyOutcome(
                Succeeded: false,
                InSync: false,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: configViolations);
        }

        ArchitectureBaselineDocument existingBaseline = baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        bool inSync = comparison.Resolved.Count == 0 && comparison.ConfigurationErrors.Count == 0;

        return new BaselineVerifyOutcome(
            Succeeded: true,
            InSync: inSync,
            New: comparison.New,
            Frozen: comparison.Frozen,
            Resolved: comparison.Resolved,
            ConfigurationErrors: comparison.ConfigurationErrors,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>());
    }

    private (ArchitectureContractDocument Document, IReadOnlyList<ArchitectureBaselineCandidate>? Candidates, List<ArchitectureViolation> ConfigurationViolations)
        CollectCandidates(string policyPath, string mode, string? conditionSetName, IReadOnlyCollection<string>? contractIds)
    {
        if (mode is not ("strict" or "audit" or "all"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath);

        HashSet<string>? selectedContractIds = contractIds is { Count: > 0 }
            ? new HashSet<string>(contractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
            document, policyPath, conditionSetName, selectedContractIds: selectedContractIds, enableUnmatchedIgnoreTracking: true);

        IArchitectureContractRunner runner = setup.Runner;

        List<ArchitectureViolation> configViolations = runner.CheckConfiguration(strict: true);
        if (configViolations.Count > 0)
        {
            return (document, null, configViolations);
        }

        bool includeStrict = mode is "strict" or "all";
        bool includeAudit = mode is "audit" or "all";

        if (includeStrict)
        {
            contractExecutor.Execute(runner.Session, "strict", handlerRegistry, includeAsmdefContracts: false);
        }

        if (includeAudit)
        {
            contractExecutor.Execute(runner.Session, "audit", handlerRegistry, includeAsmdefContracts: false);
        }

        return (document, runner.BaselineCandidates, new List<ArchitectureViolation>());
    }
}
