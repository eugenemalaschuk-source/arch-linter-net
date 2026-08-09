using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSharedFrameworkResolverTests
{
    private FakeArchitectureFileSystem _fileSystem = null!;
    private FakeArchitectureEnvironment _environment = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new FakeArchitectureFileSystem();
        _environment = new FakeArchitectureEnvironment();
    }

    [Test]
    public void ResolveProbingPaths_NoSharedFrameworksConfigured_ReturnsEmptyWithoutTouchingEnvironment()
    {
        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string>(), _fileSystem, _environment);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveProbingPaths_DotNetRootEnvVar_ResolvesHighestInstalledVersion()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.3");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.10");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.10" }));
    }

    [Test]
    public void ResolveProbingPaths_RuntimeDirectoryFallback_UsedWhenDotNetRootNotSet()
    {
        _environment.RuntimeDirectory = "/usr/local/share/dotnet/shared/Microsoft.NETCore.App/10.0.0";
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5" }));
    }

    [Test]
    public void ResolveProbingPaths_DotNetRootTakesPrecedenceWhenBothRootsHaveTheFramework()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");
        _environment.RuntimeDirectory = "/usr/local/share/dotnet/shared/Microsoft.NETCore.App/10.0.0";
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" }));
    }

    [Test]
    public void ResolveProbingPaths_FallsThroughToNextRootWhenFirstRootLacksTheFramework()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _environment.RuntimeDirectory = "/usr/local/share/dotnet/shared/Microsoft.NETCore.App/10.0.0";
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5" }));
    }

    [Test]
    public void ResolveProbingPaths_PrereleaseVersionSuffix_IsIgnoredForVersionComparison()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.0-preview.1");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.0-preview.1" }));
    }

    [Test]
    public void ResolveProbingPaths_NonVersionDirectoryName_IsSkippedWithoutThrowing()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/current");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" }));
    }

    [Test]
    public void ResolveProbingPaths_BlankAndDuplicateNames_AreTrimmedAndDeduplicated()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");

        IReadOnlyList<string> result = ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            new List<string> { " Microsoft.AspNetCore.App ", "Microsoft.AspNetCore.App", "" }, _fileSystem, _environment);

        Assert.That(result, Is.EqualTo(new[] { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" }));
    }

    [Test]
    public void ResolveProbingPaths_MissingFramework_ThrowsActionableExceptionNamingItAndSearchedRoots()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
                new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("Microsoft.AspNetCore.App"));
            Assert.That(exception.Message, Does.Contain("/opt/dotnet/shared"));
            Assert.That(exception.Message, Does.Contain("DOTNET_ROOT"));
        });
    }

    [Test]
    public void ResolveProbingPaths_NoSharedRootsResolvable_ThrowsNamingNoneSearched()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
                new List<string> { "Microsoft.AspNetCore.App" }, _fileSystem, _environment))!;

        Assert.That(exception.Message, Does.Contain("<none>"));
    }
}
