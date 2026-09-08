using System.Reflection;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSourceFileFactTraversalTests
{
    private const string TestAssemblyName = "ArchLinterNet.Core.Tests";
    private const string FixtureTypeName = "ArchLinterNet.Core.Tests.SourceFactFixtures.SingleTypeFixture";
    private const string FixtureSource =
        "namespace ArchLinterNet.Core.Tests.SourceFactFixtures { public sealed class SingleTypeFixture { } }";

    [Test]
    public void AllFacts_EnumerationEscapingConfiguredSourceRoot_DoesNotReadOrAttachEscapedFile()
    {
        string repositoryRoot = FakePaths.Root("/fake/repo");
        string sourceRoot = repositoryRoot + "/src";
        string containedFile = sourceRoot + "/SingleTypeFixture.cs";
        string escapedFile = repositoryRoot + "/outside/SingleTypeFixture.cs";
        var inner = new FakeArchitectureFileSystem();
        inner.AddDirectory(sourceRoot);
        inner.AddFile(containedFile, FixtureSource, DateTime.UtcNow);
        inner.AddFile(escapedFile, FixtureSource, DateTime.UtcNow);
        var fileSystem = new EscapingEnumerationFileSystem(inner, [containedFile, escapedFile]);
        var counters = new AnalysisSessionProfilingCounters();

        var index = new ArchitectureSourceFileFactIndex(
            [typeof(ArchitectureSourceFileFactTraversalTests).Assembly],
            repositoryRoot,
            ["src"],
            preprocessorSymbols: null,
            fileSystem,
            new ArchitectureSourceFileFactIndex.ProjectOwnership(
                ProjectDiscovery: null,
                SourceRootAssemblyOwnership: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["."] = TestAssemblyName
                }),
            new ArchitectureSourceFileFactIndex.ConstructionOptions(counters, CancellationToken.None));

        bool found = index.TryGetFact(FixtureTypeName, out ArchitectureDeclaredTypeFact fact);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(fact.SourceFilePath, Is.EqualTo("src/SingleTypeFixture.cs"));
            Assert.That(fileSystem.ReadAllTextCalls, Is.EqualTo(1));
            Assert.That(counters.SourceFilesScanned, Is.EqualTo(1));
            Assert.That(index.ConsumedSourceInputPaths, Is.EqualTo([Path.GetFullPath(containedFile)]));
        });
    }

    [Test]
    public void AllFacts_EnumerationEscapingWindowsVolume_DoesNotReadOrAttachEscapedFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("This regression exercises Path.GetRelativePath across Windows volumes.");
        }

        const string RepositoryRoot = @"C:\repo";
        string sourceRoot = Path.Combine(RepositoryRoot, "src");
        string containedFile = Path.Combine(sourceRoot, "SingleTypeFixture.cs");
        const string EscapedFile = @"D:\outside\SingleTypeFixture.cs";
        var inner = new FakeArchitectureFileSystem();
        inner.AddDirectory(sourceRoot);
        inner.AddFile(containedFile, FixtureSource, DateTime.UtcNow);
        inner.AddFile(EscapedFile, FixtureSource, DateTime.UtcNow);
        var fileSystem = new EscapingEnumerationFileSystem(inner, [containedFile, EscapedFile]);
        var counters = new AnalysisSessionProfilingCounters();

        var index = new ArchitectureSourceFileFactIndex(
            [typeof(ArchitectureSourceFileFactTraversalTests).Assembly],
            RepositoryRoot,
            ["src"],
            preprocessorSymbols: null,
            fileSystem,
            new ArchitectureSourceFileFactIndex.ProjectOwnership(
                ProjectDiscovery: null,
                SourceRootAssemblyOwnership: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["."] = TestAssemblyName
                }),
            new ArchitectureSourceFileFactIndex.ConstructionOptions(counters, CancellationToken.None));

        bool found = index.TryGetFact(FixtureTypeName, out ArchitectureDeclaredTypeFact fact);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(fact.SourceFilePath, Is.EqualTo("src/SingleTypeFixture.cs"));
            Assert.That(fileSystem.ReadAllTextCalls, Is.EqualTo(1));
            Assert.That(counters.SourceFilesScanned, Is.EqualTo(1));
            Assert.That(index.ConsumedSourceInputPaths, Is.EqualTo([Path.GetFullPath(containedFile)]));
        });
    }

    private sealed class EscapingEnumerationFileSystem(
        IArchitectureFileSystem inner,
        IReadOnlyList<string> enumeratedFiles) : IArchitectureFileSystem
    {
        public int ReadAllTextCalls { get; private set; }

        public bool FileExists(string path) => inner.FileExists(path);

        public string ReadAllText(string path)
        {
            ReadAllTextCalls++;
            return inner.ReadAllText(path);
        }

        public IEnumerable<string> ReadLines(string path) => inner.ReadLines(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            foreach (string enumeratedFile in enumeratedFiles)
            {
                yield return enumeratedFile;
            }
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
            inner.EnumerateDirectories(path, searchPattern, searchOption);

        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

        public string GetCurrentDirectory() => inner.GetCurrentDirectory();
    }
}
