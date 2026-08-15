namespace ArchLinterNet.Core.BuildState;

public interface IBuildStatePreparationService
{
    BuildStatePreflightResult Prepare(BuildStatePreflightRequest request);
}
