using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSourceScannerGeneratedFileExclusionTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        // Deliberately lives directly under the OS temp directory, which itself contains a "Temp"
        // path segment (e.g. Windows %TEMP%) — regression coverage for that segment not being
        // mistaken for a Unity Temp/ build folder when it's an ancestor of the repo, not inside it.
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-gen-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repoRoot, "src"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, true);
        }
    }

    private sealed class RecordingCompilationFactory : IRoslynCompilationFactory
    {
        public IReadOnlyList<string>? SeenSourceFilePaths { get; private set; }

        public CSharpCompilation Create(
            string assemblyName,
            IReadOnlyList<string> sourceFilePaths,
            IReadOnlyList<string>? preprocessorSymbols,
            IArchitectureFileSystem fileSystem,
            IArchitectureAssemblyLoader assemblyLoader,
            IReadOnlyList<string>? explicitReferenceAssemblyPaths = null)
        {
            SeenSourceFilePaths = sourceFilePaths;
            return CSharpCompilation.Create(assemblyName);
        }
    }

    private static ArchitectureContractExecutionContext ExecutionContext()
    {
        return new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);
    }

    private List<string> Scan(RecordingCompilationFactory compilationFactory)
    {
        _ = new ArchitectureSourceScanner().FindMethodBodyViolations(
            _repoRoot,
            "Fake.Namespace",
            new[] { "System.Console.WriteLine" },
            ExecutionContext(),
            sourceRoots: new[] { "src" },
            compilationFactory: compilationFactory).ToList();

        return compilationFactory.SeenSourceFilePaths?.ToList() ?? new List<string>();
    }

    [Test]
    public void FindMethodBodyViolations_RepoRootUnderOsTempAncestor_StillScansOrdinaryFiles()
    {
        string sourceFile = Path.Combine(_repoRoot, "src", "Widget.cs");
        File.WriteAllText(sourceFile, "namespace Fake.Namespace;\nclass Widget { }\n");

        var compilationFactory = new RecordingCompilationFactory();

        List<string> seen = Scan(compilationFactory);

        Assert.That(seen, Has.Some.EqualTo(sourceFile));
    }

    [TestCase("bin", "Debug", "Widget.cs")]
    [TestCase("obj", "Debug", "Widget.cs")]
    public void FindMethodBodyViolations_BuildOutputDirectoryInsideSourceRoot_IsExcluded(
        string dir1, string dir2, string fileName)
    {
        string nestedDirectory = Path.Combine(_repoRoot, "src", dir1, dir2);
        Directory.CreateDirectory(nestedDirectory);
        string sourceFile = Path.Combine(nestedDirectory, fileName);
        File.WriteAllText(sourceFile, "namespace Fake.Namespace;\nclass Widget { }\n");

        var compilationFactory = new RecordingCompilationFactory();

        List<string> seen = Scan(compilationFactory);

        Assert.That(seen, Has.None.EqualTo(sourceFile));
    }

    [Test]
    public void FindMethodBodyViolations_GeneratedFilenameSuffixInsideSourceRoot_IsExcluded()
    {
        string sourceFile = Path.Combine(_repoRoot, "src", "Widget.g.cs");
        File.WriteAllText(sourceFile, "namespace Fake.Namespace;\nclass Widget { }\n");

        var compilationFactory = new RecordingCompilationFactory();

        List<string> seen = Scan(compilationFactory);

        Assert.That(seen, Has.None.EqualTo(sourceFile));
    }
}
