namespace ArchLinterNet.Core.Validation.Abstractions;

/// <summary>Composes reviewed persistent debt and optional policy-weakening guardrail evidence.</summary>
public interface IArchitectureDebtGateApplicationService
{
    ArchitectureDebtGateOutcome Evaluate(ArchitectureDebtGateRequest request);
}
