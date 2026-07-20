using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
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
    private const string ModeStrict = "strict";
    private const string ModeAudit = "audit";

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
            + comparison.ConfigurationErrors.Count + newEntries.Count + comparison.OutOfScope.Count);
        entries.AddRange(comparison.Frozen);
        entries.AddRange(comparison.Resolved);
        entries.AddRange(comparison.ConfigurationErrors);
        entries.AddRange(newEntries);
        entries.AddRange(comparison.OutOfScope);

        ArchitectureBaselineDocument updated = baselineGenerator.BuildFromEntries(entries, existingBaseline.Version);
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

        List<ArchitectureBaselineComparisonEntry> survivors = new(comparison.Frozen.Count + comparison.OutOfScope.Count);
        survivors.AddRange(comparison.Frozen);
        survivors.AddRange(comparison.OutOfScope);

        ArchitectureBaselineDocument pruned = baselineGenerator.BuildFromEntries(survivors, existingBaseline.Version);
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

    public BaselineMigrateOutcome Migrate(BaselineMigrateRequest request)
    {
        if (!request.DryRun)
        {
            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                return Fail("--output is required for a non-dry-run migration. Use --dry-run/--check to report without writing.");
            }

            if (PathsRefersToSameFile(request.OutputPath, request.BaselinePath))
            {
                return Fail("--output must not be the same path as --baseline; baseline migrate never overwrites the source file.");
            }
        }

        ArchitectureBaselineDocument legacyBaseline = baselineLoadingService.Load(request.BaselinePath);
        if (legacyBaseline.Version != 1)
        {
            return Fail(
                $"baseline migrate only upgrades version 1 baselines to version 2; '{request.BaselinePath}' is already version {legacyBaseline.Version}.");
        }

        // A version-2 document cannot preserve version-1 matching semantics for only part of a
        // file — a legacy entry's exact-pair identity might be ambiguous under structured identity,
        // and that can only be discovered by actually correlating it. So migrate never scopes by
        // --mode/--contract: every entry in the file is always classified against the full current
        // candidate set (which is why candidates are always collected with mode "all" and no
        // --contract restriction) before anything is written.
        (_, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, "all", request.ConditionSetName, contractIds: null);

        if (candidates == null)
        {
            return new BaselineMigrateOutcome(
                false, null, 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), configViolations);
        }

        var report = new List<BaselineMigrateEntryReport>();
        var migratedEntries = new List<ArchitectureBaselineComparisonEntry>();
        int matched = 0, stale = 0, ambiguous = 0;

        foreach (string groupName in ArchitectureBaselineContractGroups.GroupNames)
        {
            foreach (var entry in legacyBaseline.Baseline.GetGroup(groupName))
            {
                foreach (var ignore in entry.IgnoredViolations)
                {
                    List<ArchitectureBaselineCandidate> matches = candidates
                        .Where(c => c.ContractGroup == groupName
                            && string.Equals(c.ContractId, entry.Id, StringComparison.OrdinalIgnoreCase)
                            && c.SourceType == ignore.SourceType
                            && c.ForbiddenReference == ignore.ForbiddenReference)
                        .ToList();

                    if (matches.Count == 1)
                    {
                        matched++;
                        ArchitectureBaselineCandidate candidate = matches[0];
                        migratedEntries.Add(new ArchitectureBaselineComparisonEntry(
                            groupName, entry.Id, candidate.SourceType, candidate.ForbiddenReference, ignore.Reason, candidate.Identity));
                        report.Add(new BaselineMigrateEntryReport(
                            groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference, "matched", 1));
                    }
                    else if (matches.Count == 0)
                    {
                        stale++;
                        report.Add(new BaselineMigrateEntryReport(
                            groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference, "stale", 0));
                    }
                    else
                    {
                        ambiguous++;
                        report.Add(new BaselineMigrateEntryReport(
                            groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference, "ambiguous", matches.Count));
                    }
                }
            }
        }

        bool canWrite = !request.DryRun && ambiguous == 0;
        string? yaml = null;
        if (canWrite)
        {
            ArchitectureBaselineDocument migrated = baselineGenerator.BuildFromEntries(
                migratedEntries, version: ArchitectureViolationIdentity.CurrentVersion);
            yaml = baselineGenerator.Serialize(migrated);
        }

        bool succeeded = request.DryRun ? ambiguous == 0 : canWrite;

        return new BaselineMigrateOutcome(succeeded, yaml, matched, stale, ambiguous, report, Array.Empty<ArchitectureViolation>());
    }

    private static BaselineMigrateOutcome Fail(string error)
    {
        return new BaselineMigrateOutcome(
            false, null, 0, 0, 0, Array.Empty<BaselineMigrateEntryReport>(), Array.Empty<ArchitectureViolation>(), error);
    }

    private static bool PathsRefersToSameFile(string outputPath, string baselinePath)
    {
        string normalizedOutput = Path.GetFullPath(outputPath);
        string normalizedBaseline = Path.GetFullPath(baselinePath);
        return string.Equals(normalizedOutput, normalizedBaseline, StringComparison.OrdinalIgnoreCase);
    }

    private (ArchitectureContractDocument Document, IReadOnlyList<ArchitectureBaselineCandidate>? Candidates, List<ArchitectureViolation> ConfigurationViolations)
        CollectCandidates(string policyPath, string mode, string? conditionSetName, IReadOnlyCollection<string>? contractIds)
    {
        if (mode is not (ModeStrict or ModeAudit or "all"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.", nameof(mode));
        }

        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath);

        HashSet<string>? selectedContractIds = contractIds is { Count: > 0 }
            ? new HashSet<string>(contractIds, StringComparer.OrdinalIgnoreCase)
            : null;

        if (selectedContractIds != null)
        {
            HashSet<string> availableIds = CollectAvailableContractIds(document, mode);
            List<string> unknownIds = selectedContractIds.Where(id => !availableIds.Contains(id)).ToList();

            if (unknownIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unknown contract IDs: {string.Join(", ", unknownIds)}{Environment.NewLine}" +
                    $"Available IDs in {mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
            }
        }

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(
            document,
            policyPath,
            conditionSetName,
            selectedContractIds: selectedContractIds,
            enableUnmatchedIgnoreTracking: true,
            mode: mode == "all" ? null : mode);

        IArchitectureContractRunner runner = setup.Runner;

        List<ArchitectureViolation> configViolations = mode switch
        {
            ModeStrict => runner.CheckConfiguration(strict: true),
            ModeAudit => runner.CheckConfiguration(strict: false),
            "all" => runner.CheckConfiguration(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported baseline mode."),
        };

        if (configViolations.Count > 0)
        {
            return (document, null, configViolations);
        }

        bool includeStrict = mode is ModeStrict or "all";
        bool includeAudit = mode is ModeAudit or "all";

        if (includeStrict)
        {
            contractExecutor.Execute(runner.Session, ModeStrict, handlerRegistry, includeAsmdefContracts: false);
        }

        if (includeAudit)
        {
            contractExecutor.Execute(runner.Session, ModeAudit, handlerRegistry, includeAsmdefContracts: false);
        }

        return (document, runner.BaselineCandidates, new List<ArchitectureViolation>());
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
