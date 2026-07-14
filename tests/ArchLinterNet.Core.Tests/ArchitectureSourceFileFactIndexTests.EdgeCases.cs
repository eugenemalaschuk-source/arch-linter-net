using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Edge-case tests for ArchitectureSourceFileFactIndex split into a separate partial file
// to keep each file under the 800-line decomposition limit.
[TestFixture]
public sealed partial class ArchitectureSourceFileFactIndexTests
{
    // ── Unique-file ambiguity (4.1) ────────────────────────────────────────────────────

    [Test]
    public void Ambiguities_SameTypeAppearsTwiceInSameFile_NotAmbiguous()
    {
        // A type that appears twice in source entries for the same file (e.g. from overlapping
        // source roots scanning the same file, or multiple partial declarations in one file)
        // is NOT an ambiguity — ambiguity requires more than one distinct file path.
        const string PartA = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;

        // Scan the same source root twice (simulates overlapping roots or a duplicate scan).
        // Both entries map to the same normalized file path → unique paths count == 1 → no ambiguity.
        ArchitectureSourceFileFactIndex index = BuildIndexWithTwoRoots(
            "/fake/repo",
            sourceRoot1: "src1", files1: new Dictionary<string, string> { ["PartialFixture.Part1.cs"] = PartA },
            sourceRoot2: "src1", files2: new Dictionary<string, string> { ["PartialFixture.Part1.cs"] = PartA });

        // Single unique path → no ambiguity and source path is populated.
        Assert.That(index.Ambiguities, Is.Empty);
        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.PartialFixture",
            out ArchitectureDeclaredTypeFact fact);
        Assert.That(fact.SourceFilePath, Is.Not.Null);
    }

    [Test]
    public void Ambiguities_PartialClassInTwoDistinctFiles_IsAmbiguous()
    {
        // Two files with different names → two distinct paths → genuine ambiguity.
        const string PartA = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;
        const string PartB = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["PartialFixture.Part1.cs"] = PartA,
                ["PartialFixture.Part2.cs"] = PartB
            });

        Assert.That(index.Ambiguities, Has.Count.EqualTo(1));
        Assert.That(index.Ambiguities[0].AssemblyName, Is.EqualTo(TestAssemblyName));
        Assert.That(index.Ambiguities[0].SourceFilePaths, Has.Count.EqualTo(2));
    }

    // ── Deterministic ordering (4.2) ───────────────────────────────────────────────────

    [Test]
    public void AllFacts_AreOrderedByFullTypeNameOrdinal()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class FileTypeB { }
                public sealed class FileTypeA { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["FileTypes.cs"] = Source
            });

        IReadOnlyList<ArchitectureDeclaredTypeFact> allFacts = index.AllFacts;
        List<ArchitectureDeclaredTypeFact> sorted = allFacts
            .OrderBy(f => f.FullTypeName, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            allFacts.Select(f => f.FullTypeName),
            Is.EqualTo(sorted.Select(f => f.FullTypeName)));
    }

    [Test]
    public void Ambiguities_AreOrderedByFullTypeNameOrdinal()
    {
        const string PartA = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;
        const string PartB = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["PartialFixture.Part1.cs"] = PartA,
                ["PartialFixture.Part2.cs"] = PartB
            });

        IReadOnlyList<ArchitectureDeclaredTypeSourceAmbiguity> ambiguities = index.Ambiguities;
        List<string> sortedNames = ambiguities
            .Select(a => a.FullTypeName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            ambiguities.Select(a => a.FullTypeName),
            Is.EqualTo(sortedNames));
    }

    [Test]
    public void Ambiguity_SourceFilePaths_AreOrderedOrdinal()
    {
        const string Part = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public partial class PartialFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["Z_PartialFixture.cs"] = Part,
                ["A_PartialFixture.cs"] = Part
            });

        Assert.That(index.Ambiguities, Has.Count.EqualTo(1));
        IReadOnlyList<string> paths = index.Ambiguities[0].SourceFilePaths;
        List<string> sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.That(paths, Is.EqualTo(sorted));
    }

    [Test]
    public void TryGetFact_UnownedSourceRoot_WithMultipleTargetAssemblies_DoesNotAttachForeignDeclaration()
    {
        const string ForeignSource = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string absoluteRoot = absoluteRepoRoot + "/foreign";
        fs.AddDirectory(absoluteRoot);
        fs.AddFile(absoluteRoot + "/SingleTypeFixture.cs", ForeignSource, DateTime.UtcNow);

        var index = new ArchitectureSourceFileFactIndex(
            _testAndCoreAssemblies,
            absoluteRepoRoot,
            _foreignSourceRoot,
            null,
            fs,
            projectDiscovery: null,
            sourceRootAssemblyOwnership: null);

        bool found = index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.Null,
            "Source declarations from an unowned root must not be attached by CLR-name coincidence alone");
        Assert.That(index.Ambiguities, Is.Empty);
    }

    // ── Preprocessor symbols through the index (3.2) ─────────────────────────────────

    [Test]
    public void TryGetFact_WithPreprocessorSymbol_FindsConditionalType()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
            #if FEATURE_X
                public class SingleTypeFixture { }
            #endif
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndexWithPreprocessorSymbols(
            "/fake/repo", "src",
            files: new Dictionary<string, string> { ["SingleTypeFixture.cs"] = Source },
            preprocessorSymbols: _featureXSymbols);

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.SourceFilePath, Is.EqualTo("src/SingleTypeFixture.cs"));
        Assert.That(fact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    [Test]
    public void TryGetFact_WithoutPreprocessorSymbol_DoesNotEnrichConditionalType()
    {
        // Without the defining symbol, the parser does not see the declaration →
        // the type has no source enrichment (reflection-only fact, null SourceFilePath).
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
            #if FEATURE_X
                public class SingleTypeFixture { }
            #endif
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndexWithPreprocessorSymbols(
            "/fake/repo", "src",
            files: new Dictionary<string, string> { ["SingleTypeFixture.cs"] = Source },
            preprocessorSymbols: null);

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.SourceFilePath, Is.Null,
            "Without the symbol the declaration is invisible to the parser → no source enrichment");
    }

    // ── Assembly-aware lookup (3.1) ────────────────────────────────────────────────────
    //
    // Two-assembly regression for CLR name collision: the test and core assemblies have no
    // overlapping CLR names, so the multi-assembly guard (multiAssemblyNames) cannot be exercised
    // via real assemblies here. The tests below verify the new overload API correctness; the guard
    // itself is covered by the implementation unit-reading the reflectionFactsByName list count.

    private static readonly Assembly _coreAssembly = typeof(ArchitectureDeclaredTypeFact).Assembly;
    private static readonly Assembly[] _testAndCoreAssemblies = [_testAssembly, _coreAssembly];
    private static readonly string[] _featureXSymbols = ["FEATURE_X"];
    private static readonly string[] _singleSourceRoot = ["src"];
    private static readonly string[] _foreignSourceRoot = ["foreign"];
    private static readonly string[] _singleTestAssemblyName = [TestAssemblyName];
    private static readonly string[] _coreAndTestAssemblyNames = ["ArchLinterNet.Core.Tests", "ArchLinterNet.Core"];
    private static readonly string[] _net10 = ["net10.0"];
    private static readonly string[] _discoveredSourceRoots = ["src/App", "src/Core"];
    private static readonly string[] _srcDomainRoot = ["src/App/Domain"];

    [Test]
    public void TryGetFact_AssemblyAndName_ReturnsCorrectFactForSpecifiedAssembly()
    {
        // TryGetFact(assemblyName, fullTypeName) must return only the fact belonging to the
        // requested assembly, not a fact from a different assembly that happens to share the name.
        var index = new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly, _coreAssembly },
            FakePaths.Root("/fake/repo"),
            Array.Empty<string>(),
            null,
            new FakeArchitectureFileSystem());

        string testOnlyType = "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture";

        bool foundInTest = index.TryGetFact("ArchLinterNet.Core.Tests", testOnlyType, out ArchitectureDeclaredTypeFact fact);
        bool foundInCore = index.TryGetFact("ArchLinterNet.Core", testOnlyType, out _);

        Assert.That(foundInTest, Is.True);
        Assert.That(fact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
        Assert.That(foundInCore, Is.False,
            "Type from test assembly must not be visible under a different assembly name");
    }

    [Test]
    public void TryGetFact_AssemblyAndName_ReturnsFalseForUnknownAssemblyName()
    {
        var index = new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            FakePaths.Root("/fake/repo"),
            Array.Empty<string>(),
            null,
            new FakeArchitectureFileSystem());

        bool found = index.TryGetFact(
            "SomeOther.Assembly",
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetFact_AssemblyAndName_WithSource_ReturnsEnrichedFact()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string> { ["SingleTypeFixture.cs"] = Source });

        bool found = index.TryGetFact(
            "ArchLinterNet.Core.Tests",
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/SingleTypeFixture.cs"));
    }

    [Test]
    public void TryGetFact_AssemblyAndName_TwoAssembliesOneSourceRoot_EachAssemblySeesOwnFact()
    {
        // Regression: index backed by two assemblies must not expose a type from assembly A
        // when the caller requests it by assembly B name, even when source is available.
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string absoluteRoot = absoluteRepoRoot + "/src";
        fs.AddDirectory(absoluteRoot);
        fs.AddFile(absoluteRoot + "/SingleTypeFixture.cs", Source, DateTime.UtcNow);

        var index = new ArchitectureSourceFileFactIndex(
            _testAndCoreAssemblies,
            absoluteRepoRoot,
            _singleSourceRoot,
            null,
            fs);

        string typeName = "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture";

        bool inTest = index.TryGetFact("ArchLinterNet.Core.Tests", typeName, out ArchitectureDeclaredTypeFact testFact);
        bool inCore = index.TryGetFact("ArchLinterNet.Core", typeName, out _);

        Assert.That(inTest, Is.True);
        Assert.That(testFact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
        Assert.That(inCore, Is.False,
            "ArchLinterNet.Core assembly does not declare SingleTypeFixture");
    }

    [Test]
    public void TryGetFact_FullNameCollisionAcrossAssemblies_ReturnsFalseForAmbiguousLookup()
    {
        const string CollidingFullTypeName = "Collision.Namespace.SharedType";
        (Assembly assemblyA, string nameA) = CreateDynamicAssemblyWithType("CollisionFixtureA", CollidingFullTypeName);
        (Assembly assemblyB, string nameB) = CreateDynamicAssemblyWithType("CollisionFixtureB", CollidingFullTypeName);

        var index = new ArchitectureSourceFileFactIndex(
            new[] { assemblyA, assemblyB },
            FakePaths.Root("/fake/repo"),
            Array.Empty<string>(),
            null,
            new FakeArchitectureFileSystem());

        bool foundByName = index.TryGetFact(CollidingFullTypeName, out _);
        bool foundInA = index.TryGetFact(nameA, CollidingFullTypeName, out ArchitectureDeclaredTypeFact factA);
        bool foundInB = index.TryGetFact(nameB, CollidingFullTypeName, out ArchitectureDeclaredTypeFact factB);

        Assert.That(foundByName, Is.False,
            "Single-argument lookup must not silently choose one assembly when the CLR name is ambiguous");
        Assert.That(foundInA, Is.True);
        Assert.That(foundInB, Is.True);
        Assert.That(factA.AssemblyName, Is.EqualTo(nameA));
        Assert.That(factB.AssemblyName, Is.EqualTo(nameB));
    }

    [Test]
    public void TryGetFact_CommonSourceRootWithMultipleDiscoveredProjects_ResolvesOwnershipPerProjectSubtree()
    {
        const string TestSource = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;
        const string CoreSource = """
            namespace ArchLinterNet.Core.Execution {
                public sealed class ArchitectureSourceFileFactIndex { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string appRoot = absoluteRepoRoot + "/src/App";
        string coreRoot = absoluteRepoRoot + "/src/Core";
        fs.AddDirectory(absoluteRepoRoot + "/src");
        fs.AddDirectory(appRoot);
        fs.AddDirectory(coreRoot);
        fs.AddFile(appRoot + "/SingleTypeFixture.cs", TestSource, DateTime.UtcNow);
        fs.AddFile(coreRoot + "/ArchitectureSourceFileFactIndex.cs", CoreSource, DateTime.UtcNow);

        ProjectDiscoveryResult discovery = new(
            _coreAndTestAssemblyNames,
            Array.Empty<string>(),
            _discoveredSourceRoots,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("src/App/App.csproj", "ArchLinterNet.Core.Tests", _net10),
                new ArchitectureDiscoveredProject("src/Core/Core.csproj", "ArchLinterNet.Core", _net10)
            ]
        };

        var index = new ArchitectureSourceFileFactIndex(
            _testAndCoreAssemblies,
            absoluteRepoRoot,
            _singleSourceRoot,
            null,
            fs,
            discovery,
            sourceRootAssemblyOwnership: null);

        bool foundTest = index.TryGetFact(
            "ArchLinterNet.Core.Tests",
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact testFact);
        bool foundCore = index.TryGetFact(
            "ArchLinterNet.Core",
            "ArchLinterNet.Core.Execution.ArchitectureSourceFileFactIndex",
            out ArchitectureDeclaredTypeFact coreFact);

        Assert.That(foundTest, Is.True);
        Assert.That(foundCore, Is.True);
        Assert.That(testFact.SourceFilePath, Is.EqualTo("src/App/SingleTypeFixture.cs"));
        Assert.That(coreFact.SourceFilePath, Is.EqualTo("src/Core/ArchitectureSourceFileFactIndex.cs"));
    }

    [Test]
    public void TryGetFact_RootLevelProjectOwnsFilesUnderConfiguredSourceRoot()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string srcRoot = absoluteRepoRoot + "/src";
        fs.AddDirectory(srcRoot);
        fs.AddFile(srcRoot + "/SingleTypeFixture.cs", Source, DateTime.UtcNow);

        ProjectDiscoveryResult discovery = new(
            _singleTestAssemblyName,
            Array.Empty<string>(),
            _singleSourceRoot,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("App.csproj", TestAssemblyName, _net10)
            ]
        };

        var index = new ArchitectureSourceFileFactIndex(
            _testAssemblyOnly,
            absoluteRepoRoot,
            _singleSourceRoot,
            null,
            fs,
            discovery,
            sourceRootAssemblyOwnership: null);

        bool found = index.TryGetFact(
            TestAssemblyName,
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/SingleTypeFixture.cs"));
    }

    [Test]
    public void TryGetFact_ConfiguredSourceRootInsideProjectDirectory_UsesOwningProject()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string domainRoot = absoluteRepoRoot + "/src/App/Domain";
        fs.AddDirectory(absoluteRepoRoot + "/src");
        fs.AddDirectory(absoluteRepoRoot + "/src/App");
        fs.AddDirectory(domainRoot);
        fs.AddFile(domainRoot + "/SingleTypeFixture.cs", Source, DateTime.UtcNow);

        ProjectDiscoveryResult discovery = new(
            _singleTestAssemblyName,
            Array.Empty<string>(),
            _srcDomainRoot,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("src/App/App.csproj", TestAssemblyName, _net10)
            ]
        };

        var index = new ArchitectureSourceFileFactIndex(
            _testAssemblyOnly,
            absoluteRepoRoot,
            _srcDomainRoot,
            null,
            fs,
            discovery,
            sourceRootAssemblyOwnership: null);

        bool found = index.TryGetFact(
            TestAssemblyName,
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/App/Domain/SingleTypeFixture.cs"));
    }

    [Test]
    public void TryGetFact_TwoProjectsInSameDirectory_DoesNotChooseOwnerByDiscoveryOrder()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        string absoluteRepoRoot = FakePaths.Root("/fake/repo");
        var fs = new FakeArchitectureFileSystem();
        string srcRoot = absoluteRepoRoot + "/src";
        fs.AddDirectory(srcRoot);
        fs.AddFile(srcRoot + "/SingleTypeFixture.cs", Source, DateTime.UtcNow);

        ProjectDiscoveryResult discovery = new(
            _coreAndTestAssemblyNames,
            Array.Empty<string>(),
            _singleSourceRoot,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("src/AppA.csproj", "ArchLinterNet.Core.Tests", _net10),
                new ArchitectureDiscoveredProject("src/AppB.csproj", "ArchLinterNet.Core", _net10)
            ]
        };

        var index = new ArchitectureSourceFileFactIndex(
            _testAndCoreAssemblies,
            absoluteRepoRoot,
            _singleSourceRoot,
            null,
            fs,
            discovery,
            sourceRootAssemblyOwnership: null);

        bool foundTest = index.TryGetFact(
            "ArchLinterNet.Core.Tests",
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact testFact);
        bool foundCore = index.TryGetFact(
            "ArchLinterNet.Core",
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out _);

        Assert.That(foundTest, Is.True);
        Assert.That(testFact.SourceFilePath, Is.Null,
            "When two assemblies own the same-most-specific project subtree, source enrichment must stay unavailable");
        Assert.That(foundCore, Is.False);
    }

    // ── Helpers shared across partial files ───────────────────────────────────────────

    private static ArchitectureSourceFileFactIndex BuildIndexWithPreprocessorSymbols(
        string repoRoot,
        string sourceRoot,
        Dictionary<string, string> files,
        IReadOnlyList<string>? preprocessorSymbols)
    {
        string absoluteRepoRoot = FakePaths.Root(repoRoot);
        var fs = new FakeArchitectureFileSystem();
        string absoluteRoot = absoluteRepoRoot + "/" + sourceRoot;
        fs.AddDirectory(absoluteRoot);

        foreach ((string relativePath, string content) in files)
        {
            string absolutePath = absoluteRoot + "/" + relativePath;
            string dir = absolutePath;
            while (true)
            {
                int slash = dir.LastIndexOf('/');
                if (slash < 0) break;
                dir = dir[..slash];
                if (dir.Length <= absoluteRepoRoot.Length) break;
                fs.AddDirectory(dir);
            }

            fs.AddFile(absolutePath, content, DateTime.UtcNow);
        }

        return new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            absoluteRepoRoot,
            new[] { sourceRoot },
            preprocessorSymbols,
            fs,
            projectDiscovery: null,
            sourceRootAssemblyOwnership: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sourceRoot] = TestAssemblyName
            });
    }

    private static ArchitectureSourceFileFactIndex BuildIndexWithTwoRoots(
        string repoRoot,
        string sourceRoot1, Dictionary<string, string> files1,
        string sourceRoot2, Dictionary<string, string> files2)
    {
        string absoluteRepoRoot = FakePaths.Root(repoRoot);
        var fs = new FakeArchitectureFileSystem();

        void Seed(string sourceRoot, Dictionary<string, string> files)
        {
            string absoluteRoot = absoluteRepoRoot + "/" + sourceRoot;
            fs.AddDirectory(absoluteRoot);
            foreach ((string relativePath, string content) in files)
            {
                string absolutePath = absoluteRoot + "/" + relativePath;
                string dir = absolutePath;
                while (true)
                {
                    int slash = dir.LastIndexOf('/');
                    if (slash < 0) break;
                    dir = dir[..slash];
                    if (dir.Length <= absoluteRepoRoot.Length) break;
                    fs.AddDirectory(dir);
                }

                fs.AddFile(absolutePath, content, DateTime.UtcNow);
            }
        }

        Seed(sourceRoot1, files1);
        Seed(sourceRoot2, files2);

        return new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            absoluteRepoRoot,
            new[] { sourceRoot1, sourceRoot2 },
            null,
            fs,
            projectDiscovery: null,
            sourceRootAssemblyOwnership: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sourceRoot1] = TestAssemblyName,
                [sourceRoot2] = TestAssemblyName
            });
    }

    private static (Assembly Assembly, string AssemblyName) CreateDynamicAssemblyWithType(
        string assemblyName,
        string fullTypeName)
    {
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

        int lastDot = fullTypeName.LastIndexOf('.');
        string namespaceName = fullTypeName[..lastDot];
        string typeName = fullTypeName[(lastDot + 1)..];
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"{namespaceName}.{typeName}",
            TypeAttributes.Public | TypeAttributes.Class);
        _ = typeBuilder.CreateType();

        return (assemblyBuilder, assemblyName);
    }
}
