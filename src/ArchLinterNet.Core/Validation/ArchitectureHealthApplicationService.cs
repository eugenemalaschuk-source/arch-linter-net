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
        ArgumentNullException.ThrowIfNull(request);
        ArchitectureDebtGateRequest debtGateRequest = request.DebtGate
            ?? throw new ArgumentException("A canonical debt-gate request is required.", nameof(request));
        string[] modes = ResolveModes(debtGateRequest.Mode);
        AnalysisSnapshotRequest snapshotRequest = new()
        {
            PolicyPath = debtGateRequest.PolicyPath,
            BaselinePath = debtGateRequest.BaselinePath,
            ConditionSetName = debtGateRequest.ConditionSetName,
            ContractIds = debtGateRequest.ContractIds,
            PreparationMode = debtGateRequest.PreparationMode,
            NoRestore = debtGateRequest.NoRestore,
            RequestedConfiguration = debtGateRequest.RequestedConfiguration,
            RequestedTargetFramework = debtGateRequest.RequestedTargetFramework,
            RequestedPlatform = debtGateRequest.RequestedPlatform,
            RequestedRuntimeIdentifier = debtGateRequest.RequestedRuntimeIdentifier,
            CancellationToken = debtGateRequest.CancellationToken,
        };

        using ArchitectureAnalysisSnapshot snapshot = validationService.CreateSnapshot(snapshotRequest);
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
        };
    }

    private static string[] ResolveModes(string mode) => mode switch
    {
        "strict" => ["strict"],
        "audit" => ["audit"],
        "all" => ["strict", "audit"],
        _ => throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode)),
    };

    private static ArchitectureHealthValidationOutcome[] AttachExternalEvidence(
        IReadOnlyList<ArchitectureHealthValidationOutcome> outcomes,
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
