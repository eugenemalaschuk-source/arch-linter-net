using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitecturePublicApiApplicationServiceTests
{
    [Test]
    public void CoreComposition_RegistersSurfaceResolverAndApplicationFacade()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddArchLinterNetCore()
            .BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ArchitecturePublicApiSurfaceResolver>(), Is.Not.Null);
            Assert.That(
                provider.GetRequiredService<IArchitecturePublicApiApplicationService>(),
                Is.TypeOf<ArchitecturePublicApiApplicationService>());
        });
    }
}
