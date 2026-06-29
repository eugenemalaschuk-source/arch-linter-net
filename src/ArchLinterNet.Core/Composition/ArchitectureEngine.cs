using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public sealed class ArchitectureEngine
{
    private readonly IServiceProvider _serviceProvider;

    internal ArchitectureEngine(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ValidationOutcome Validate(ValidationRequest request, ValidationTiming? timing = null)
    {
        return _serviceProvider.GetRequiredService<IArchitectureValidationApplicationService>()
            .Validate(request, timing);
    }

    public BaselineGenerationOutcome GenerateBaseline(BaselineGenerationRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureBaselineApplicationService>()
            .Generate(request);
    }
}
