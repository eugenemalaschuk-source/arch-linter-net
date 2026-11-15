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
public sealed class ArchitectureSourceScannerExecutableBodyTraversalParityTests
{
    private const string ForbiddenCall = "System.Console.WriteLine";
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-traversal-parity-{Guid.NewGuid():N}");
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

    [TestCase(1)]
    [TestCase(10)]
    [TestCase(50)]
    public void FindMethodBodyViolations_NestedLocalFunctionsMatchLegacyOrderAndIdentities(int depth)
    {
        string source = CreateNestedLocalFunctionSource(depth);
        (CSharpCompilation compilation, SyntaxTree syntaxTree) = Compile(source);
        CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
        IReadOnlyList<ForbiddenCallPattern> patterns =
            ArchitectureForbiddenCallMatcher.NormalizePatterns([ForbiddenCall]);

        List<ArchitectureSourceScanner.ForbiddenCallMatch> legacyMatches = FindLegacyForbiddenUsagesInBodies(
            semanticModel, root, patterns, new Dictionary<string, bool>(StringComparer.Ordinal));
        List<ArchitectureSourceScanner.ForbiddenCallMatch> currentMatches =
            ArchitectureSourceScanner.FindForbiddenUsagesInBodies(
                semanticModel, root, patterns, new Dictionary<string, bool>(StringComparer.Ordinal));

        List<ArchitectureBaselineCandidate> legacyCandidates = CollectLegacyCandidates(legacyMatches);
        ArchitectureViolationIdentity[] legacyIdentities = legacyCandidates
            .Select(candidate => candidate.Identity!)
            .ToArray();
        string[] expectedReferences = legacyMatches
            .Select(match => match.Display)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(display => display, StringComparer.Ordinal)
            .ToArray();

        string sourcePath = WriteSource(source);
        List<ArchitectureBaselineCandidate> currentCandidates = new();
        List<ArchitectureViolation> currentViolations = Scan(
            CreateExecutionContext(baselineCandidates: currentCandidates, enableUnmatchedIgnoreTracking: true));
        ArchitectureViolationIdentity[] currentIdentities = currentCandidates
            .Select(candidate => candidate.Identity!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(currentMatches, Is.EqualTo(legacyMatches),
                "the full ordered match records must retain display, source attribution, and target identity");
            Assert.That(currentIdentities, Is.EqualTo(legacyIdentities),
                "the full structured baseline identities and their occurrence order must match the old traversal");
            Assert.That(currentViolations, Has.Count.EqualTo(1));
            Assert.That(currentViolations[0].ForbiddenReferences, Is.EqualTo(expectedReferences));
            Assert.That(File.Exists(sourcePath), Is.True);
        });

