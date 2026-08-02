using System.Diagnostics;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Testing;

public sealed class ArchitectureValidationBuilder
{
    private static readonly Lazy<ArchitectureEngine> _engine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    private readonly string _policyPath;
    private string? _conditionSetName;
    private IReadOnlyCollection<string>? _contractIds;
    private string? _baselinePath;
    private bool _enforceUnmatchedIgnoredViolationsPolicy;
    private bool _collectTimings;
    private bool _collectProfile;
    private BuildPreparationMode _preparationMode = BuildPreparationMode.Ordinary;
    private bool _noRestore;
    private string? _requestedConfiguration;
    private string? _requestedTargetFramework;
    private CancellationToken _cancellationToken;

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
    }

    public ArchitectureValidationBuilder WithConditionSet(string name)
    {
        _conditionSetName = name;
        return this;
    }

    public ArchitectureValidationBuilder WithContracts(IEnumerable<string> contractIds)
    {
        _contractIds = contractIds.ToArray();
        return this;
    }

    public ArchitectureValidationBuilder WithContracts(params string[] contractIds)
    {
        return WithContracts((IEnumerable<string>)contractIds);
    }

    public ArchitectureValidationBuilder WithBaseline(string baselinePath)
    {
        _baselinePath = baselinePath;
        return this;
    }

    public ArchitectureValidationBuilder WithUnmatchedIgnoredViolationsPolicy(bool enforce = true)
    {
        _enforceUnmatchedIgnoredViolationsPolicy = enforce;
        return this;
    }

    public ArchitectureValidationBuilder WithTimings()
    {
        _collectTimings = true;
        return this;
    }

    // Opt-in mirror of the CLI's --profile option — see
    // openspec/specs/analysis-profile/spec.md, "Testing API exposes the same profile semantics as
    // the CLI". Implies timing collection internally (a profile needs a real ValidationTiming
    // instance for contract-family counts) without also enabling WithTimings()' own effects
    // (there are none beyond exposing ArchitectureValidationResult.Timing, which this leaves
    // populated as a side effect — same as calling both would already do).
    public ArchitectureValidationBuilder WithProfile()
    {
        _collectProfile = true;
        return this;
    }

    public ArchitectureValidationBuilder WithEnsureBuilt(string? configuration = null, string? targetFramework = null)
    {
        _preparationMode = BuildPreparationMode.EnsureBuilt;
        _requestedConfiguration = configuration;
        _requestedTargetFramework = targetFramework;
        return this;
    }

    public ArchitectureValidationBuilder WithNoRestore()
    {
        _noRestore = true;
        return this;
    }

    /// <summary>Bounds this builder's validation/snapshot calls with a caller-supplied cancellation token.</summary>
    public ArchitectureValidationBuilder WithCancellation(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    public ArchitectureValidationResult ValidateStrict()
    {
        return Validate(mode: "strict");
    }

    public ArchitectureValidationResult ValidateAudit()
    {
        return Validate(mode: "audit");
    }

    /// <summary>Compares the configured baseline without changing it.</summary>
    public BaselineDiffOutcome DiffBaseline(string mode = "all")
    {
        return _engine.Value.DiffBaseline(new BaselineDiffRequest
        {
            PolicyPath = _policyPath,
            BaselinePath = RequireBaselinePath(),
            Mode = mode,
            ConditionSetName = _conditionSetName,
            ContractIds = _contractIds?.ToList(),
        });
    }

    /// <summary>Verifies the configured baseline and exposes its typed comparison entries.</summary>
    public BaselineVerifyOutcome VerifyBaseline(string mode = "all")
    {
        return _engine.Value.VerifyBaseline(new BaselineVerifyRequest
        {
            PolicyPath = _policyPath,
            BaselinePath = RequireBaselinePath(),
            Mode = mode,
            ConditionSetName = _conditionSetName,
            ContractIds = _contractIds?.ToList(),
        });
    }

    /// <summary>Performs a non-writing migration analysis for the configured version-1 baseline.</summary>
    public BaselineMigrateOutcome MigrateBaseline()
    {
        return _engine.Value.MigrateBaseline(new BaselineMigrateRequest
        {
            PolicyPath = _policyPath,
            BaselinePath = RequireBaselinePath(),
            ConditionSetName = _conditionSetName,
            DryRun = true,
        });
    }

    // Explicit opt-in for callers who want strict/audit (and, via each mode's own coverage
    // families, coverage) served from one composed policy/project-graph/assembly-load instead of
    // one independent Validate call per mode — see openspec/specs/analysis-snapshot/spec.md,
    // "Testing API exposes an explicitly owned shared snapshot". ValidateStrict()/ValidateAudit()
    // above are unaffected and keep performing independent runs.
    public ArchitectureValidationSnapshotSession CreateSnapshot()
    {
        long allocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
        AnalysisSnapshotRequest request = new()
        {
            PolicyPath = _policyPath,
            ConditionSetName = _conditionSetName,
            ContractIds = _contractIds,
            BaselinePath = _baselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = _enforceUnmatchedIgnoredViolationsPolicy,
            PreparationMode = _preparationMode,
            NoRestore = _noRestore,
            RequestedConfiguration = _requestedConfiguration,
            RequestedTargetFramework = _requestedTargetFramework,
            CancellationToken = _cancellationToken,
        };

        ValidationTiming? timing = _collectTimings || _collectProfile ? new ValidationTiming() : null;
        ArchitectureAnalysisSnapshot snapshot = _engine.Value.CreateSnapshot(request, timing);
        return new ArchitectureValidationSnapshotSession(snapshot, timing, _collectProfile, allocatedBytesAtStart);
    }

    private ArchitectureValidationResult Validate(string mode)
    {
        long allocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
        ValidationRequest request = new()
        {
            PolicyPath = _policyPath,
            Mode = mode,
            ConditionSetName = _conditionSetName,
            ContractIds = _contractIds,
            BaselinePath = _baselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = _enforceUnmatchedIgnoredViolationsPolicy,
            PreparationMode = _preparationMode,
            NoRestore = _noRestore,
            RequestedConfiguration = _requestedConfiguration,
            RequestedTargetFramework = _requestedTargetFramework,
            CancellationToken = _cancellationToken,
        };

        ValidationTiming? timing = _collectTimings || _collectProfile ? new ValidationTiming() : null;
        (ValidationOutcome outcome, ArchitectureAnalysisSnapshotCounters counters) =
            _engine.Value.ValidateWithCounters(request, timing);

        AnalysisProfile? profile = _collectProfile
            ? AnalysisProfileBuilder.Build(
                counters, timing, renderedSinkCount: 0, outputSinkCount: 0,
                ResolveCompletionStatus(outcome), cancellationObserved: false,
                CaptureMeasurements(allocatedBytesAtStart))
            : null;

        return ArchitectureValidationResultMapper.ToResult(outcome, timing, mode, profile);
    }

    // outcome.PreflightBlocked wins over Passed the same way the CLI's own resolver does (see
    // ValidateCommandHandler.Profile.cs) — neither this builder's Validate nor
    // ArchitectureValidationSnapshotSession.Evaluate catches cancellation (an
    // OperationCanceledException just propagates), so Cancelled is never resolved here.
    internal static AnalysisProfileCompletionStatus ResolveCompletionStatus(ValidationOutcome outcome)
    {
        if (outcome.PreflightBlocked)
        {
            return AnalysisProfileCompletionStatus.PreparationFailure;
        }

        return outcome.Passed ? AnalysisProfileCompletionStatus.Success : AnalysisProfileCompletionStatus.ValidationFailure;
    }

    internal static AnalysisProfileMeasurements CaptureMeasurements(long allocatedBytesAtStart)
    {
        long peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
        return new AnalysisProfileMeasurements
        {
            PeakWorkingSetBytes = peakWorkingSetBytes > 0 ? peakWorkingSetBytes : null,
            AllocatedBytesTotal = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBytesAtStart),
        };
    }

    private string RequireBaselinePath()
    {
        return _baselinePath ?? throw new InvalidOperationException(
            "A baseline path is required. Call WithBaseline(path) before requesting a baseline comparison.");
    }
}
