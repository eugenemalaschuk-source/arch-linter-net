using ArchLinterNet.Core.Asmdef;
using ArchLinterNet.Core.Asmdef.Abstractions;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public sealed class ArchitectureEngine : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;

    internal ArchitectureEngine(ServiceProvider serviceProvider)
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

    public BaselineUpdateOutcome UpdateBaseline(BaselineUpdateRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureBaselineApplicationService>()
            .Update(request);
    }

    public BaselinePruneOutcome PruneBaseline(BaselinePruneRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureBaselineApplicationService>()
            .Prune(request);
    }

    public BaselineDiffOutcome DiffBaseline(BaselineDiffRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureBaselineApplicationService>()
            .Diff(request);
    }

    public BaselineVerifyOutcome VerifyBaseline(BaselineVerifyRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureBaselineApplicationService>()
            .Verify(request);
    }

    public AsmdefValidationOutcome ValidateAsmdef(AsmdefValidationRequest request)
    {
        return _serviceProvider.GetRequiredService<IAsmdefValidationService>()
            .Validate(request);
    }

    public ArchitectureGraphOutcome BuildGraph(ArchitectureGraphRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureGraphApplicationService>()
            .BuildGraph(request);
    }

    public ArchitectureExplainOutcome Explain(ArchitectureExplainRequest request)
    {
        return _serviceProvider.GetRequiredService<IArchitectureExplainApplicationService>()
            .Explain(request);
    }

    public IArchitectureGraphFormatter GraphFormatter =>
        _serviceProvider.GetRequiredService<IArchitectureGraphFormatter>();

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _serviceProvider.DisposeAsync();
    }
}
