using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Read-only orchestration over the authoritative baseline and policy-weakening services.
/// It deliberately owns neither identity/lifecycle comparison nor weakening classification.
/// </summary>
public sealed class ArchitectureDebtGateApplicationService : IArchitectureDebtGateApplicationService
{
    private readonly IArchitectureBaselineApplicationService _baselineService;
    private readonly IArchitecturePublicApiApplicationService? _publicApiService;
    private readonly IArchitectureValidationApplicationService? _validationService;

    public ArchitectureDebtGateApplicationService(IArchitectureBaselineApplicationService baselineService)
        : this(baselineService, null, null)
    {
    }

    public ArchitectureDebtGateApplicationService(
        IArchitectureBaselineApplicationService baselineService,
        IArchitecturePublicApiApplicationService? publicApiService)
        : this(baselineService, publicApiService, null)
    {
    }

    internal ArchitectureDebtGateApplicationService(
        IArchitectureBaselineApplicationService baselineService,
        IArchitecturePublicApiApplicationService? publicApiService,
        IArchitectureValidationApplicationService? validationService)
    {
        _baselineService = baselineService;
        _publicApiService = publicApiService;
        _validationService = validationService;
    }

    internal (ArchitectureDebtGateOutcome Outcome, ArchitectureAnalysisSnapshotCounters Counters) EvaluateWithCounters(
        ArchitectureDebtGateRequest request)
    {
        if (_validationService is null)
        {
            return (Evaluate(request), new ArchitectureAnalysisSnapshotCounters());
        }

        using ArchitectureAnalysisSnapshot snapshot = _validationService.CreateSnapshot(new AnalysisSnapshotRequest
        {
            PolicyPath = request.PolicyPath,
            BaselinePath = request.BaselinePath,
            ConditionSetName = request.ConditionSetName,
            ContractIds = request.ContractIds,
            PreparationMode = request.PreparationMode,
            NoRestore = request.NoRestore,
            RequestedConfiguration = request.RequestedConfiguration,
            RequestedTargetFramework = request.RequestedTargetFramework,
            RequestedPlatform = request.RequestedPlatform,
            RequestedRuntimeIdentifier = request.RequestedRuntimeIdentifier,
            CancellationToken = request.CancellationToken,
        });
        foreach (string mode in ResolveModes(request.Mode))
        {
            snapshot.Evaluate(mode);
        }

        ArchitectureDebtGateOutcome outcome = Evaluate(request, snapshot);
        return (outcome, snapshot.Counters);
    }

