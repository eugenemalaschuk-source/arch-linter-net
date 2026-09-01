using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Validation.Abstractions;

public interface IArchitectureBaselineApplicationService
{
    BaselineGenerationOutcome Generate(BaselineGenerationRequest request);

    BaselineUpdateOutcome Update(BaselineUpdateRequest request);

    BaselinePruneOutcome Prune(BaselinePruneRequest request);

    BaselineDiffOutcome Diff(BaselineDiffRequest request);

    BaselineVerifyOutcome Verify(BaselineVerifyRequest request);

    /// <summary>
    /// Verifies against candidates already collected by <paramref name="snapshot"/>. This is the
    /// snapshot-sharing path for a single composed consumer such as Architecture Health.
    /// </summary>
    BaselineVerifyOutcome Verify(BaselineVerifyRequest request, ArchitectureAnalysisSnapshot snapshot);

    BaselineMigrateOutcome Migrate(BaselineMigrateRequest request);
}
