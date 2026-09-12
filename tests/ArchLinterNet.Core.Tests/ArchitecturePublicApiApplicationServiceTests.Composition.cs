using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static ArchLinterNet.Core.Tests.ArchitecturePublicApiApplicationServiceTests;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePublicApiApplicationServiceCompositionTests
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
