using System.Text.Json;
using System.Xml.Linq;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Guards the benchmark suite's required-constraint list from #168: the reusable benchmark
/// surface must never gain a <em>direct</em> dependency on Core, YAML, Roslyn, or an external CEL
/// runtime, and any Roslyn (<c>Microsoft.CodeAnalysis*</c>) package present transitively must be
/// reachable only through <c>BenchmarkDotNet</c> itself — not introduced independently.
/// <c>benchmarks/ArchLinterNet.CEL.Benchmarks</c> is deliberately outside
/// <c>architecture/dependencies.arch.yml</c>'s scanned assembly set (consistent with how the
/// <c>*.Tests</c> projects are also excluded from self-architecture-policy scanning), so this
/// constraint has no other automated enforcement. This test reads the project file and the
/// restored dependency graph directly rather than depending on <c>ArchLinterNet.Core</c>'s
/// project-discovery services, to keep this test project's own "no Core dependency" property
/// intact.
/// </summary>
/// <remarks>
/// BenchmarkDotNet 0.14.0 itself transitively depends on <c>Microsoft.CodeAnalysis.CSharp</c> for
/// its own internal benchmark-harness generation — this is inherent to using the benchmarking tool
/// the issue explicitly directs ("Prefer BenchmarkDotNet..."), not a Roslyn dependency this
/// project's benchmark or test code introduces or calls into. No file in
/// <c>benchmarks/ArchLinterNet.CEL.Benchmarks</c> references a <c>Microsoft.CodeAnalysis</c>
/// namespace. <see cref="BenchmarksProject_RoslynIsOnlyReachableThroughBenchmarkDotNet"/> verifies
/// this stays true: if a future dependency (direct or transitive, from any package) introduced
/// Roslyn through a path other than <c>BenchmarkDotNet</c>, or if the whitelist test below ever
/// let a second direct package reference in, that would be a real, undocumented violation of the
/// "no Roslyn dependency" constraint — this only accepts the one specific, known, unavoidable path.
/// </remarks>
[TestFixture]
public sealed class CelBenchmarksProjectDependencyTests
{
    private static readonly string[] _allowedPackageReferences = ["BenchmarkDotNet"];
    private const string KnownRoslynTransitiveRoot = "BenchmarkDotNet";
    private const string RoslynPackagePrefix = "Microsoft.CodeAnalysis";

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

        // Whitelist, not a blocklist of specific forbidden names: any PackageReference other than
        // BenchmarkDotNet fails this test, so a future addition of Core/YAML/an external CEL
        // runtime/anything else is caught regardless of what it happens to be named. A second
        // direct dependency could also introduce Roslyn through a path
        // BenchmarksProject_RoslynIsOnlyReachableThroughBenchmarkDotNet below does not expect.
        var packageReferences = document.Descendants("PackageReference")
            .Select(el => el.Attribute("Include")!.Value)
            .ToList();
        Assert.That(packageReferences, Is.EquivalentTo(_allowedPackageReferences),
            $"{csprojPath} must depend on exactly {{{string.Join(", ", _allowedPackageReferences)}}} " +
            "(issue #168 required constraint: no Core, YAML, Roslyn, or external CEL runtime " +
            $"dependency in the reusable benchmark surface); found {{{string.Join(", ", packageReferences)}}}.");
    }

    [Test]
    public void BenchmarksProject_RoslynIsOnlyReachableThroughBenchmarkDotNet()
    {
        var assetsPath = FindProjectAssetsJson();
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));

        var targets = document.RootElement.GetProperty("targets");
        var targetFramework = targets.EnumerateObject().First();
        var libraries = targetFramework.Value;

        // Build a name -> direct-dependency-names graph for the resolved target, keyed by package
        // ID without its resolved version (assets.json keys are "PackageId/Version").
        var dependencyGraph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var allPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries.EnumerateObject())
        {
            var packageId = library.Name.Split('/')[0];
            allPackageIds.Add(packageId);
            var deps = new List<string>();
            if (library.Value.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dep in dependencies.EnumerateObject())
                    deps.Add(dep.Name);
            }
            dependencyGraph[packageId] = deps;
        }

        // Every Roslyn package actually present in the resolved graph must be reachable by walking
        // outward from BenchmarkDotNet — if it is reachable from nowhere (dependencyGraph has no
        // entry reaching it) or reachable only from some other root, that is a new, undocumented
        // source of the Roslyn dependency this test does not know about.
        var reachableFromBenchmarkDotNet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(KnownRoslynTransitiveRoot);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachableFromBenchmarkDotNet.Add(current))
                continue;
            if (dependencyGraph.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                    stack.Push(dep);
            }
        }

        var roslynPackages = allPackageIds.Where(id => id.StartsWith(RoslynPackagePrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.That(roslynPackages, Is.Not.Empty,
            $"Expected {RoslynPackagePrefix}* packages to be present transitively via " +
            $"{KnownRoslynTransitiveRoot} — if this now fails, BenchmarkDotNet no longer depends on " +
            "Roslyn and this test (and its documentation) should be simplified back to a plain " +
            "\"no Roslyn dependency\" claim.");

        var unexplainedRoslynPackages = roslynPackages.Where(id => !reachableFromBenchmarkDotNet.Contains(id)).ToList();
        Assert.That(unexplainedRoslynPackages, Is.Empty,
            $"Found {RoslynPackagePrefix}* package(s) not reachable from {KnownRoslynTransitiveRoot}'s " +
            $"own dependency tree: {{{string.Join(", ", unexplainedRoslynPackages)}}}. This means " +
            "something other than BenchmarkDotNet is now pulling in Roslyn — a real violation of " +
            "issue #168's \"no Roslyn dependency\" constraint, not the one known, documented, " +
            "unavoidable path this test accepts.");
    }

    private static string FindBenchmarksCsproj()
    {
        var csprojPath = Path.Combine(
            FindRepositoryRoot(), "benchmarks", "ArchLinterNet.CEL.Benchmarks", "ArchLinterNet.CEL.Benchmarks.csproj");
        Assert.That(File.Exists(csprojPath), Is.True, $"Expected benchmark project file at '{csprojPath}'.");
        return csprojPath;
    }

    private static string FindProjectAssetsJson()
    {
        var assetsPath = Path.Combine(
            FindRepositoryRoot(), "benchmarks", "ArchLinterNet.CEL.Benchmarks", "obj", "project.assets.json");
        Assert.That(File.Exists(assetsPath), Is.True,
            $"Expected a restored 'project.assets.json' at '{assetsPath}' — run 'rtk dotnet restore' " +
            "(or 'rtk make restore') before running this test.");
        return assetsPath;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
            directory = directory.Parent;

        Assert.That(directory, Is.Not.Null, "Could not locate the repository root (ArchLinterNet.slnx) from the test output directory.");
        return directory!.FullName;
    }
}
