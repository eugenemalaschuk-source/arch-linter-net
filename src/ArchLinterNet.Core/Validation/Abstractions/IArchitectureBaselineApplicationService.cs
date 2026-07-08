using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureBaselineApplicationService
{
    BaselineGenerationOutcome Generate(BaselineGenerationRequest request);

    BaselineUpdateOutcome Update(BaselineUpdateRequest request);

    BaselinePruneOutcome Prune(BaselinePruneRequest request);

    BaselineDiffOutcome Diff(BaselineDiffRequest request);

    BaselineVerifyOutcome Verify(BaselineVerifyRequest request);
}
