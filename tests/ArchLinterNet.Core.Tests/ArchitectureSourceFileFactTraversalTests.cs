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

    [Test]
    public void AllFacts_EnumerationEscapingConfiguredSourceRoot_DoesNotReadOrAttachEscapedFile()
    {
        string repositoryRoot = FakePaths.Root("/fake/repo");
        string sourceRoot = repositoryRoot + "/src";
        string escapedFile = repositoryRoot + "/outside/SingleTypeFixture.cs";
        var inner = new FakeArchitectureFileSystem();
        inner.AddDirectory(sourceRoot);
        inner.AddFile(
            escapedFile,
            "namespace ArchLinterNet.Core.Tests.SourceFactFixtures { public sealed class SingleTypeFixture { } }",
            DateTime.UtcNow);
        var fileSystem = new EscapingEnumerationFileSystem(inner, escapedFile);
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
                    ["src"] = TestAssemblyName
                }),
            new ArchitectureSourceFileFactIndex.ConstructionOptions(counters, CancellationToken.None));

        bool found = index.TryGetFact(FixtureTypeName, out ArchitectureDeclaredTypeFact fact);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(fact.SourceFilePath, Is.Null);
            Assert.That(fileSystem.ReadAllTextCalls, Is.Zero);
            Assert.That(counters.SourceFilesScanned, Is.Zero);
            Assert.That(index.ConsumedSourceInputPaths, Is.Empty);
        });
    }

    private sealed class EscapingEnumerationFileSystem(
        IArchitectureFileSystem inner,
        string escapedFile) : IArchitectureFileSystem
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
            yield return escapedFile;
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
            inner.EnumerateDirectories(path, searchPattern, searchOption);

        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

        public string GetCurrentDirectory() => inner.GetCurrentDirectory();
    }
}
