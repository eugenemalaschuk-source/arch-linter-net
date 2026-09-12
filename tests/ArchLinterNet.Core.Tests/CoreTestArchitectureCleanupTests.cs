using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Ratchets the test-cleanup boundary without governing legitimate generated interop or source
/// snippets that deliberately model C# partial declarations.
/// </summary>
[TestFixture]
public sealed class CoreTestArchitectureCleanupTests
{
    [Test]
    public void PartialLanguageFixture_RemainsDiscoverableWithEveryDeclarationPath()
    {
        const string Part = "namespace Fixture { public partial class IntentionalFixture { } }";
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory("/fake/src");
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part1.cs", Part, DateTime.UtcNow);
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part2.cs", Part, DateTime.UtcNow);

        var index = new ArchitectureSourceFileFactIndex(
            [typeof(CoreTestArchitectureCleanupTests).Assembly],
            "/fake",
            ["src"],
            preprocessorSymbols: null,
            fileSystem);

        IReadOnlyList<ArchitectureTypeSourceDeclaration> declarations = index.SourceDeclarations
            .Where(declaration => declaration.FullTypeName == "Fixture.IntentionalFixture")
            .ToArray();

        Assert.That(declarations, Has.Count.EqualTo(2));
        Assert.That(declarations.All(declaration => declaration.IsPartial), Is.True);
        Assert.That(
            declarations.Select(declaration => declaration.SourceFilePath),
            Is.EqualTo(new[] { "src/IntentionalFixture.Part1.cs", "src/IntentionalFixture.Part2.cs" }));
    }

    [Test]
    public void ProductionPartialDeclarations_DoNotSpanMultipleSourceFiles()
    {
        string repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        var sourceIndex = new ArchitectureSourceFileFactIndex(
            [typeof(ArchitectureSourceFileFactIndex).Assembly],
            repositoryRoot,
            ["src"]);
        var declarations = sourceIndex.SourceDeclarations
            .Where(declaration => declaration.IsPartial)
            .GroupBy(declaration => (declaration.AssemblyName, declaration.FullTypeName))
            .Select(group => new
            {
                group.Key.AssemblyName,
                group.Key.FullTypeName,
                SourcePaths = group
                    .Select(declaration => declaration.SourceFilePath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(group => group.SourcePaths.Length > 1)
            .OrderBy(group => group.AssemblyName, StringComparer.Ordinal)
            .ThenBy(group => group.FullTypeName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(declarations, Is.Empty,
            "A production type may use a generated partial declaration, but must not span multiple source files: "
            + string.Join(", ", declarations.Select(group =>
                $"{group.AssemblyName}:{group.FullTypeName} ({string.Join(", ", group.SourcePaths)})")));
    }
}
