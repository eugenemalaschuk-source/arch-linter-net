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
    private sealed class FakeRoslynCompilationFactory : IRoslynCompilationFactory
    {
        public bool WasCalled { get; private set; }

        public CSharpCompilation Create(
            string assemblyName,
            IReadOnlyList<string> sourceFilePaths,
            IReadOnlyList<string>? preprocessorSymbols,
            IArchitectureFileSystem fileSystem,
            IArchitectureAssemblyLoader assemblyLoader)
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
        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory("/fake/repo/src");
        fileSystem.AddFile(
            "/fake/repo/src/Widget.cs",
            "namespace Fake.Forbidden.Namespace;\nclass Widget { }\n",
            DateTime.UtcNow);

        var compilationFactory = new FakeRoslynCompilationFactory();

        var executionContext = new ArchitectureContractExecutionContext(
            "fake-contract", "fake-contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);

        List<ArchitectureViolation> violations = new ArchitectureSourceScanner().FindMethodBodyViolations(
            "/fake/repo",
            "Fake.Forbidden.Namespace",
            new[] { "System.Console.WriteLine" },
            executionContext,
            sourceRoots: new[] { "src" },
            fileSystem: fileSystem,
            compilationFactory: compilationFactory).ToList();

        Assert.That(compilationFactory.WasCalled, Is.True);
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences, Has.Some.Contains("System.Console.WriteLine"));
    }
}
