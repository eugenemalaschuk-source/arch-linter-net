namespace ArchLinterNet.Core.Validation;

public interface IArchitectureBaselineApplicationService
{
    BaselineGenerationOutcome Generate(BaselineGenerationRequest request);
}
