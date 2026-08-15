using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests
{

    [TestFixture]
    public sealed class CliCommandModuleCatalogTests
    {
        [Test]
        public void IsGovernedModuleCandidate_AcceptsOnlyFeatureEntryPoints()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CliCommandModuleCatalog.IsGovernedModuleCandidate(
                    typeof(ArchLinterNet.Cli.Commands.CatalogFixture.EntryPoint.GovernedCandidate)), Is.True);
                Assert.That(CliCommandModuleCatalog.IsGovernedModuleCandidate(
                    typeof(ArchLinterNet.Cli.Commands.CatalogFixture.Application.ApplicationCandidate)), Is.False);
                Assert.That(CliCommandModuleCatalog.IsGovernedModuleCandidate(
                    typeof(ArchLinterNet.Cli.Commands.Common.EntryPoint.GenericBucketCandidate)), Is.False);
                Assert.That(CliCommandModuleCatalog.IsGovernedModuleCandidate(
                    typeof(ArchLinterNet.Cli.Commands.ContainerRootCandidate)), Is.False);
            });
        }
    }
}

namespace ArchLinterNet.Cli.Commands.CatalogFixture.EntryPoint
{
    internal sealed class GovernedCandidate;
}

namespace ArchLinterNet.Cli.Commands.CatalogFixture.Application
{
    internal sealed class ApplicationCandidate;
}

namespace ArchLinterNet.Cli.Commands.Common.EntryPoint
{
    internal sealed class GenericBucketCandidate;
}

namespace ArchLinterNet.Cli.Commands
{
    internal sealed class ContainerRootCandidate;
}
