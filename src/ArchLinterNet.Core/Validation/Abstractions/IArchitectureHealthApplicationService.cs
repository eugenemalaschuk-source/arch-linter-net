namespace ArchLinterNet.Core.Validation.Abstractions;

/// <summary>Composes existing validation and debt-gate receipts into architecture-health/v1.</summary>
public interface IArchitectureHealthApplicationService
{
    ArchitectureHealthOutcome Evaluate(ArchitectureHealthRequest request);
}
