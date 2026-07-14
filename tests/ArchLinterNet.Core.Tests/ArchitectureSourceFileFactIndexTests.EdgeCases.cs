using System.Reflection;
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
            preprocessorSymbols: new[] { "FEATURE_X" });

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
            fs);
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
            fs);
    }
}
