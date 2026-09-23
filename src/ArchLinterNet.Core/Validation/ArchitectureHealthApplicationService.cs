using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Read-only orchestration for Architecture Health. All domain semantics remain owned by the
/// validation snapshot and debt-gate services; this service only obtains their receipts once and
/// passes them to <see cref="ArchitectureHealthProjector"/>.
/// </summary>
public sealed class ArchitectureHealthApplicationService(
    IArchitectureValidationApplicationService validationService,
    IArchitectureDebtGateApplicationService debtGateService)
    : IArchitectureHealthApplicationService
{
    public ArchitectureHealthOutcome Evaluate(ArchitectureHealthRequest request)
    {
        ArchitectureDebtGateRequest debtGateRequest = RequireDebtGateRequest(request);
        using ArchitectureAnalysisSnapshot snapshot = validationService.CreateSnapshot(
            CreateSnapshotRequest(debtGateRequest));
        return Evaluate(request, snapshot);
    }

    /// <summary>
    /// Projects Health from a caller-owned immutable snapshot. Composite CLI workflows use this
    /// seam to publish Health and another canonical read-only projection without creating a second
    /// preparation or analysis lifetime. The caller retains ownership and must dispose the
    /// snapshot after every requested projection has completed.
    /// </summary>
    internal ArchitectureHealthOutcome Evaluate(
        ArchitectureHealthRequest request,
        ArchitectureAnalysisSnapshot snapshot)
    {
        ArchitectureDebtGateRequest debtGateRequest = RequireDebtGateRequest(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        string[] modes = ResolveModes(debtGateRequest.Mode);
        ArchitectureHealthValidationOutcome[] validationOutcomes = modes
            .Select(mode => new ArchitectureHealthValidationOutcome(mode, snapshot.Evaluate(mode)))
            .ToArray();
        validationOutcomes = AttachExternalEvidence(
            validationOutcomes,
            request.ExternalEvidenceArtifacts,
            request.ExternalEvidenceAssessmentContext,
            debtGateRequest.CancellationToken);
        ArchitectureDebtGateOutcome debtGate = debtGateService.Evaluate(debtGateRequest, snapshot);
        return new ArchitectureHealthOutcome(
            ArchitectureHealthProjector.Project(validationOutcomes, debtGate),
            validationOutcomes,
            debtGate)
        {
            AnalysisCounters = snapshot.Counters,
            ExecutionContext = request.ExecutionContext,
            ConditionSetName = debtGateRequest.ConditionSetName ?? string.Empty,
            AnalysisInputs = ArchitectureAnalysisInputPaths.Create(
                snapshot.GetCapturePolicyImportPaths(),
                snapshot.GetCaptureResolvedAssemblyPaths(),
                snapshot.GetCaptureDiscoveredProjectPaths(),
                snapshot.GetCaptureConsumedInputPaths()),
        };
    }

    private static ArchitectureDebtGateRequest RequireDebtGateRequest(ArchitectureHealthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.DebtGate
            ?? throw new ArgumentException("A canonical debt-gate request is required.", nameof(request));
    }

    private static AnalysisSnapshotRequest CreateSnapshotRequest(ArchitectureDebtGateRequest request) => new()
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
        IncludeRepositoryMetrics = true,
        CancellationToken = request.CancellationToken,
    };

    private static string[] ResolveModes(string mode) => mode switch
    {
        "strict" => ["strict"],
        "audit" => ["audit"],
        "all" => ["strict", "audit"],
        _ => throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode)),
    };

    private static ArchitectureHealthValidationOutcome[] AttachExternalEvidence(
        ArchitectureHealthValidationOutcome[] outcomes,
        IReadOnlyList<SarifEvidenceArtifactReference> artifacts,
        SarifEvidenceAssessmentContext? assessmentContext,
        CancellationToken cancellationToken)
    {
        ArchitectureHealthValidationOutcome first = outcomes[0];
        ArchitectureExternalEvidenceBinder.ValidateBindingIds(
            first.Outcome.ExternalEvidenceRequirements,
            artifacts);
        if (first.Outcome.PreflightBlocked)
        {
            return outcomes.ToArray();
        }

        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            first.Outcome.ExternalEvidenceRequirements,
            first.Outcome.RepositoryRoot,
            artifacts,
            assessmentContext,
            cancellationToken);
        return outcomes
            .Select(outcome => new ArchitectureHealthValidationOutcome(
                outcome.Mode,
                ArchitectureExternalEvidenceBinder.Attach(outcome.Outcome, binding, outcome.Mode)))
            .ToArray();
    }
}
