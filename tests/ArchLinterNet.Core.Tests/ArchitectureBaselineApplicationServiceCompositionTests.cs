using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed class ArchitectureBaselineApplicationServiceCompositionTests
{
    [Test]
    public void CoreComposition_RegistersCandidateCollectorAndApplicationFacade()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddArchLinterNetCore()
            .BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ArchitectureBaselineCandidateCollector>(), Is.Not.Null);
            Assert.That(
                provider.GetRequiredService<IArchitectureBaselineApplicationService>(),
                Is.TypeOf<ArchitectureBaselineApplicationService>());
        });
    }
}
