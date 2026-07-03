using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRepositoryRootResolverTests
{
    [Test]
    public void Resolve_TwoInstancesWithDifferentFakeRoots_ResolveIndependently()
    {
        var fileSystemA = new FakeArchitectureFileSystem();
        fileSystemA.AddFile("/repo-a/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environmentA = new FakeArchitectureEnvironment { BaseDirectory = "/repo-a" };
        var resolverA = new ArchitectureRepositoryRootResolver(fileSystemA, environmentA);

        var fileSystemB = new FakeArchitectureFileSystem();
        fileSystemB.AddFile("/repo-b/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environmentB = new FakeArchitectureEnvironment { BaseDirectory = "/repo-b" };
        var resolverB = new ArchitectureRepositoryRootResolver(fileSystemB, environmentB);

        Assert.That(resolverA.Resolve(), Is.EqualTo("/repo-a"));
        Assert.That(resolverB.Resolve(), Is.EqualTo("/repo-b"));
    }

    [Test]
    public void Resolve_CalledTwiceOnSameInstance_ReflectsCurrentFakeStateEachTime()
    {
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile("/repo-a/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environment = new FakeArchitectureEnvironment { BaseDirectory = "/repo-a" };
        var resolver = new ArchitectureRepositoryRootResolver(fileSystem, environment);

        Assert.That(resolver.Resolve(), Is.EqualTo("/repo-a"));

        environment.BaseDirectory = "/repo-b";
        fileSystem.AddFile("/repo-b/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);

        Assert.That(resolver.Resolve(), Is.EqualTo("/repo-b"));
    }

    [Test]
    public void Resolve_NoContractFileFound_ThrowsDirectoryNotFoundException()
    {
        var fileSystem = new FakeArchitectureFileSystem();
        var environment = new FakeArchitectureEnvironment { BaseDirectory = "/repo-without-contract" };
        var resolver = new ArchitectureRepositoryRootResolver(fileSystem, environment);

        Assert.Throws<DirectoryNotFoundException>(() => resolver.Resolve());
    }
}
