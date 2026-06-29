using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public sealed class ArchitectureEngineBuilder
{
    private readonly IServiceCollection _services = new ServiceCollection();

    public ArchitectureEngineBuilder AddArchLinterNetCore()
    {
        _services.AddArchLinterNetCore();
        return this;
    }

    public ArchitectureEngine Build()
    {
        return new ArchitectureEngine(_services.BuildServiceProvider());
    }
}
