using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSourceScannerFakeSeamTests
{
    private static readonly string[] _consoleWriteLine = ["System.Console.WriteLine"];
    private static readonly string[] _srcRoot = ["src"];

    private sealed class FakeRoslynCompilationFactory : IRoslynCompilationFactory
    {
        public bool WasCalled { get; private set; }

        public CSharpCompilation Create(
            string assemblyName,
            IReadOnlyList<string> sourceFilePaths,
            IReadOnlyList<string>? preprocessorSymbols,
            IArchitectureFileSystem fileSystem,
            IArchitectureAssemblyLoader assemblyLoader,
            IReadOnlyList<string>? explicitReferenceAssemblyPaths = null)
        {
            WasCalled = true;

            var syntaxTree = CSharpSyntaxTree.ParseText(
                """
                namespace Fake.Forbidden.Namespace
                {
                    public class Widget
                    {
                        public void Run()
                        {
                            System.Console.WriteLine("forbidden call");
                        }
                    }
                }
                """,
                path: "/fake/repo/src/Widget.cs");

            var references = new[]
            {
                (Microsoft.CodeAnalysis.MetadataReference)Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                    typeof(object).Assembly.Location),
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                    typeof(Console).Assembly.Location),
            };

            return CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
    }

    [Test]
    public void FindMethodBodyViolations_FakeCompilationFactory_UsesFakeCompilationInsteadOfRealRoslynPipeline()
    {
        string repoRoot = FakePaths.Root("/fake/repo");

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory($"{repoRoot}/src");
        fileSystem.AddFile(
            $"{repoRoot}/src/Widget.cs",
            "namespace Fake.Forbidden.Namespace;\nclass Widget { }\n",
            DateTime.UtcNow);

        var compilationFactory = new FakeRoslynCompilationFactory();

        var executionContext = new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);

        List<ArchitectureViolation> violations = new ArchitectureSourceScanner().FindMethodBodyViolations(
            repoRoot,
            "Fake.Forbidden.Namespace",
            _consoleWriteLine,
            executionContext,
            sourceRoots: _srcRoot,
            fileSystem: fileSystem,
            compilationFactory: compilationFactory).ToList();

        Assert.That(compilationFactory.WasCalled, Is.True);
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences, Has.Some.Contains("System.Console.WriteLine"));
    }

    [Test]
    public void FindMethodBodyViolations_AllMatchesIgnored_ProducesNoViolations()
    {
        string repoRoot = FakePaths.Root("/fake/repo");

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory($"{repoRoot}/src");
        fileSystem.AddFile(
            $"{repoRoot}/src/Widget.cs",
            "namespace Fake.Forbidden.Namespace;\nclass Widget { }\n",
            DateTime.UtcNow);

        var compilationFactory = new FakeRoslynCompilationFactory();

        // A wildcard ignore matches the single forbidden call the fake compilation surfaces, so every
        // match is filtered out and the scanner takes the unignored.Length == 0 continue branch,
        // yielding no violation for the file even though a forbidden usage was found.
        var ignoredEverything = new[]
        {
            new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*", Reason = "test" },
        };
        var executionContext = new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", ignoredEverything,
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);

        List<ArchitectureViolation> violations = new ArchitectureSourceScanner().FindMethodBodyViolations(
            repoRoot,
            "Fake.Forbidden.Namespace",
            _consoleWriteLine,
            executionContext,
            sourceRoots: _srcRoot,
            fileSystem: fileSystem,
            compilationFactory: compilationFactory).ToList();

        Assert.That(compilationFactory.WasCalled, Is.True);
        Assert.That(violations, Is.Empty);
    }

    // PR #416 review round 3: FindMethodBodyViolations previously accepted no CancellationToken at
    // all, so the Roslyn compilation was always built and every syntax tree always fully analyzed
    // regardless of cancellation. Proves the token is observed before the (expensive, single-call)
    // compilation build even starts.
    [Test]
    public void FindMethodBodyViolations_PreCancelledToken_NeverBuildsCompilation()
    {
        string repoRoot = FakePaths.Root("/fake/repo");

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory($"{repoRoot}/src");
        fileSystem.AddFile(
            $"{repoRoot}/src/Widget.cs",
            "namespace Fake.Forbidden.Namespace;\nclass Widget { }\n",
            DateTime.UtcNow);

        var compilationFactory = new FakeRoslynCompilationFactory();
        var executionContext = new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new ArchitectureSourceScanner().FindMethodBodyViolations(
            repoRoot,
            "Fake.Forbidden.Namespace",
            _consoleWriteLine,
            executionContext,
            sourceRoots: _srcRoot,
            fileSystem: fileSystem,
            compilationFactory: compilationFactory,
            cancellationToken: cts.Token).ToList());

        Assert.That(compilationFactory.WasCalled, Is.False,
            "a pre-cancelled token must stop the scan before the Roslyn compilation is ever built");
    }

    // Proves file discovery itself is interruptible per file, not only the compilation/semantic
    // analysis pass that runs afterward: cancellation triggered while enumerating the first file
    // must stop before a second file is ever fetched from the file system.
    [Test]
    public void FindMatchingSourceFiles_CancelledDuringEnumeration_StopsBeforeTheNextFileIsFetched()
    {
        string repoRoot = FakePaths.Root("/fake/repo");

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory($"{repoRoot}/src");
        fileSystem.AddFile($"{repoRoot}/src/A.cs", "namespace Fake.Forbidden.Namespace;\nclass A { }\n", DateTime.UtcNow);
        fileSystem.AddFile($"{repoRoot}/src/B.cs", "namespace Fake.Forbidden.Namespace;\nclass B { }\n", DateTime.UtcNow);

        var compilationFactory = new FakeRoslynCompilationFactory();
        var executionContext = new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);
        using CancellationTokenSource cts = new();
        var cancelOnFirstFile = new CancelOnFirstEnumeratedFile(fileSystem, cts);

        Assert.Throws<OperationCanceledException>(() => new ArchitectureSourceScanner().FindMethodBodyViolations(
            repoRoot,
            "Fake.Forbidden.Namespace",
            _consoleWriteLine,
            executionContext,
            sourceRoots: _srcRoot,
            fileSystem: cancelOnFirstFile,
            compilationFactory: compilationFactory,
            cancellationToken: cts.Token).ToList());

        Assert.That(cancelOnFirstFile.FetchedCount, Is.EqualTo(1),
            "the second file must never be fetched once cancellation is observed while the first is being processed");
        Assert.That(compilationFactory.WasCalled, Is.False);
    }

    // Delegates every call to a real FakeArchitectureFileSystem except EnumerateFiles, which
    // cancels as a side effect of yielding its first result — lets a test observe cancellation
    // mid-file-enumeration without needing to modify the shared fake itself.
    private sealed class CancelOnFirstEnumeratedFile : IArchitectureFileSystem
    {
        private readonly FakeArchitectureFileSystem _inner;
        private readonly CancellationTokenSource _cts;

        public CancelOnFirstEnumeratedFile(FakeArchitectureFileSystem inner, CancellationTokenSource cts)
        {
            _inner = inner;
            _cts = cts;
        }

        public int FetchedCount { get; private set; }

        public bool FileExists(string path) => _inner.FileExists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public IEnumerable<string> ReadLines(string path) => _inner.ReadLines(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            foreach (string filePath in _inner.EnumerateFiles(path, searchPattern, searchOption))
            {
                FetchedCount++;
                if (FetchedCount == 1)
                {
                    _cts.Cancel();
                }

                yield return filePath;
            }
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
            _inner.EnumerateDirectories(path, searchPattern, searchOption);
        public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);
        public string GetCurrentDirectory() => _inner.GetCurrentDirectory();
    }
}
