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
/// this stays true and stays the <em>only</em> path: it walks the reachable set from every
/// top-level root of this project — every direct <c>PackageReference</c> (currently just
/// <c>BenchmarkDotNet</c>) and the <c>ArchLinterNet.CEL</c> project reference itself — and asserts
/// Roslyn is reachable from <c>BenchmarkDotNet</c>'s tree specifically and from no other root's
/// tree. Proving "reachable via BenchmarkDotNet" alone is not sufficient: a second, independent
/// path (e.g. Roslyn entering transitively through <c>ArchLinterNet.CEL</c> itself, or through a
/// future direct dependency the whitelist test below would also flag) could exist at the same time
/// and this test would stay green if it only checked the one known path without also checking
/// every other root is Roslyn-free.
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

        // Resolve the reference to its canonical absolute path and compare against the real
        // ArchLinterNet.CEL.csproj file, rather than a substring match on the reference text — a
        // decoy project with a similar name (e.g. "FakeArchLinterNet.CEL.csproj", or a path
        // elsewhere on disk that merely contains that substring) would pass `Does.Contain` while
        // not actually being the real, architecture-governed ArchLinterNet.CEL project.
        var referencedProjectFullPath = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(csprojPath)!, projectReferences[0]));
        var expectedCelCsprojFullPath = Path.GetFullPath(
            Path.Combine(FindRepositoryRoot(), "src", "ArchLinterNet.CEL", "ArchLinterNet.CEL.csproj"));
        Assert.That(referencedProjectFullPath, Is.EqualTo(expectedCelCsprojFullPath).IgnoreCase,
            $"The benchmark project's ProjectReference resolves to '{referencedProjectFullPath}', " +
            $"not the real ArchLinterNet.CEL project at '{expectedCelCsprojFullPath}'.");
        Assert.That(File.Exists(expectedCelCsprojFullPath), Is.True,
            $"Expected the real ArchLinterNet.CEL project file to exist at '{expectedCelCsprojFullPath}'.");

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
        var root = document.RootElement;

        var targets = root.GetProperty("targets");
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

        // Every top-level root of this project: direct PackageReferences (from
        // project.frameworks.<tfm>.dependencies) plus any ProjectReference (libraries entries with
        // type "project", e.g. ArchLinterNet.CEL). A root not in this set could not have introduced
        // the dependency in the first place, so checking only known roots is exhaustive here.
        var allTopLevelRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frameworkDependencies = root.GetProperty("project").GetProperty("frameworks")
            .EnumerateObject().First().Value;
        if (frameworkDependencies.TryGetProperty("dependencies", out var directDeps))
        {
            foreach (var dep in directDeps.EnumerateObject())
                allTopLevelRoots.Add(dep.Name);
        }
        foreach (var library in libraries.EnumerateObject())
        {
            if (library.Value.TryGetProperty("type", out var typeProperty) && typeProperty.GetString() == "project")
                allTopLevelRoots.Add(library.Name.Split('/')[0]);
        }

        Assert.That(allTopLevelRoots, Does.Contain(KnownRoslynTransitiveRoot));
        var otherRoots = allTopLevelRoots.Where(r => !string.Equals(r, KnownRoslynTransitiveRoot, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.That(otherRoots, Is.Not.Empty,
            "Expected at least one other top-level root (e.g. the ArchLinterNet.CEL project " +
            "reference) besides BenchmarkDotNet — if this project ever has only one root, the " +
            "exclusivity check below becomes vacuous and this assumption needs revisiting.");

        var roslynPackages = allPackageIds.Where(id => id.StartsWith(RoslynPackagePrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.That(roslynPackages, Is.Not.Empty,
            $"Expected {RoslynPackagePrefix}* packages to be present transitively via " +
            $"{KnownRoslynTransitiveRoot} — if this now fails, BenchmarkDotNet no longer depends on " +
            "Roslyn and this test (and its documentation) should be simplified back to a plain " +
            "\"no Roslyn dependency\" claim.");

        var reachableFromBenchmarkDotNet = ReachableSet(KnownRoslynTransitiveRoot, dependencyGraph);
        var unexplainedRoslynPackages = roslynPackages.Where(id => !reachableFromBenchmarkDotNet.Contains(id)).ToList();
        Assert.That(unexplainedRoslynPackages, Is.Empty,
            $"Found {RoslynPackagePrefix}* package(s) not reachable from {KnownRoslynTransitiveRoot}'s " +
            $"own dependency tree: {{{string.Join(", ", unexplainedRoslynPackages)}}}. This means " +
            "something other than BenchmarkDotNet is now pulling in Roslyn — a real violation of " +
            "issue #168's \"no Roslyn dependency\" constraint, not the one known, documented, " +
            "unavoidable path this test accepts.");

        // Exclusivity: BenchmarkDotNet being A path to Roslyn does not prove it is the ONLY path.
        // Walk every other top-level root independently and confirm none of them can also reach a
        // Roslyn package — a second, parallel path (e.g. through ArchLinterNet.CEL itself gaining a
        // transitive Roslyn dependency in the future) would otherwise go undetected as long as
        // BenchmarkDotNet's own path kept the check above green.
        foreach (var otherRoot in otherRoots)
        {
            var reachableFromOtherRoot = ReachableSet(otherRoot, dependencyGraph);
            var roslynViaOtherRoot = roslynPackages.Where(reachableFromOtherRoot.Contains).ToList();
            Assert.That(roslynViaOtherRoot, Is.Empty,
                $"Found {RoslynPackagePrefix}* package(s) reachable from '{otherRoot}' as well as " +
                $"from {KnownRoslynTransitiveRoot}: {{{string.Join(", ", roslynViaOtherRoot)}}}. " +
                $"{KnownRoslynTransitiveRoot} must be the only path to Roslyn — a second, " +
                "independent path is a real, additional violation of issue #168's \"no Roslyn " +
                "dependency\" constraint even though the check above (which only proves " +
                $"reachability via {KnownRoslynTransitiveRoot}) would stay green.");
        }
    }

    private static HashSet<string> ReachableSet(string startingRoot, Dictionary<string, List<string>> dependencyGraph)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(startingRoot);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachable.Add(current))
                continue;
            if (dependencyGraph.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                    stack.Push(dep);
            }
        }
        return reachable;
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
