using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #375 follow-up: LoadDocument accepted a CancellationToken but only checked it before and
// after the whole policyDocumentLoader.Load call — the recursive import/schema loader itself
// never observed it, so cancellation during a large import graph ran the entire graph to
// completion before the next check. These tests prove the token now reaches the import traversal
// itself and stops it mid-graph.
[TestFixture]
public sealed class ArchitecturePolicyImportCancellationTests
{
    [Test]
    public void Load_CancelledWhileReadingAnImport_StopsBeforeItsOwnNestedImportIsRead()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-import-cancel-{Guid.NewGuid():N}", "architecture");
        Directory.CreateDirectory(directory);
        string rootPath = Path.Combine(directory, "root.yml");
        string aPath = Path.Combine(directory, "a.yml");
        string bPath = Path.Combine(directory, "b.yml");

        File.WriteAllText(
            rootPath,
            "version: 1\nname: Root\nimports: [a.yml]\nanalysis:\n  target_assemblies: [App]\ncontracts:\n  strict: []\n");
        // b.yml is only reachable by first reading and visiting a.yml's own import list.
        File.WriteAllText(aPath, "imports: [b.yml]\nlayers:\n  domain:\n    namespace: App.Domain\n");
        File.WriteAllText(bPath, "layers:\n  infra:\n    namespace: App.Infra\n");

        try
        {
            using CancellationTokenSource cts = new();
            var fileSystem = new CancelOnReadFileSystem(aPath, cts);

            Assert.Throws<OperationCanceledException>(() =>
                new ArchitecturePolicyDocumentLoader(fileSystem).Load(rootPath, cts.Token));

            Assert.That(
                fileSystem.ReadPaths.Any(path => string.Equals(path, Path.GetFullPath(bPath), StringComparison.OrdinalIgnoreCase)),
                Is.False,
                "cancellation observed while reading a.yml must stop before a.yml's own imports (b.yml) are visited");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }

    // PR #416 review round 2: the per-document check at the top of Visit() only covers a nested
    // import's own subtree — it does not stop a SIBLING import from being resolved, read, and
    // parsed. a.yml here has no imports of its own, so Visit(a) returns normally without ever
    // observing the mid-read cancellation; the loop back in the parent must catch it before
    // moving on to b.yml.
    [Test]
    public void Load_CancelledWhileReadingASiblingImport_StopsBeforeTheNextSiblingIsRead()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-import-cancel-{Guid.NewGuid():N}", "architecture");
        Directory.CreateDirectory(directory);
        string rootPath = Path.Combine(directory, "root.yml");
        string aPath = Path.Combine(directory, "a.yml");
        string bPath = Path.Combine(directory, "b.yml");

        File.WriteAllText(
            rootPath,
            "version: 1\nname: Root\nimports: [a.yml, b.yml]\nanalysis:\n  target_assemblies: [App]\ncontracts:\n  strict: []\n");
        // a.yml has no nested imports, so Visit(a) completes and returns to the parent's loop
        // without ever re-observing a cancellation signal raised while a.yml was being read.
        File.WriteAllText(aPath, "layers:\n  domain:\n    namespace: App.Domain\n");
        File.WriteAllText(bPath, "layers:\n  infra:\n    namespace: App.Infra\n");

        try
        {
            using CancellationTokenSource cts = new();
            var fileSystem = new CancelOnReadFileSystem(aPath, cts);

            Assert.Throws<OperationCanceledException>(() =>
                new ArchitecturePolicyDocumentLoader(fileSystem).Load(rootPath, cts.Token));

            Assert.That(
                fileSystem.ReadPaths.Any(path => string.Equals(path, Path.GetFullPath(bPath), StringComparison.OrdinalIgnoreCase)),
                Is.False,
                "cancellation observed while reading sibling a.yml must stop before sibling b.yml is resolved/read/parsed");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }

    [Test]
    public void Load_TokenNotCancelled_ReadsEveryImportAsBefore()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-import-cancel-{Guid.NewGuid():N}", "architecture");
        Directory.CreateDirectory(directory);
        string rootPath = Path.Combine(directory, "root.yml");
        string aPath = Path.Combine(directory, "a.yml");
        string bPath = Path.Combine(directory, "b.yml");

        File.WriteAllText(
            rootPath,
            "version: 1\nname: Root\nimports: [a.yml]\nanalysis:\n  target_assemblies: [App]\ncontracts:\n  strict: []\n");
        File.WriteAllText(aPath, "imports: [b.yml]\nlayers:\n  domain:\n    namespace: App.Domain\n");
        File.WriteAllText(bPath, "layers:\n  infra:\n    namespace: App.Infra\n");

        try
        {
            var document = new ArchitecturePolicyDocumentLoader(ArchitectureFileSystem.Real)
                .Load(rootPath, CancellationToken.None);

            Assert.That(document.Layers.Keys, Is.EquivalentTo(new[] { "domain", "infra" }));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }

    private sealed class CancelOnReadFileSystem : IArchitectureFileSystem
    {
        private readonly string _cancelAfterReadingPath;
        private readonly CancellationTokenSource _cts;

        public CancelOnReadFileSystem(string cancelAfterReadingPath, CancellationTokenSource cts)
        {
            _cancelAfterReadingPath = Path.GetFullPath(cancelAfterReadingPath);
            _cts = cts;
        }

        public List<string> ReadPaths { get; } = new();

        public bool FileExists(string path) => ArchitectureFileSystem.Real.FileExists(path);

        public string ReadAllText(string path)
        {
            string full = Path.GetFullPath(path);
            ReadPaths.Add(full);
            string content = ArchitectureFileSystem.Real.ReadAllText(path);
            if (string.Equals(full, _cancelAfterReadingPath, StringComparison.OrdinalIgnoreCase))
            {
                _cts.Cancel();
            }

            return content;
        }

        public IEnumerable<string> ReadLines(string path) => ArchitectureFileSystem.Real.ReadLines(path);

        public bool DirectoryExists(string path) => ArchitectureFileSystem.Real.DirectoryExists(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
            ArchitectureFileSystem.Real.EnumerateFiles(path, searchPattern, searchOption);

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
            ArchitectureFileSystem.Real.EnumerateDirectories(path, searchPattern, searchOption);

        public DateTime GetLastWriteTimeUtc(string path) => ArchitectureFileSystem.Real.GetLastWriteTimeUtc(path);

        public string GetCurrentDirectory() => ArchitectureFileSystem.Real.GetCurrentDirectory();
    }
}
