using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTargetFrameworkSelectorTests
{
    [Test]
    public void Select_UsesOnlyNamedResolvedPathsAndDeduplicatesFrameworksCaseInsensitively()
    {
        var resolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["First"] = "/repo/bin/Debug/net10.0/First.dll",
            ["Second"] = "/repo/bin/Debug/NET10.0/Second.dll",
            ["Unselected"] = "/repo/bin/Debug/net8.0/Unselected.dll",
        };

        IReadOnlyCollection<string> result = ArchitectureTargetFrameworkSelector.Select(
            resolvedAssemblyPaths, ["Second", "First", "Missing", "Second"]);

        Assert.That(result, Is.EqualTo(new[] { "NET10.0" }));
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result, Does.Contain("net10.0"));
        Assert.That(result, Does.Not.Contain("net8.0"));
    }

    [Test]
    public void Select_ExtractsBinFrameworkAndFallsBackToAssemblyParentDirectory()
    {
        var resolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Runtime"] = "/repo/bin/Release/net8.0/linux-x64/Runtime.dll",
            ["Fallback"] = "/repo/output/net7.0/Fallback.dll",
        };

        IReadOnlyCollection<string> result = ArchitectureTargetFrameworkSelector.Select(
            resolvedAssemblyPaths, ["Runtime", "Fallback"]);

        Assert.That(result, Is.EqualTo(new[] { "net8.0", "net7.0" }));
    }

    [Test]
    public void Select_WithNoResolvedPaths_ReturnsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureTargetFrameworkSelector.Select(null, ["App"]), Is.Empty);
            Assert.That(
                ArchitectureTargetFrameworkSelector.Select(
                    new Dictionary<string, string>(StringComparer.Ordinal), ["App"]),
                Is.Empty);
        });
    }
}
