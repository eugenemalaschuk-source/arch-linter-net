using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureBaselineApplicationService
{
    BaselineGenerationOutcome Generate(BaselineGenerationRequest request);
}
