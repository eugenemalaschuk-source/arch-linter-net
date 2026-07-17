using System.Xml.Linq;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Guards the benchmark suite's required-constraint list from #168: the reusable benchmark
/// surface must never gain a dependency on Core, YAML, Roslyn, or an external CEL runtime.
/// <c>benchmarks/ArchLinterNet.CEL.Benchmarks</c> is deliberately outside
/// <c>architecture/dependencies.arch.yml</c>'s scanned assembly set (consistent with how the
/// <c>*.Tests</c> projects are also excluded from self-architecture-policy scanning), so this
/// constraint has no other automated enforcement. This test reads the project file directly
/// rather than depending on <c>ArchLinterNet.Core</c>'s project-discovery services, to keep this
/// test project's own "no Core dependency" property intact.
/// </summary>
[TestFixture]
public sealed class CelBenchmarksProjectDependencyTests
{
    private const string ProhibitedProjectReferenceFragment = "ArchLinterNet.Core";
    private static readonly string[] _prohibitedPackageReferences =
        ["YamlDotNet", "Microsoft.CodeAnalysis", "Buildalyzer"];

    [Test]
    public void BenchmarksProject_ReferencesOnlyCelAndBenchmarkDotNet()
    {
        var csprojPath = FindBenchmarksCsproj();
        var document = XDocument.Load(csprojPath);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(el => el.Attribute("Include")!.Value)
            .ToList();
        Assert.That(projectReferences, Has.Count.EqualTo(1),
            "The benchmark project must reference exactly one project: ArchLinterNet.CEL.");
        Assert.That(projectReferences[0], Does.Contain("ArchLinterNet.CEL.csproj"));
        Assert.That(projectReferences[0], Does.Not.Contain(ProhibitedProjectReferenceFragment + "."),
            $"{csprojPath} must not reference {ProhibitedProjectReferenceFragment} or any Core-dependent project.");

        var packageReferences = document.Descendants("PackageReference")
            .Select(el => el.Attribute("Include")!.Value)
            .ToList();
        foreach (var prohibited in _prohibitedPackageReferences)
        {
            Assert.That(packageReferences, Has.None.EqualTo(prohibited),
                $"{csprojPath} must not depend on {prohibited} (issue #168 required constraint: " +
                "no Core, YAML, Roslyn, or external CEL runtime dependency in the reusable benchmark surface).");
        }
    }

    private static string FindBenchmarksCsproj()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
            directory = directory.Parent;

        Assert.That(directory, Is.Not.Null, "Could not locate the repository root (ArchLinterNet.slnx) from the test output directory.");

        var csprojPath = Path.Combine(
            directory!.FullName, "benchmarks", "ArchLinterNet.CEL.Benchmarks", "ArchLinterNet.CEL.Benchmarks.csproj");
        Assert.That(File.Exists(csprojPath), Is.True, $"Expected benchmark project file at '{csprojPath}'.");
        return csprojPath;
    }
}
