namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitecturePolicyCheckApplicationService
{
    PolicyCheckOutcome Check(string policyPath);
}
