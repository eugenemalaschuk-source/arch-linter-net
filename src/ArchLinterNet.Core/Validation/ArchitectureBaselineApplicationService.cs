using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

public sealed class ArchitectureBaselineApplicationService : IArchitectureBaselineApplicationService
{
    private readonly ArchitectureBaselineCandidateCollector _candidateCollector;
    private readonly IArchitectureBaselineGenerator _baselineGenerator;
    private readonly IArchitectureBaselineLoadingService _baselineLoadingService;

    // Keep the existing public construction seam for consumers that compose Core directly. The
    // application service delegates policy/build/candidate work to the internal collector.
    public ArchitectureBaselineApplicationService(
        IArchitectureRunnerSetupService runnerSetupService,
        IArchitectureContractHandlerRegistry handlerRegistry,
        IArchitectureContractExecutor contractExecutor,
        IArchitectureBaselineGenerator baselineGenerator,
        IArchitectureBaselineLoadingService baselineLoadingService,
        IBuildStatePreparationService? buildStatePreparationService = null)
        : this(
            new ArchitectureBaselineCandidateCollector(
                runnerSetupService, handlerRegistry, contractExecutor, buildStatePreparationService),
            baselineGenerator,
            baselineLoadingService)
    {
    }

    // DI composes the already-registered collector, while the public constructor above keeps the
    // existing direct-composition API stable.
    internal ArchitectureBaselineApplicationService(
        ArchitectureBaselineCandidateCollector candidateCollector,
        IArchitectureBaselineGenerator baselineGenerator,
        IArchitectureBaselineLoadingService baselineLoadingService)
    {
        _candidateCollector = candidateCollector;
        _baselineGenerator = baselineGenerator;
        _baselineLoadingService = baselineLoadingService;
    }

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

        BaselineCandidateCollection collection = _candidateCollector.CollectGenerateCandidates(request);
        IReadOnlyList<ArchitectureBaselineCandidate>? candidates = collection.Candidates;
        List<ArchitectureViolation> configViolations = collection.ConfigurationViolations;

        if (candidates == null)
        {
            return new BaselineGenerationOutcome(
                Succeeded: false, Yaml: null, CandidateCount: 0, ConfigurationViolations: configViolations)
            {
                PreflightDiagnostics = collection.PreflightDiagnostics,
            };
        }

        BaselineWritePlan plan = BaselineWritePlanner.PlanGenerate(candidates, reasonMap);
        ArchitectureBaselineDocument baseline = _baselineGenerator.BuildFromEntries(
            plan.OutputEntries,
            collection.HasSelectedRelativeMetricBudgets
                ? ArchitectureViolationIdentity.CurrentVersion + 1
                : ArchitectureViolationIdentity.CurrentVersion);
        baseline.MetricBaselines = collection.MetricBaselineCandidates.ToList();

        return new BaselineGenerationOutcome(
            Succeeded: true,
            Yaml: _baselineGenerator.Serialize(baseline),
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

        BaselineCandidateCollection collection = _candidateCollector.CollectUpdateCandidates(request);
        ArchitectureContractDocument document = collection.Document;
        IReadOnlyList<ArchitectureBaselineCandidate>? candidates = collection.Candidates;
        List<ArchitectureViolation> configViolations = collection.ConfigurationViolations;

        if (candidates == null)
        {
            return new BaselineUpdateOutcome(
                Succeeded: false, Yaml: null, PreservedCount: 0, NewCount: 0, ConfigurationViolations: configViolations)
            {
                PreflightDiagnostics = collection.PreflightDiagnostics,
            };
        }

        ArchitectureBaselineDocument existingBaseline = _baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        BaselineWritePlan plan = BaselineWritePlanner.PlanUpdate(comparison, reasonMap);
        ArchitectureBaselineDocument updated = _baselineGenerator.BuildFromEntries(plan.OutputEntries, existingBaseline.Version);
        updated.MetricBaselines = existingBaseline.MetricBaselines.ToList();

        BaselineCommentInspection comments = InspectComments(request.BaselinePath);

        return new BaselineUpdateOutcome(
            Succeeded: true,
            Yaml: comments.Header + _baselineGenerator.Serialize(updated),
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
        BaselineCandidateCollection collection = _candidateCollector.CollectPruneCandidates(request);
        ArchitectureContractDocument document = collection.Document;
        IReadOnlyList<ArchitectureBaselineCandidate>? candidates = collection.Candidates;
        List<ArchitectureViolation> configViolations = collection.ConfigurationViolations;

        if (candidates == null)
        {
            return new BaselinePruneOutcome(
                Succeeded: false, Yaml: null, RemovedEntries: Array.Empty<BaselineRemovedEntry>(), ConfigurationViolations: configViolations)
            {
                PreflightDiagnostics = collection.PreflightDiagnostics,
            };
        }

        ArchitectureBaselineDocument existingBaseline = _baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        BaselineWritePlan plan = BaselineWritePlanner.PlanPrune(comparison);

        List<BaselineRemovedEntry> removed = comparison.Resolved
            .Select(e => new BaselineRemovedEntry(e, BaselineEntryLifecycleNames.Resolved))
            .Concat(comparison.ConfigurationErrors.Select(
                e => new BaselineRemovedEntry(e, BaselineEntryLifecycleNames.Stale)))
            .ToList();

        string rawBaseline = _baselineLoadingService.ReadRawText(request.BaselinePath);
        BaselineCommentInspection comments = BaselineCommentInspector.Inspect(rawBaseline);

        // Nothing to remove means the input already is the answer. Reserializing it would be a
        // no-op prune that still rewrote quoting, line endings, or blank-line placement — the file
        // has to come back byte-for-byte identical.
        string yaml = plan.RemovesNothing
            ? rawBaseline
            : comments.Header + _baselineGenerator.Serialize(
                BuildPreservingMetricBaselines(plan.OutputEntries, existingBaseline));

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
        BaselineCandidateCollection collection = _candidateCollector.CollectDiffCandidates(request);

        if (collection.Candidates == null)
        {
            return new BaselineDiffOutcome(
                Succeeded: false,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: collection.ConfigurationViolations)
            {
                PreflightDiagnostics = collection.PreflightDiagnostics,
            };
        }

        ArchitectureBaselineDocument existingBaseline = _baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            collection.Document, existingBaseline, collection.Candidates, request.Mode, request.ContractIds);

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
        BaselineCandidateCollection collection = _candidateCollector.CollectVerifyCandidates(request);
        return VerifyCollectedCandidates(
            request,
            collection.Document,
            collection.Candidates,
            collection.ConfigurationViolations,
            collection.PreflightDiagnostics);
    }

