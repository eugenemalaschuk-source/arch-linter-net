using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public static class ArchitectureValidationService
{
    private static readonly Lazy<ArchitectureEngine> _defaultEngine =
        new(() => new ArchitectureEngineBuilder().AddArchLinterNetCore().Build());

    public static ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        return _defaultEngine.Value.Validate(request, timing);
    }
}
