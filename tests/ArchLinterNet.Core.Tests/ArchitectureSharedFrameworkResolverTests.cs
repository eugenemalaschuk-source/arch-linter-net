using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSharedFrameworkResolverTests
{
    private static readonly string[] _value = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.10" };
    private static readonly string[] _value1 = { "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5" };
    private static readonly string[] _value2 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" };
    private static readonly string[] _value3 = { "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5" };
    private static readonly string[] _value4 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11" };
    private static readonly string[] _value5 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/9.1.0-preview.1" };
    private static readonly string[] _value6 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.5" };
    private static readonly string[] _value7 = { "net8.0" };
    private static readonly string[] _value8 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11" };
    private static readonly string[] _value9 = { "net8.0" };
    private static readonly string[] _value10 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.2" };
    private static readonly string[] _value11 = { "net8.0", "net10.0" };
    private static readonly string[] _value12 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.9" };
    private static readonly string[] _value13 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" };
    private static readonly string[] _value14 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" };
    private static readonly string[] _value15 = { "/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1" };
    private static readonly List<string> _aspNetCoreApp = new() { "Microsoft.AspNetCore.App" };

    private FakeArchitectureFileSystem _fileSystem = null!;
    private FakeArchitectureEnvironment _environment = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new FakeArchitectureFileSystem();
        _environment = new FakeArchitectureEnvironment();
    }

    private IReadOnlyList<string> Resolve(
        IReadOnlyList<string> sharedFrameworkNames,
        string? targetFrameworkMoniker = null,
        IReadOnlyCollection<string>? discoveredTargetFrameworkMonikers = null) =>
        ArchitectureSharedFrameworkResolver.ResolveProbingPaths(
            sharedFrameworkNames, targetFrameworkMoniker,
            discoveredTargetFrameworkMonikers ?? Array.Empty<string>(), _fileSystem, _environment);

    [Test]
    public void ResolveProbingPaths_NoSharedFrameworksConfigured_ReturnsEmptyWithoutTouchingEnvironment()
    {
        IReadOnlyList<string> result = Resolve(new List<string>());

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

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value));
    }

    [Test]
    public void ResolveProbingPaths_RuntimeDirectoryFallback_UsedWhenDotNetRootNotSet()
    {
        _environment.RuntimeDirectory = "/usr/local/share/dotnet/shared/Microsoft.NETCore.App/10.0.0";
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.5");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value1));
    }

    [Test]
    public void ResolveProbingPaths_DotNetRootTakesPrecedenceWhenBothRootsHaveTheFramework()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");
        _environment.RuntimeDirectory = "/usr/local/share/dotnet/shared/Microsoft.NETCore.App/8.0.0";
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/8.0.9");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value2));
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

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value3));
    }

    [Test]
    public void ResolveProbingPaths_StableVersionPreferredOverHigherNumberedPrerelease()
    {
        // A stable 8.x install must win over a numerically higher 9.0.0 prerelease — the .NET host
        // never rolls a release build forward into a prerelease build.
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.0-preview.1");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value4));
    }

    [Test]
    public void ResolveProbingPaths_OnlyPrereleaseInstalled_FallsBackToHighestPrerelease()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.0-preview.1");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.1.0-preview.1");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value5));
    }

    [Test]
    public void ResolveProbingPaths_TargetFrameworkAnchor_ExcludesAHigherPrereleaseMajorEvenThoughItIsInstalled()
    {
        // The reviewer's coexistence scenario: a net10 consumer on a machine that also has
        // Microsoft.AspNetCore.App 11.0.0-preview.* installed must stay on the 10.x stable build.
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.5");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/11.0.0-preview.1");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp, "net10.0");

        Assert.That(result, Is.EqualTo(_value6));
    }

    [Test]
    public void ResolveProbingPaths_DiscoveredTargetFrameworkAnchor_UsedWhenNoExplicitTargetFramework()
    {
        // The consumer's actually-resolved project output targets net8 while the ArchLinterNet CLI
        // itself always runs as net10 — the CLI's own runtime major must not leak in as the anchor
        // when discovery already knows the real target framework.
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.2");
        _environment.RuntimeDirectory = "/opt/dotnet/shared/Microsoft.NETCore.App/10.0.0";

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp, discoveredTargetFrameworkMonikers: _value7);

        Assert.That(result, Is.EqualTo(_value8));
    }

    [Test]
    public void ResolveProbingPaths_ExplicitTargetFrameworkTakesPrecedenceOverDiscovered()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.11");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.2");

        IReadOnlyList<string> result = Resolve(
            _aspNetCoreApp, "net10.0", discoveredTargetFrameworkMonikers: _value9);

        Assert.That(result, Is.EqualTo(_value10));
    }

    [Test]
    public void ResolveProbingPaths_AmbiguousDiscoveredMajors_ThrowsFailClosedInsteadOfPickingOne()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            _aspNetCoreApp, discoveredTargetFrameworkMonikers: _value11))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("8, 10"));
            Assert.That(exception.Message, Does.Contain("analysis.target_framework"));
        });
    }

    [Test]
    public void ResolveProbingPaths_RuntimeDirectoryAnchor_UsedAsLastResortWhenNothingElseIsKnown()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.9");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/10.0.2");
        _environment.RuntimeDirectory = "/opt/dotnet/shared/Microsoft.NETCore.App/9.0.0";

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value12));
    }

    [Test]
    public void ResolveProbingPaths_AnchorMajorHasNoCandidate_TreatsFrameworkAsMissingRatherThanCrossingMajor()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/9.0.9");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Resolve(_aspNetCoreApp, "net10.0"))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("Microsoft.AspNetCore.App"));
            Assert.That(exception.Message, Does.Contain("major version 10"));
        });
    }

    [Test]
    public void ResolveProbingPaths_TargetFrameworkWithoutADot_IsNotUsedAsAnAnchor()
    {
        // "net48" (.NET Framework) never carries a dot after "net<digits>" the way "netN.0" TFMs
        // do; it must not be misparsed into major version 48.
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp, "net48");

        Assert.That(result, Is.EqualTo(_value13));
    }

    [Test]
    public void ResolveProbingPaths_NonVersionDirectoryName_IsSkippedWithoutThrowing()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/current");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");

        IReadOnlyList<string> result = Resolve(_aspNetCoreApp);

        Assert.That(result, Is.EqualTo(_value14));
    }

    [Test]
    public void ResolveProbingPaths_BlankAndDuplicateNames_AreTrimmedAndDeduplicated()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App");
        _fileSystem.AddDirectory("/opt/dotnet/shared/Microsoft.AspNetCore.App/8.0.1");

        IReadOnlyList<string> result = Resolve(new List<string> { " Microsoft.AspNetCore.App ", "Microsoft.AspNetCore.App", "" });

        Assert.That(result, Is.EqualTo(_value15));
    }

    [Test]
    public void ResolveProbingPaths_MissingFramework_ThrowsActionableExceptionNamingItAndSearchedRoots()
    {
        _environment.SetEnvironmentVariable("DOTNET_ROOT", "/opt/dotnet");
        _fileSystem.AddDirectory("/opt/dotnet/shared");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Resolve(_aspNetCoreApp))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("Microsoft.AspNetCore.App"));
            // The joined root uses the host's own directory separator (backslash on Windows), so
            // normalize before asserting on its content.
            Assert.That(exception.Message.Replace('\\', '/'), Does.Contain("/opt/dotnet/shared"));
            Assert.That(exception.Message, Does.Contain("DOTNET_ROOT"));
        });
    }

    [Test]
    public void ResolveProbingPaths_NoSharedRootsResolvable_ThrowsNamingNoneSearched()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Resolve(_aspNetCoreApp))!;

        Assert.That(exception.Message, Does.Contain("<none>"));
    }
}
