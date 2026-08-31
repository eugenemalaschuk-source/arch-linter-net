using ArchLinterNet.Core.BuildState;
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
        ArchitectureDebtGateOutcome debtGate = debtGateService.Evaluate(debtGateRequest);
        return new ArchitectureHealthOutcome(
            ArchitectureHealthProjector.Project(validationOutcomes, debtGate),
            validationOutcomes,
            debtGate);
    }

    private static string[] ResolveModes(string mode) => mode switch
    {
        "strict" => ["strict"],
        "audit" => ["audit"],
        "all" => ["strict", "audit"],
        _ => throw new ArgumentException("Invalid mode. Use 'strict', 'audit', or 'all'.", nameof(mode)),
    };
}
