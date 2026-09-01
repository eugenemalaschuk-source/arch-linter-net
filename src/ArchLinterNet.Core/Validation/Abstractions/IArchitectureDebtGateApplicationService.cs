namespace ArchLinterNet.Core.Validation.Abstractions;

/// <summary>Composes reviewed persistent debt and optional policy-weakening guardrail evidence.</summary>
public interface IArchitectureDebtGateApplicationService
{
    ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request);

    /// <summary>Evaluates debt from candidates already collected by the supplied analysis snapshot.</summary>
    ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request, ArchitectureAnalysisSnapshot snapshot);
}
