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
        if (!BaselineReasonMap.TryParse(
                request.ReasonForContract, request.ReasonForFamily, request.Reason,
                out BaselineReasonMap reasonMap, out string? reasonError))
        {
            return new BaselineGenerationOutcome(
                Succeeded: false, Yaml: null, CandidateCount: 0, ConfigurationViolations: Array.Empty<ArchitectureViolation>())
            {
                Error = reasonError,
            };
        }

        (_, IReadOnlyList<ArchitectureBaselineCandidate>? candidates, List<ArchitectureViolation> configViolations) =
            CollectCandidates(request.PolicyPath, request.Mode, request.ConditionSetName, request.ContractIds);

        if (candidates == null)
        {
            return new BaselineGenerationOutcome(
                Succeeded: false, Yaml: null, CandidateCount: 0, ConfigurationViolations: configViolations);
        }

        BaselineWritePlan plan = BaselineWritePlanner.PlanGenerate(candidates, reasonMap);
        ArchitectureBaselineDocument baseline = baselineGenerator.BuildFromEntries(
            plan.OutputEntries, ArchitectureViolationIdentity.CurrentVersion);

        return new BaselineGenerationOutcome(
            Succeeded: true,
            Yaml: baselineGenerator.Serialize(baseline),
            CandidateCount: candidates.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>())
        {
            Entries = plan.LifecycleEntries,
        };
    }

    public BaselineUpdateOutcome Update(BaselineUpdateRequest request)
    {
        if (!BaselineReasonMap.TryParse(
                request.ReasonForContract, request.ReasonForFamily, request.Reason,
                out BaselineReasonMap reasonMap, out string? reasonError))
        {
            return new BaselineUpdateOutcome(
                Succeeded: false, Yaml: null, PreservedCount: 0, NewCount: 0,
                ConfigurationViolations: Array.Empty<ArchitectureViolation>())
            {
                Error = reasonError,
            };
        }

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

        BaselineWritePlan plan = BaselineWritePlanner.PlanUpdate(comparison, reasonMap);
        ArchitectureBaselineDocument updated = baselineGenerator.BuildFromEntries(plan.OutputEntries, existingBaseline.Version);

        BaselineCommentInspection comments = InspectComments(request.BaselinePath);

        return new BaselineUpdateOutcome(
            Succeeded: true,
            Yaml: comments.Header + baselineGenerator.Serialize(updated),
            PreservedCount: comparison.Frozen.Count,
            NewCount: comparison.New.Count,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>())
        {
            Entries = plan.LifecycleEntries,
            CommentDiagnostic = DescribeCommentRefusal("baseline update", request.BaselinePath, comments),
        };
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

        BaselineWritePlan plan = BaselineWritePlanner.PlanPrune(comparison);

        List<BaselineRemovedEntry> removed = comparison.Resolved
            .Select(e => new BaselineRemovedEntry(e, BaselineEntryLifecycleNames.Resolved))
            .Concat(comparison.ConfigurationErrors.Select(
                e => new BaselineRemovedEntry(e, BaselineEntryLifecycleNames.Stale)))
            .ToList();

        string rawBaseline = baselineLoadingService.ReadRawText(request.BaselinePath);
        BaselineCommentInspection comments = BaselineCommentInspector.Inspect(rawBaseline);

        // Nothing to remove means the input already is the answer. Reserializing it would be a
        // no-op prune that still rewrote quoting, line endings, or blank-line placement — the file
        // has to come back byte-for-byte identical.
        string yaml = plan.RemovesNothing
            ? rawBaseline
            : comments.Header + baselineGenerator.Serialize(
                baselineGenerator.BuildFromEntries(plan.OutputEntries, existingBaseline.Version));

        return new BaselinePruneOutcome(
            Succeeded: true,
            Yaml: yaml,
            RemovedEntries: removed,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>())
        {
            Entries = plan.LifecycleEntries,
            IsNoOp = plan.RemovesNothing,
            // A no-op prune rewrites nothing, so unpreservable comments cannot be lost by it either.
            CommentDiagnostic = plan.RemovesNothing
                ? null
                : DescribeCommentRefusal("baseline prune", request.BaselinePath, comments),
        };
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
            ConfigurationViolations: Array.Empty<ArchitectureViolation>())
        {
            Ambiguous = comparison.Ambiguous,
            Entries = BaselineWritePlanner.Report(comparison),
        };
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

        // Ambiguity is out-of-sync too: one entry standing in for several distinct violations
        // suppresses more than it was reviewed for.
        bool inSync = comparison.Resolved.Count == 0
            && comparison.ConfigurationErrors.Count == 0
            && comparison.Ambiguous.Count == 0;

        return new BaselineVerifyOutcome(
            Succeeded: true,
            InSync: inSync,
            New: comparison.New,
            Frozen: comparison.Frozen,
            Resolved: comparison.Resolved,
            ConfigurationErrors: comparison.ConfigurationErrors,
            ConfigurationViolations: Array.Empty<ArchitectureViolation>())
        {
            Ambiguous = comparison.Ambiguous,
            Entries = BaselineWritePlanner.Report(comparison),
        };
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
                            groupName, entry.Id, candidate.SourceType, candidate.ForbiddenReference, ignore.Reason, candidate.Identity)
                        {
                            Issue = ignore.Issue,
                        });
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

        // A dry run must show the deterministic portion of the migration even when ambiguities
        // make the result unsafe to write. The caller keeps the write gate closed in that case.
        bool writable = ambiguous == 0;
        ArchitectureBaselineDocument migrated = baselineGenerator.BuildFromEntries(
            migratedEntries, version: ArchitectureViolationIdentity.CurrentVersion);
        string yaml = baselineGenerator.Serialize(migrated);

        return new BaselineMigrateOutcome(writable, yaml, matched, stale, ambiguous, report, Array.Empty<ArchitectureViolation>());
    }

    private BaselineCommentInspection InspectComments(string baselinePath)
    {
        return BaselineCommentInspector.Inspect(baselineLoadingService.ReadRawText(baselinePath));
    }

    // Reported rather than thrown: classification and `--dry-run` reporting stay available on a file
    // whose comments block an in-place rewrite, which is what makes the refusal actionable.
    private static string? DescribeCommentRefusal(string command, string baselinePath, BaselineCommentInspection comments)
    {
        return comments.CanRoundTrip
            ? null
            : BaselineCommentInspector.DescribeRefusal(command, baselinePath, comments.UnanchorableCommentLines);
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