    private static string[] ResolveModes(string mode) => mode switch
    {
        "strict" => ["strict"],
        "audit" => ["audit"],
        "all" => ["strict", "audit"],
        _ => throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode)),
    };

    public ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request)
    {
        return EvaluateCore(request, snapshot: null);
    }

    /// <summary>Evaluates debt from the exact candidate receipt retained by an analysis snapshot.</summary>
    public ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request, ArchitectureAnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return EvaluateCore(request, snapshot) with
        {
            AnalysisInputs = ArchitectureAnalysisInputPaths.Create(
                snapshot.GetCapturePolicyImportPaths(),
                snapshot.GetCaptureResolvedAssemblyPaths(),
                snapshot.GetCaptureDiscoveredProjectPaths(),
                snapshot.GetCaptureConsumedInputPaths()),
        };
    }

    private ArchitectureDebtGateOutcome EvaluateCore(
        ArchitectureDebtGateRequest request,
        ArchitectureAnalysisSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool hasBaseContext = request.BasePolicyContext is not null;
        bool hasCurrentContext = request.CurrentPolicyContext is not null;
        if (hasBaseContext != hasCurrentContext)
        {
            throw new ArgumentException(
                "Both base and current policy contexts are required when policy-weakening checks are enabled.",
                nameof(request));
        }

        if (request.PublicApiWeakeningApprovals is { Count: > 0 } && !hasBaseContext)
        {
            throw new ArgumentException(
                "Public API weakening approvals require both base and current policy contexts.",
                nameof(request));
        }

        var baselineRequest = new BaselineVerifyRequest
        {
            PolicyPath = request.PolicyPath,
            BaselinePath = request.BaselinePath,
            Mode = request.Mode,
            ConditionSetName = request.ConditionSetName,
            ContractIds = request.ContractIds,
            PreparationMode = request.PreparationMode,
            NoRestore = request.NoRestore,
            RequestedConfiguration = request.RequestedConfiguration,
            RequestedTargetFramework = request.RequestedTargetFramework,
            RequestedPlatform = request.RequestedPlatform,
            RequestedRuntimeIdentifier = request.RequestedRuntimeIdentifier,
            CancellationToken = request.CancellationToken,
        };
        BaselineVerifyOutcome persistentDebt = snapshot is null
            ? _baselineService.Verify(baselineRequest)
            : _baselineService.Verify(baselineRequest, snapshot);

        IReadOnlyList<ArchitecturePublicApiLiveEvidence> publicApiEvidence = hasBaseContext
            ? CapturePublicApiEvidence(request, _publicApiService)
            : [];
        ArchitecturePolicyWeakeningResult? weakening = hasBaseContext
            ? ArchitecturePolicyWeakeningComparer.Compare(new ArchitecturePolicyWeakeningRequest(
                request.BasePolicyContext!, request.CurrentPolicyContext!)
            {
                PublicApiApprovals = request.PublicApiWeakeningApprovals ?? [],
                PublicApiLiveEvidence = publicApiEvidence,
            })
            : null;
        bool passed = persistentDebt.Succeeded
            && persistentDebt.InSync
            && (weakening is null || !weakening.HasErrors);

        return new ArchitectureDebtGateOutcome(
            persistentDebt.Succeeded,
            passed,
            new ArchitectureDebtGateEvaluation(
                persistentDebt.Succeeded,
                request.Mode,
                persistentDebt.PreflightDiagnostics.ToArray())
            {
                ReusedAnalysisSnapshot = snapshot is not null,
            },
            persistentDebt)
        {
            PolicyWeakening = weakening,
            PolicyWeakeningRequested = hasBaseContext,
        };
    }

    private static List<ArchitecturePublicApiLiveEvidence> CapturePublicApiEvidence(
        ArchitectureDebtGateRequest request,
        IArchitecturePublicApiApplicationService? publicApiService)
    {
        if (publicApiService is null || request.PublicApiWeakeningApprovals is not { Count: > 0 })
        {
            return [];
        }

        string currentDigest = ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(request.CurrentPolicyContext!);
        return request.PublicApiWeakeningApprovals
            .Select(approval =>
            {
                PublicApiCaptureOutcome capture = publicApiService.Capture(new PublicApiCaptureRequest
                {
                    PolicyPath = request.PolicyPath,
                    ContractId = approval.ContractId,
                    OutputPath = "architecture/public-api-approval-evidence.txt",
                    ConditionSetName = request.ConditionSetName,
                    PreparationMode = request.PreparationMode,
                    NoRestore = request.NoRestore,
                    CancellationToken = request.CancellationToken,
                });
                if (!capture.Succeeded || capture.Snapshot is null)
                {
                    return null;
                }

                PublicApiSnapshotDocument document = PublicApiSnapshotFormat.Parse(capture.Snapshot, "captured live public API");
                return new ArchitecturePublicApiLiveEvidence(
                    ArchitecturePublicApiLiveEvidence.CurrentSchemaVersion,
                    ArchitecturePublicApiLiveEvidence.EvidenceKind,
                    currentDigest,
                    approval.ContractId,
                    document.Entries);
            })
            .Where(evidence => evidence is not null)
            .Cast<ArchitecturePublicApiLiveEvidence>()
            .ToList();
    }
}
