using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
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
}
