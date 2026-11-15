using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSourceScannerExecutableBodyTraversalTests
{
    private static readonly string[] _forbiddenCalls = ["System.Console.WriteLine"];
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-source-traversal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repoRoot, "src"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }

    [TestCase(1, 28, 14, 8)]
    [TestCase(10, 550, 176, 26)]
    [TestCase(50, 10710, 2856, 106)]
    public void FindForbiddenUsagesInBodies_NestedLocalFunctionsVisitsAndQueriesEachNodeOnce(
        int depth,
        int legacyBodyNodeVisits,
        int legacySemanticLookupCount,
        int uniqueSemanticLookupCount)
    {
        string source = CreateNestedLocalFunctionSource(depth);
        (CSharpCompilation compilation, SyntaxTree syntaxTree) = Compile(source);
        CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
        int visitedNodes = 0;
        int semanticLookups = 0;

        List<ArchitectureSourceScanner.ForbiddenCallMatch> matches =
            ArchitectureSourceScanner.FindForbiddenUsagesInBodies(
                semanticModel,
                root,
                ArchitectureForbiddenCallMatcher.NormalizePatterns(_forbiddenCalls),
                new Dictionary<string, bool>(StringComparer.Ordinal),
                onSyntaxNodeVisited: () => visitedNodes++,
                onSemanticLookup: () => semanticLookups++);

        int totalSyntaxNodes = root.DescendantNodesAndSelf().Count();
        int legacyBodyVisits = CountLegacyBodyDescendantVisits(root);
        int legacySemanticLookups = CountLegacySemanticLookups(root);
        MethodDeclarationSyntax method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        int uniqueBodySemanticLookups = method.Body!.DescendantNodes().Count(IsSemanticLookupCandidate);

        TestContext.Out.WriteLine(
            $"depth={depth}; syntaxNodes={totalSyntaxNodes}; visitedNodes={visitedNodes}; semanticLookups={semanticLookups}; " +
            $"legacyBodyVisits={legacyBodyVisits}; legacySemanticLookups={legacySemanticLookups}; matches={matches.Count}");

        Assert.Multiple(() =>
        {
            Assert.That(legacyBodyVisits, Is.EqualTo(legacyBodyNodeVisits));
            Assert.That(legacySemanticLookups, Is.EqualTo(legacySemanticLookupCount));
            Assert.That(visitedNodes, Is.LessThanOrEqualTo(totalSyntaxNodes),
                "the context-aware visitor must not revisit syntax nodes as local-function depth grows");
            Assert.That(visitedNodes, Is.LessThan(legacyBodyVisits));
            Assert.That(semanticLookups, Is.EqualTo(uniqueSemanticLookupCount));
            Assert.That(semanticLookups, Is.EqualTo(uniqueBodySemanticLookups),
                "every syntax node that was eligible for a semantic lookup in the original relevant node set is queried once");
            Assert.That(semanticLookups, Is.LessThan(legacySemanticLookups));
            Assert.That(matches, Has.Count.EqualTo(3 * (depth + 1)),
                "the existing per-region match/candidate order and multiplicity are retained after each symbol is resolved once");
        });
    }

    [TestCase(1, 6)]
    [TestCase(10, 33)]
    [TestCase(50, 153)]
    public void FindMethodBodyViolations_NestedLocalFunctionsPreserveFindingsIdentityAndSuppression(
        int depth,
        int expectedBaselineCandidateCount)
    {
        string sourcePath = WriteSource(CreateNestedLocalFunctionSource(depth));
        List<ArchitectureBaselineCandidate> baselineCandidates = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(
            baselineCandidates: baselineCandidates,
            enableUnmatchedIgnoreTracking: true);

        List<ArchitectureViolation> violations = Scan(executionContext);

        ArchitectureViolationIdentity[] identities = baselineCandidates
            .Select(candidate => candidate.Identity!)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].SourceType, Is.EqualTo("src/Caller.cs"));
            Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("method-body"));
            Assert.That(violations[0].ForbiddenReferences, Has.Count.EqualTo(1));
            Assert.That(violations[0].ForbiddenReferences.Single(), Does.Contain("System.Console.WriteLine"));
            Assert.That(violations[0].ForbiddenReferences.Single(), Does.StartWith("line "));
            Assert.That(baselineCandidates, Has.Count.EqualTo(expectedBaselineCandidateCount));
            Assert.That(identities.Select(identity => identity.Occurrence),
                Is.EqualTo(Enumerable.Range(0, expectedBaselineCandidateCount)));
            Assert.That(identities, Has.All.Matches<ArchitectureViolationIdentity>(
                identity => !string.IsNullOrWhiteSpace(identity.SourceMember)
                            && identity.SourceMember.Contains("Local", StringComparison.Ordinal)));
            Assert.That(File.Exists(sourcePath), Is.True);
        });

        ArchitectureIgnoredViolation[] allOccurrencesIgnored = baselineCandidates
            .Select(ToIgnoredViolation)
            .ToArray();
        List<ArchitectureViolation> partiallySuppressed = Scan(
            CreateExecutionContext(ignoredViolations: [allOccurrencesIgnored[0]]));
        List<ArchitectureViolation> fullySuppressed = Scan(
            CreateExecutionContext(ignoredViolations: allOccurrencesIgnored));

        Assert.Multiple(() =>
        {
            Assert.That(partiallySuppressed, Has.Count.EqualTo(1),
                "the remaining legacy identity occurrences continue to keep a finding visible");
            Assert.That(fullySuppressed, Is.Empty,
                "exact structured suppressions still match every original body-region occurrence");
        });
    }

    [Test]
    public void FindMethodBodyViolations_PreservesConstructorAccessorLambdaExpressionAndAsyncFindings()
    {
        string source = """
            using System;
            using System.Threading.Tasks;
            namespace TraversalFixture;
            public sealed class Caller
            {
                public Caller()
                {
                    Console.WriteLine("constructor");
                }

                public void Nested()
                {
                    void Local()
                    {
                        Console.WriteLine("local");
                    }

                    void LocalExpression() => Console.WriteLine("local expression");

                    Local();
                    LocalExpression();
                    Action callback = () => Console.WriteLine("lambda");
                }

                public int Value
                {
                    get
                    {
                        Console.WriteLine("getter");
                        return 1;
                    }
                    set
                    {
                        Console.WriteLine("setter");
                    }
                }

                public void Expression() => Console.WriteLine("expression");

                public async Task Async()
                {
                    await Task.Yield();
                    Console.WriteLine("async");
                }
            }
            """;
        WriteSource(source);
        List<ArchitectureBaselineCandidate> candidates = new();

        List<ArchitectureViolation> violations = Scan(CreateExecutionContext(
            baselineCandidates: candidates,
            enableUnmatchedIgnoreTracking: true));

        string[] sourceMembers = candidates
            .Select(candidate => candidate.Identity?.SourceMember)
            .Where(member => member != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].ForbiddenReferences, Has.Count.EqualTo(8));
            Assert.That(violations[0].ForbiddenReferences, Has.All.Contains("System.Console.WriteLine"));
            Assert.That(sourceMembers, Has.Some.Contains("Caller"));
            Assert.That(sourceMembers, Has.Some.Contains("Local"));
            Assert.That(sourceMembers, Has.Some.Contains("LocalExpression"));
            Assert.That(sourceMembers, Has.Some.Contains("lambda expression"));
            Assert.That(sourceMembers, Has.Some.Contains("Value"));
            Assert.That(sourceMembers, Has.Some.Contains("Expression"));
            Assert.That(sourceMembers, Has.Some.Contains("Async"));
        });
    }

    [Test]
    public void FindMethodBodyViolations_AmbiguousSymbolUsesTheFirstCandidateFallback()
    {
        const string Source = """
            namespace TraversalFixture;
            public interface ILeft { }
            public interface IRight { }
            public sealed class Both : ILeft, IRight { }
            public sealed class Caller
            {
                private static void Choose(ILeft value) { }
                private static void Choose(IRight value) { }
                public void Run() => Choose(new Both());
            }
            """;
        WriteSource(Source);

        List<ArchitectureViolation> violations = Scan(
            CreateExecutionContext(),
            forbiddenCalls: ["Choose"]);

        Assert.That(violations.SelectMany(violation => violation.ForbiddenReferences),
            Has.Some.Contains("[ambiguous-candidate]"));
    }

    [Test]
    public void FindForbiddenUsagesInBodies_ExpressionBodyRootRemainsExcluded()
    {
        const string Source = """
            namespace TraversalFixture;
            public sealed class Caller
            {
                public void Run() => System.Console.WriteLine("expression root");
            }
            """;
        (CSharpCompilation compilation, SyntaxTree syntaxTree) = Compile(Source);
        CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
        ExpressionSyntax expressionRoot = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single().ExpressionBody!.Expression;
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
        int semanticLookups = 0;

        List<ArchitectureSourceScanner.ForbiddenCallMatch> matches =
            ArchitectureSourceScanner.FindForbiddenUsagesInBodies(
                semanticModel,
                root,
                ArchitectureForbiddenCallMatcher.NormalizePatterns(_forbiddenCalls),
                new Dictionary<string, bool>(StringComparer.Ordinal),
                onSemanticLookup: () => semanticLookups++);

        int descendantLookupCandidates = expressionRoot.DescendantNodes().Count(IsSemanticLookupCandidate);
        int descendantAndSelfLookupCandidates = expressionRoot.DescendantNodesAndSelf()
            .Count(IsSemanticLookupCandidate);

        Assert.Multiple(() =>
        {
            Assert.That(semanticLookups, Is.EqualTo(descendantLookupCandidates));
            Assert.That(semanticLookups, Is.LessThan(descendantAndSelfLookupCandidates));
            Assert.That(matches, Is.Not.Empty);
        });
    }

    private List<ArchitectureViolation> Scan(
        ArchitectureContractExecutionContext executionContext,
        IReadOnlyList<string>? forbiddenCalls = null)
    {
        return new ArchitectureSourceScanner().FindMethodBodyViolations(
            _repoRoot,
            "TraversalFixture",
            forbiddenCalls ?? _forbiddenCalls,
            executionContext,
            sourceRoots: ["src"]).ToList();
    }

    private string WriteSource(string source)
    {
        string sourcePath = Path.Combine(_repoRoot, "src", "Caller.cs");
        File.WriteAllText(sourcePath, source);
        return sourcePath;
    }

    private static ArchitectureContractExecutionContext CreateExecutionContext(
        IReadOnlyList<ArchitectureIgnoredViolation>? ignoredViolations = null,
        List<ArchitectureBaselineCandidate>? baselineCandidates = null,
        bool enableUnmatchedIgnoreTracking = false)
    {
        return new ArchitectureContractExecutionContext(
            "traversal-contract",
            "traversal-contract",
            ignoredViolations ?? Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking,
            "strict_method_body",
            baselineCandidates);
    }

    private static ArchitectureIgnoredViolation ToIgnoredViolation(ArchitectureBaselineCandidate candidate)
    {
        ArchitectureViolationIdentity identity = candidate.Identity!;
        return new ArchitectureIgnoredViolation
        {
            SourceType = candidate.SourceType,
            ForbiddenReference = candidate.ForbiddenReference,
            Reason = "test exact identity suppression",
            IdentityVersion = identity.IdentityVersion,
            ContractFamily = identity.ContractFamily,
            Kind = identity.Kind,
            SourceAssembly = identity.SourceAssembly,
            SourceMember = identity.SourceMember,
            TargetAssembly = identity.TargetAssembly,
            TargetType = identity.TargetType,
            TargetMember = identity.TargetMember,
            Occurrence = identity.Occurrence,
            Configuration = identity.Configuration,
        };
    }

    private static (CSharpCompilation Compilation, SyntaxTree SyntaxTree) Compile(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, path: "src/Caller.cs");
        MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "ArchitectureSourceScannerTraversalTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return (compilation, syntaxTree);
    }

    private static int CountLegacyBodyDescendantVisits(CompilationUnitSyntax root)
    {
        return GetLegacyBodyRoots(root).Sum(bodyRoot => bodyRoot.DescendantNodes().Count());
    }

    private static int CountLegacySemanticLookups(CompilationUnitSyntax root)
    {
        return GetLegacyBodyRoots(root).Sum(bodyRoot =>
            bodyRoot.DescendantNodes().Count(IsSemanticLookupCandidate));
    }

    private static IEnumerable<SyntaxNode> GetLegacyBodyRoots(CompilationUnitSyntax root)
    {
        foreach (BaseMethodDeclarationSyntax method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            if (method.Body != null)
            {
                yield return method.Body;
            }

            if (method.ExpressionBody != null)
            {
                yield return method.ExpressionBody.Expression;
            }
        }

        foreach (AccessorDeclarationSyntax accessor in root.DescendantNodes().OfType<AccessorDeclarationSyntax>())
        {
            if (accessor.Body != null)
            {
                yield return accessor.Body;
            }

            if (accessor.ExpressionBody != null)
            {
                yield return accessor.ExpressionBody.Expression;
            }
        }

        foreach (LocalFunctionStatementSyntax localFunction in root.DescendantNodes()
                     .OfType<LocalFunctionStatementSyntax>())
        {
            if (localFunction.Body != null)
            {
                yield return localFunction.Body;
            }

            if (localFunction.ExpressionBody != null)
            {
                yield return localFunction.ExpressionBody.Expression;
            }
        }
    }

    private static bool IsSemanticLookupCandidate(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax or MemberAccessExpressionSyntax or ObjectCreationExpressionSyntax or
            ImplicitObjectCreationExpressionSyntax or IdentifierNameSyntax;
    }

    private static string CreateNestedLocalFunctionSource(int depth)
    {
        System.Text.StringBuilder source = new();
        source.AppendLine("namespace TraversalFixture;");
        source.AppendLine("public sealed class Caller");
        source.AppendLine("{");
        source.AppendLine("    public void Run()");
        source.AppendLine("    {");
        for (int level = 1; level <= depth; level++)
        {
            source.Append("        void Local").Append(level).AppendLine("()");
            source.AppendLine("        {");
        }

        source.AppendLine("            System.Console.WriteLine(\"blocked\");");
        for (int level = depth; level >= 1; level--)
        {
            source.AppendLine("        }");
            if (level > 1)
            {
                source.Append("            Local").Append(level).AppendLine("();");
            }
        }

        source.AppendLine("        Local1();");
        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }
}
