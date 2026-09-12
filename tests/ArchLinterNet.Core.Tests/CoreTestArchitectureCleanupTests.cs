using System.Text.RegularExpressions;
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
        const string part = "namespace Fixture { public partial class IntentionalFixture { } }";
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory("/fake/src");
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part1.cs", part, DateTime.UtcNow);
        fileSystem.AddFile("/fake/src/IntentionalFixture.Part2.cs", part, DateTime.UtcNow);

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
        var declarations = new Regex(
                @"(?m)^\s*(?:(?:public|internal|private|protected|static|sealed|abstract)\s+)*partial\s+(?:class|record|struct|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)")
            .Matches(string.Join(
                Environment.NewLine,
                Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
                    .SelectMany(File.ReadLines)))
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.That(declarations, Is.Empty,
            "A production type may use a generated partial declaration, but must not span multiple source files: "
            + string.Join(", ", declarations));
    }
}
