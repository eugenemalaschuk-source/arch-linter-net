using ArchLinterNet.Core.Composition;

namespace ArchLinterNet.Core.Validation;

public static class ArchitectureBaselineService
{
    private static readonly Lazy<ArchitectureEngine> _defaultEngine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static BaselineGenerationOutcome Generate(BaselineGenerationRequest request)
    {
        return _defaultEngine.Value.GenerateBaseline(request);
    }
}