    /// <summary>
    /// Verifies persistent debt from candidate evidence already produced by an analysis snapshot.
    /// This avoids a second repository analysis for composite consumers such as Health.
    /// </summary>
    public BaselineVerifyOutcome Verify(BaselineVerifyRequest request, ArchitectureAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArchitectureSnapshotBaselineCandidateReceipt receipt = snapshot.CollectBaselineCandidates(request.Mode);
        return VerifyCollectedCandidates(
            request,
            receipt.Document,
            receipt.Candidates,
            receipt.ConfigurationViolations,
            receipt.PreflightDiagnostics);
    }

    private BaselineVerifyOutcome VerifyCollectedCandidates(
        BaselineVerifyRequest request,
        ArchitectureContractDocument document,
        IReadOnlyList<ArchitectureBaselineCandidate>? candidates,
        IReadOnlyCollection<ArchitectureViolation> configurationViolations,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
    {
        if (candidates == null)
        {
            return new BaselineVerifyOutcome(
                Succeeded: false,
                InSync: false,
                New: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Frozen: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                Resolved: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationErrors: Array.Empty<ArchitectureBaselineComparisonEntry>(),
                ConfigurationViolations: configurationViolations)
            {
                PreflightDiagnostics = preflightDiagnostics,
            };
        }

        ArchitectureBaselineDocument existingBaseline = _baselineLoadingService.Load(request.BaselinePath);
        ArchitectureBaselineComparisonResult comparison = ArchitectureBaselineComparer.Compare(
            document, existingBaseline, candidates, request.Mode, request.ContractIds);

        // Ambiguity is out-of-sync too: one entry standing in for several distinct violations
        // suppresses more than it was reviewed for.
        bool inSync = comparison.New.Count == 0
            && comparison.Resolved.Count == 0
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

        ArchitectureBaselineDocument legacyBaseline = _baselineLoadingService.Load(request.BaselinePath);
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
        BaselineCandidateCollection collection = _candidateCollector.CollectCandidates(
            request.PolicyPath, "all", request.ConditionSetName, contractIds: null, cancellationToken: request.CancellationToken);
        IReadOnlyList<ArchitectureBaselineCandidate>? candidates = collection.Candidates;
        List<ArchitectureViolation> configViolations = collection.ConfigurationViolations;

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
                            groupName, entry.Id, ignore.SourceType, ignore.ForbiddenReference, "matched", 1)
                        {
                            Identity = candidate.Identity,
                        });
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
        ArchitectureBaselineDocument migrated = _baselineGenerator.BuildFromEntries(
            migratedEntries, version: ArchitectureViolationIdentity.CurrentVersion);
        string yaml = _baselineGenerator.Serialize(migrated);

        return new BaselineMigrateOutcome(writable, yaml, matched, stale, ambiguous, report, Array.Empty<ArchitectureViolation>());
    }

    private ArchitectureBaselineDocument BuildPreservingMetricBaselines(
        IReadOnlyList<ArchitectureBaselineComparisonEntry> entries,
        ArchitectureBaselineDocument existingBaseline)
    {
        ArchitectureBaselineDocument updated = _baselineGenerator.BuildFromEntries(entries, existingBaseline.Version);
        updated.MetricBaselines = existingBaseline.MetricBaselines.ToList();
        return updated;
    }

    private BaselineCommentInspection InspectComments(string baselinePath)
    {
        return BaselineCommentInspector.Inspect(_baselineLoadingService.ReadRawText(baselinePath));
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
}
