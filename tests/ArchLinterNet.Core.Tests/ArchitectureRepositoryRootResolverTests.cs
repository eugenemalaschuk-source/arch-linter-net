using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRepositoryRootResolverTests
{
    [Test]
    public void Resolve_TwoInstancesWithDifferentFakeRoots_ResolveIndependently()
    {
        string rootA = FakePaths.Root("/repo-a");
        string rootB = FakePaths.Root("/repo-b");

        var fileSystemA = new FakeArchitectureFileSystem();
        fileSystemA.AddFile($"{rootA}/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environmentA = new FakeArchitectureEnvironment { BaseDirectory = rootA };
        var resolverA = new ArchitectureRepositoryRootResolver(fileSystemA, environmentA);

        var fileSystemB = new FakeArchitectureFileSystem();
        fileSystemB.AddFile($"{rootB}/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environmentB = new FakeArchitectureEnvironment { BaseDirectory = rootB };
        var resolverB = new ArchitectureRepositoryRootResolver(fileSystemB, environmentB);

        Assert.That(resolverA.Resolve().Replace('\\', '/'), Is.EqualTo(rootA));
        Assert.That(resolverB.Resolve().Replace('\\', '/'), Is.EqualTo(rootB));
    }

    [Test]
    public void Resolve_CalledTwiceOnSameInstance_ReflectsCurrentFakeStateEachTime()
    {
        string rootA = FakePaths.Root("/repo-a");
        string rootB = FakePaths.Root("/repo-b");

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile($"{rootA}/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);
        var environment = new FakeArchitectureEnvironment { BaseDirectory = rootA };
        var resolver = new ArchitectureRepositoryRootResolver(fileSystem, environment);

        Assert.That(resolver.Resolve().Replace('\\', '/'), Is.EqualTo(rootA));

        environment.BaseDirectory = rootB;
        fileSystem.AddFile($"{rootB}/architecture/dependencies.arch.yml", "version: 1", DateTime.UtcNow);

        Assert.That(resolver.Resolve().Replace('\\', '/'), Is.EqualTo(rootB));
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
