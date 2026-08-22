using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Read-only orchestration over the authoritative baseline and policy-weakening services.
/// It deliberately owns neither identity/lifecycle comparison nor weakening classification.
/// </summary>
public sealed class ArchitectureDebtGateApplicationService(
    IArchitectureBaselineApplicationService baselineService)
    : IArchitectureDebtGateApplicationService
{
    public ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request)
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

        BaselineVerifyOutcome persistentDebt = baselineService.Verify(new BaselineVerifyRequest
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
        });

        ArchitecturePolicyWeakeningResult? weakening = hasBaseContext
            ? ArchitecturePolicyWeakeningComparer.Compare(new ArchitecturePolicyWeakeningRequest(
                request.BasePolicyContext!, request.CurrentPolicyContext!))
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
                persistentDebt.PreflightDiagnostics.ToArray()),
            persistentDebt)
        {
            PolicyWeakening = weakening,
            PolicyWeakeningRequested = hasBaseContext,
        };
    }
}