        ArchitectureIgnoredViolation[] oneOccurrenceIgnored = [ToIgnoredViolation(legacyCandidates[0])];
        ArchitectureIgnoredViolation[] allOccurrencesIgnored = legacyCandidates
            .Select(ToIgnoredViolation)
            .ToArray();
        string[] partiallySuppressedExpected = GetLegacyVisibleReferences(legacyMatches, oneOccurrenceIgnored);
        string[] fullySuppressedExpected = GetLegacyVisibleReferences(legacyMatches, allOccurrencesIgnored);
        List<ArchitectureViolation> partiallySuppressed = Scan(
            CreateExecutionContext(ignoredViolations: oneOccurrenceIgnored));
        List<ArchitectureViolation> fullySuppressed = Scan(
            CreateExecutionContext(ignoredViolations: allOccurrencesIgnored));
        string[] partiallySuppressedActual = partiallySuppressed
            .SelectMany(violation => violation.ForbiddenReferences)
            .ToArray();
        string[] fullySuppressedActual = fullySuppressed
            .SelectMany(violation => violation.ForbiddenReferences)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(partiallySuppressedActual, Is.EqualTo(partiallySuppressedExpected),
                "suppressing one structured occurrence must leave exactly the same legacy findings visible");
            Assert.That(fullySuppressedActual, Is.EqualTo(fullySuppressedExpected),
                "exact suppression of every legacy occurrence must produce the same empty result");
        });
    }

    [Test]
    public void FindMethodBodyViolations_MixedForbiddenCallsMatchLegacyOrderAndSelectiveSuppression()
    {
        const string Source = """
            namespace TraversalFixture;
            public sealed class Caller
            {
                public void First()
                {
                    System.Console.WriteLine("first");
                    void Local()
                    {
                        System.Console.ReadLine();
                        void Nested() => System.Console.WriteLine("nested");
                        Nested();
                    }

                    Local();
                }

                public int Value
                {
                    get
                    {
                        System.Console.ReadLine();
                        return 1;
                    }
                    set
                    {
                        System.Console.WriteLine(value.ToString());
                    }
                }

                public void Second()
                {
                    System.Console.ReadLine();
                    System.Console.WriteLine("second");
                }
            }
            """;
        IReadOnlyList<string> forbiddenCalls = [
            "System.Console.WriteLine",
            "System.Console.ReadLine",
        ];
        (CSharpCompilation compilation, SyntaxTree syntaxTree) = Compile(Source);
        CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
        IReadOnlyList<ForbiddenCallPattern> patterns =
            ArchitectureForbiddenCallMatcher.NormalizePatterns(forbiddenCalls);
        List<ArchitectureSourceScanner.ForbiddenCallMatch> legacyMatches = FindLegacyForbiddenUsagesInBodies(
            semanticModel, root, patterns, new Dictionary<string, bool>(StringComparer.Ordinal));
        List<ArchitectureSourceScanner.ForbiddenCallMatch> currentMatches =
            ArchitectureSourceScanner.FindForbiddenUsagesInBodies(
                semanticModel, root, patterns, new Dictionary<string, bool>(StringComparer.Ordinal));
        List<ArchitectureBaselineCandidate> legacyCandidates = CollectLegacyCandidates(legacyMatches);
        ArchitectureViolationIdentity[] legacyIdentities = legacyCandidates
            .Select(candidate => candidate.Identity!)
            .ToArray();
        string[] expectedReferences = legacyMatches
            .Select(match => match.Display)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(display => display, StringComparer.Ordinal)
            .ToArray();
        string[] sourceMembers = legacyMatches
            .Select(match => match.SourceMember)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] targetMembers = legacyMatches
            .Select(match => match.TargetMember)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        WriteSource(Source);
        List<ArchitectureBaselineCandidate> currentCandidates = new();
        List<ArchitectureViolation> currentViolations = Scan(
            CreateExecutionContext(baselineCandidates: currentCandidates, enableUnmatchedIgnoreTracking: true),
            forbiddenCalls);
        ArchitectureViolationIdentity[] currentIdentities = currentCandidates
            .Select(candidate => candidate.Identity!)
            .ToArray();

        ArchitectureBaselineCandidate[] secondReadLineCandidates = legacyCandidates
            .Where(candidate => candidate.Identity is { } identity
                                && identity.SourceMember?.Contains("Second", StringComparison.Ordinal) == true
                                && identity.TargetMember?.Contains("ReadLine", StringComparison.Ordinal) == true)
            .ToArray();
        ArchitectureIgnoredViolation[] secondReadLineIgnored = secondReadLineCandidates
            .Select(ToIgnoredViolation)
            .ToArray();
        string secondReadLineDisplay = legacyMatches
            .First(match => match.SourceMember!.Contains("Second", StringComparison.Ordinal)
                            && match.TargetMember.Contains("ReadLine", StringComparison.Ordinal))
            .Display;
        string[] selectivelySuppressedExpected = GetLegacyVisibleReferences(legacyMatches, secondReadLineIgnored);
        List<ArchitectureViolation> selectivelySuppressed = Scan(
            CreateExecutionContext(ignoredViolations: secondReadLineIgnored), forbiddenCalls);
        string[] selectivelySuppressedActual = selectivelySuppressed
            .SelectMany(violation => violation.ForbiddenReferences)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(currentMatches, Is.EqualTo(legacyMatches),
                "mixed calls across methods, accessors, and local functions must preserve the full ordered records");
            Assert.That(currentIdentities, Is.EqualTo(legacyIdentities),
                "all structured identities and occurrence order must match the old traversal");
            Assert.That(currentViolations, Has.Count.EqualTo(1));
            Assert.That(currentViolations[0].ForbiddenReferences, Is.EqualTo(expectedReferences));
            Assert.That(targetMembers, Has.Some.Contains("WriteLine"));
            Assert.That(targetMembers, Has.Some.Contains("ReadLine"));
            Assert.That(sourceMembers, Has.Some.Contains("First"));
            Assert.That(sourceMembers, Has.Some.Contains("Second"));
            Assert.That(sourceMembers, Has.Some.Contains("Local"));
            Assert.That(sourceMembers, Has.Some.Contains("Value"));
            Assert.That(secondReadLineCandidates, Is.Not.Empty,
                "the selected suppression must target the ReadLine call from the second method");
            Assert.That(secondReadLineIgnored, Is.Not.Empty);
            Assert.That(selectivelySuppressedActual, Is.EqualTo(selectivelySuppressedExpected));
            Assert.That(selectivelySuppressedActual, Does.Not.Contain(secondReadLineDisplay));
        });
    }

    private List<ArchitectureViolation> Scan(
        ArchitectureContractExecutionContext executionContext,
        IReadOnlyList<string>? forbiddenCalls = null)
    {
        return new ArchitectureSourceScanner().FindMethodBodyViolations(
            _repoRoot,
            "TraversalFixture",
            forbiddenCalls ?? [ForbiddenCall],
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

    private static List<ArchitectureBaselineCandidate> CollectLegacyCandidates(
        IReadOnlyList<ArchitectureSourceScanner.ForbiddenCallMatch> matches,
        IReadOnlyList<ArchitectureIgnoredViolation>? ignoredViolations = null)
    {
        List<ArchitectureBaselineCandidate> candidates = new();
        ArchitectureContractExecutionContext context = CreateExecutionContext(
            ignoredViolations, candidates, enableUnmatchedIgnoreTracking: true);
        foreach (ArchitectureSourceScanner.ForbiddenCallMatch match in matches)
        {
            context.IsIgnored(
                "src/Caller.cs",
                match.Display,
                targetAssembly: match.TargetAssembly,
                targetType: match.TargetType,
                sourceMember: match.SourceMember,
                targetMember: match.TargetMember);
        }

        return candidates;
    }

    private static string[] GetLegacyVisibleReferences(
        IReadOnlyList<ArchitectureSourceScanner.ForbiddenCallMatch> matches,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations)
    {
        ArchitectureContractExecutionContext context = CreateExecutionContext(ignoredViolations);
        return matches
            .Where(match => !context.IsIgnored(
                "src/Caller.cs",
                match.Display,
                targetAssembly: match.TargetAssembly,
                targetType: match.TargetType,
                sourceMember: match.SourceMember,
                targetMember: match.TargetMember))
            .Select(match => match.Display)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(display => display, StringComparer.Ordinal)
            .ToArray();
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
            "ArchitectureSourceScannerTraversalParityTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return (compilation, syntaxTree);
    }

    private static List<ArchitectureSourceScanner.ForbiddenCallMatch> FindLegacyForbiddenUsagesInBodies(
        SemanticModel semanticModel,
        CompilationUnitSyntax root,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        List<ArchitectureSourceScanner.ForbiddenCallMatch> matches = new();
        foreach (SyntaxNode bodyRoot in EnumerateLegacyBodyRoots(root))
        {
            foreach (SyntaxNode node in bodyRoot.DescendantNodes())
            {
                if (!ArchitectureSourceScanner.TryGetReferencedSymbol(
                        semanticModel, node, out ISymbol? symbol, out bool usedCandidateFallback)
                    || symbol == null)
                {
                    continue;
                }

                SymbolDescriptor descriptor = ArchitectureForbiddenCallMatcher.FromRoslynSymbol(symbol);
                if (!ArchitectureForbiddenCallMatcher.TryMatch(
                        descriptor, patterns, matchCache, out string matchedPattern))
                {
                    continue;
                }

                string symbolName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string fallbackMarker = usedCandidateFallback ? " [ambiguous-candidate]" : string.Empty;
                string display = $"line {line}: {matchedPattern} -> {symbolName}{fallbackMarker}";
                string? sourceMember = semanticModel.GetEnclosingSymbol(node.SpanStart)
                    ?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                matches.Add(new ArchitectureSourceScanner.ForbiddenCallMatch(
                    display,
                    symbol.ContainingAssembly?.Name,
                    symbol.ContainingType?.ToDisplayString(),
                    symbolName,
                    sourceMember));
            }
        }

        return matches;
    }

    private static IEnumerable<SyntaxNode> EnumerateLegacyBodyRoots(CompilationUnitSyntax root)
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

    private static string CreateNestedLocalFunctionSource(int depth)
    {
        var source = new System.Text.StringBuilder();
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
