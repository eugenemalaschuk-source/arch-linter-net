using System.Reflection;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Tests.SourceFactFixtures;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSourceFileFactIndexTests
{
    private static readonly Assembly _testAssembly = typeof(ArchitectureSourceFileFactIndexTests).Assembly;

    // Builds an index backed by FakeArchitectureFileSystem seeded with the given files.
    // sourceRoot is relative to repoRoot (e.g. "src").
    private static ArchitectureSourceFileFactIndex BuildIndex(
        string repoRoot,
        string sourceRoot,
        Dictionary<string, string> files)
    {
        string absoluteRepoRoot = FakePaths.Root(repoRoot);
        var fs = new FakeArchitectureFileSystem();
        string absoluteRoot = absoluteRepoRoot + "/" + sourceRoot;
        fs.AddDirectory(absoluteRoot);

        foreach ((string relativePath, string content) in files)
        {
            string absolutePath = absoluteRoot + "/" + relativePath;
            // Register every directory segment leading to the file
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
            fs);
    }

    // ── Single type per file ──────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_SingleTypeInFile_ReturnsEnrichedFact()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["Domain/SingleTypeFixture.cs"] = Source
            });

        bool found = index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/Domain/SingleTypeFixture.cs"));
        Assert.That(fact.FileNameWithoutExtension, Is.EqualTo("SingleTypeFixture"));
        Assert.That(fact.FolderSegments, Is.EqualTo(new[] { "src", "Domain" }));
        Assert.That(fact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
        Assert.That(fact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
        Assert.That(fact.Namespace, Is.EqualTo("ArchLinterNet.Core.Tests.SourceFactFixtures"));
        Assert.That(fact.SimpleTypeName, Is.EqualTo("SingleTypeFixture"));
    }

    // ── Multiple types per file ───────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_MultipleTypesInFile_EachGetsPathData()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class FileTypeA { }
                public sealed class FileTypeB { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["FileTypes.cs"] = Source
            });

        index.TryGetFact("ArchLinterNet.Core.Tests.SourceFactFixtures.FileTypeA", out ArchitectureDeclaredTypeFact factA);
        index.TryGetFact("ArchLinterNet.Core.Tests.SourceFactFixtures.FileTypeB", out ArchitectureDeclaredTypeFact factB);

        Assert.That(factA.SourceFilePath, Is.EqualTo("src/FileTypes.cs"));
        Assert.That(factB.SourceFilePath, Is.EqualTo("src/FileTypes.cs"));
        Assert.That(factA.FileNameWithoutExtension, Is.EqualTo("FileTypes"));
        Assert.That(factB.FileNameWithoutExtension, Is.EqualTo("FileTypes"));
    }

    // ── Reflection-only (no source) ───────────────────────────────────────────────────

    [Test]
    public void TryGetFact_EmptySourceRoots_ReturnsReflectionOnlyFact()
    {
        var index = new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            FakePaths.Root("/fake/repo"),
            Array.Empty<string>(),
            new FakeArchitectureFileSystem());

        bool found = index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.Null);
        Assert.That(fact.FileNameWithoutExtension, Is.Null);
        Assert.That(fact.FolderSegments, Is.Empty);
        Assert.That(fact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
    }

    // ── Path normalization ────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_SourcePath_IsNormalizedToForwardSlashes()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["A/B/SingleTypeFixture.cs"] = Source
            });

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.SourceFilePath, Does.Not.Contain("\\"));
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/A/B/SingleTypeFixture.cs"));
    }

    // ── Folder segments ───────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_NestedDirectoryPath_FolderSegmentsMatchExpected()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["MyProject/Domain/SingleTypeFixture.cs"] = Source
            });

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.FolderSegments, Is.EqualTo(new[] { "src", "MyProject", "Domain" }));
    }

    [Test]
    public void TryGetFact_FileAtSourceRootDirectly_FolderSegmentsContainRootOnly()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["SingleTypeFixture.cs"] = Source
            });

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.FolderSegments, Is.EqualTo(new[] { "src" }));
    }

    // ── Namespace segments ────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_NamespaceSegments_SplitOnDot()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["SingleTypeFixture.cs"] = Source
            });

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.NamespaceSegments,
            Is.EqualTo(new[] { "ArchLinterNet", "Core", "Tests", "SourceFactFixtures" }));
    }

    // ── Nested type ───────────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_NestedType_FullTypeNameUsesClrPlusFormat()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class OuterFixture {
                    public sealed class InnerFixture { }
                }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["OuterFixture.cs"] = Source
            });

        bool found = index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.OuterFixture+InnerFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/OuterFixture.cs"));
        Assert.That(fact.SimpleTypeName, Is.EqualTo("InnerFixture"));
        Assert.That(fact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    // ── Generic type ─────────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_GenericType_BacktickArityMatchesReflection()
    {
        // Type.FullName for GenericFixture<T> is "...GenericFixture`1" — the parser must
        // produce the same backtick-N format for the lookup to succeed.
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class GenericFixture<T> { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["GenericFixture.cs"] = Source
            });

        string clrName = typeof(GenericFixture<>).FullName!;
        bool found = index.TryGetFact(clrName, out ArchitectureDeclaredTypeFact fact);

        Assert.That(found, Is.True);
        Assert.That(fact.SourceFilePath, Is.EqualTo("src/GenericFixture.cs"));
        Assert.That(fact.SimpleTypeName, Is.EqualTo("GenericFixture"));
    }

    // ── Record type kind ──────────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_RecordType_TypeKindIsRecordWhenSourceAvailable()
    {
        // Reflection cannot distinguish record from class — only Roslyn source analysis can.
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public record FixtureRecord(string Name);
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["FixtureRecord.cs"] = Source
            });

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.FixtureRecord",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Record));
    }

    [Test]
    public void TryGetFact_RecordType_TypeKindIsClassWhenNoSourceAvailable()
    {
        // Without source, reflection falls back to Class for record class types.
        var index = new ArchitectureSourceFileFactIndex(
            new[] { _testAssembly },
            FakePaths.Root("/fake/repo"),
            Array.Empty<string>(),
            new FakeArchitectureFileSystem());

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.FixtureRecord",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.TypeKind, Is.EqualTo(ArchitectureTypeKind.Class));
    }

    // ── Partial class ambiguity ───────────────────────────────────────────────────────

    [Test]
    public void Ambiguities_PartialClassAcrossTwoFiles_ProducesAmbiguityRecord()
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

        Assert.That(ambiguities, Has.Count.EqualTo(1));
        Assert.That(ambiguities[0].FullTypeName,
            Is.EqualTo("ArchLinterNet.Core.Tests.SourceFactFixtures.PartialFixture"));
        Assert.That(ambiguities[0].SourceFilePaths, Has.Count.EqualTo(2));
    }

    [Test]
    public void TryGetFact_PartialClassAcrossTwoFiles_FactHasNullSourcePath()
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

        index.TryGetFact(
            "ArchLinterNet.Core.Tests.SourceFactFixtures.PartialFixture",
            out ArchitectureDeclaredTypeFact fact);

        Assert.That(fact.SourceFilePath, Is.Null);
        Assert.That(fact.FileNameWithoutExtension, Is.Null);
        Assert.That(fact.FolderSegments, Is.Empty);
        Assert.That(fact.AssemblyName, Is.EqualTo("ArchLinterNet.Core.Tests"));
        Assert.That(fact.Namespace, Is.EqualTo("ArchLinterNet.Core.Tests.SourceFactFixtures"));
    }

    // ── GetFactsForFile ────────────────────────────────────────────────────────────────

    [Test]
    public void GetFactsForFile_TwoTypesInFile_ReturnsBothFacts()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class FileTypeA { }
                public sealed class FileTypeB { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["FileTypes.cs"] = Source
            });

        IReadOnlyList<ArchitectureDeclaredTypeFact> facts =
            index.GetFactsForFile("src/FileTypes.cs");

        Assert.That(
            facts.Select(f => f.FullTypeName),
            Is.EquivalentTo(new[]
            {
                "ArchLinterNet.Core.Tests.SourceFactFixtures.FileTypeA",
                "ArchLinterNet.Core.Tests.SourceFactFixtures.FileTypeB"
            }));
    }

    [Test]
    public void GetFactsForFile_UnknownPath_ReturnsEmpty()
    {
        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src", new Dictionary<string, string>());

        IReadOnlyList<ArchitectureDeclaredTypeFact> facts =
            index.GetFactsForFile("src/DoesNotExist.cs");

        Assert.That(facts, Is.Empty);
    }

    // ── GetFactsForNamespace ────────────────────────────────────────────────────────────

    [Test]
    public void GetFactsForNamespace_KnownNamespace_ReturnsMatchingFacts()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class FileTypeA { }
                public sealed class FileTypeB { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["FileTypes.cs"] = Source
            });

        IReadOnlyList<ArchitectureDeclaredTypeFact> facts =
            index.GetFactsForNamespace("ArchLinterNet.Core.Tests.SourceFactFixtures");

        Assert.That(
            facts.Select(f => f.SimpleTypeName),
            Does.Contain("FileTypeA").And.Contains("FileTypeB"));
    }

    [Test]
    public void GetFactsForNamespace_UnknownNamespace_ReturnsEmpty()
    {
        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src", new Dictionary<string, string>());

        IReadOnlyList<ArchitectureDeclaredTypeFact> facts =
            index.GetFactsForNamespace("NonExistent.Namespace");

        Assert.That(facts, Is.Empty);
    }

    // ── TryGetFact unknown name ────────────────────────────────────────────────────────

    [Test]
    public void TryGetFact_UnknownTypeName_ReturnsFalse()
    {
        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src", new Dictionary<string, string>());

        bool found = index.TryGetFact("Does.Not.Exist", out _);

        Assert.That(found, Is.False);
    }

    // ── Lazy construction ──────────────────────────────────────────────────────────────

    [Test]
    public void Construction_WithNonExistentSourceRoot_DoesNotThrow()
    {
        // Non-existent directory: DirectoryExists returns false → no file enumeration.
        // Construction must succeed (lazy); AllFacts returns reflection-only facts.
        var fs = new FakeArchitectureFileSystem();

        Assert.DoesNotThrow(() =>
        {
            var index = new ArchitectureSourceFileFactIndex(
                new[] { _testAssembly },
                FakePaths.Root("/fake/repo"),
                new[] { "src" },
                fs);

            Assert.That(index.AllFacts, Is.Not.Empty);
            Assert.That(index.AllFacts.All(f => f.SourceFilePath == null), Is.True);
        });
    }

    [Test]
    public void Ambiguities_NoAmbiguousTypes_ReturnsEmpty()
    {
        const string Source = """
            namespace ArchLinterNet.Core.Tests.SourceFactFixtures {
                public sealed class SingleTypeFixture { }
            }
            """;

        ArchitectureSourceFileFactIndex index = BuildIndex(
            "/fake/repo", "src",
            new Dictionary<string, string>
            {
                ["SingleTypeFixture.cs"] = Source
            });

        Assert.That(index.Ambiguities, Is.Empty);
    }
}
