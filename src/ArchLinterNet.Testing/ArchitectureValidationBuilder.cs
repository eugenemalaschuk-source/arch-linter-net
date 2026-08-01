using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Composition;
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

        ValidationTiming? timing = _collectTimings ? new ValidationTiming() : null;
        ArchitectureAnalysisSnapshot snapshot = _engine.Value.CreateSnapshot(request, timing);
        return new ArchitectureValidationSnapshotSession(snapshot, timing);
    }

    private ArchitectureValidationResult Validate(string mode)
    {
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

        ValidationTiming? timing = _collectTimings ? new ValidationTiming() : null;
        ValidationOutcome outcome = _engine.Value.Validate(request, timing);

        return ArchitectureValidationResultMapper.ToResult(outcome, timing, mode);
    }

    private string RequireBaselinePath()
    {
        return _baselinePath ?? throw new InvalidOperationException(
            "A baseline path is required. Call WithBaseline(path) before requesting a baseline comparison.");
    }
}
