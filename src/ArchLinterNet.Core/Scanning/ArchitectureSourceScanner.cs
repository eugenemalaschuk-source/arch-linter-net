using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureSourceScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        string repositoryRoot,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        string[]? sourceRoots = null,
        ArchitectureLayer? sourceLayer = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null,
        IRoslynCompilationFactory? compilationFactory = null,
        IArchitectureAssemblyLoader? assemblyLoader = null,
        IReadOnlyList<string>? explicitReferenceAssemblyPaths = null);

    IReadOnlyList<string> FindMatchingSourceFiles(
        string repositoryRoot,
        ArchitectureLayer layer,
        string[]? sourceRoots = null,
        IArchitectureFileSystem? fileSystem = null);
}

internal sealed class ArchitectureSourceScanner : IArchitectureSourceScanner
{
    private static readonly string[] _defaultSourceRoots = ["src", "tests"];

    public IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        string repositoryRoot,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        string[]? sourceRoots = null,
        ArchitectureLayer? sourceLayer = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        IArchitectureFileSystem? fileSystem = null,
        IRoslynCompilationFactory? compilationFactory = null,
        IArchitectureAssemblyLoader? assemblyLoader = null,
        IReadOnlyList<string>? explicitReferenceAssemblyPaths = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        compilationFactory ??= RoslynCompilationFactory.Real;
        assemblyLoader ??= ArchitectureAssemblyLoader.Real;

        ArchitectureLayer effectiveLayer = sourceLayer
                                          ?? new ArchitectureLayer { Namespace = sourceNamespacePrefix };
        List<string> sourceFiles = FindMatchingSourceFiles(repositoryRoot, effectiveLayer, sourceRoots, fileSystem).ToList();
        if (sourceFiles.Count == 0)
        {
            return Array.Empty<ArchitectureViolation>();
        }

        CSharpCompilation compilation = compilationFactory.Create(
            "ArchitectureSourceScanner", sourceFiles, preprocessorSymbols, fileSystem, assemblyLoader,
            explicitReferenceAssemblyPaths);
        IReadOnlyList<ForbiddenCallPattern> patterns =
            ArchitectureForbiddenCallMatcher.NormalizePatterns(forbiddenCallPatterns);
        Dictionary<string, bool> matchCache = new(StringComparer.Ordinal);
        List<ArchitectureViolation> violations = new();

        foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, true);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

            if (!ContainsNamespace(root, effectiveLayer))
            {
                continue;
            }

            List<string> matches = FindForbiddenUsagesInBodies(semanticModel, root, patterns, matchCache);
            if (matches.Count == 0)
            {
                continue;
            }

            string relativePath = GetRelativePath(repositoryRoot, syntaxTree.FilePath);

            IReadOnlyList<string> unignored = matches
                .Where(match => !executionContext.IsIgnored(relativePath, match))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(match => match, StringComparer.Ordinal)
                .ToArray();

            if (unignored.Count == 0)
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                executionContext.ContractName,
                executionContext.ContractId,
                relativePath,
                "method-body",
                unignored));
        }

        return violations;
    }

    public IReadOnlyList<string> FindMatchingSourceFiles(
        string repositoryRoot,
        ArchitectureLayer layer,
        string[]? sourceRoots = null,
        IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        string[] roots = sourceRoots ?? _defaultSourceRoots;
        return FindSourceFilesForNamespace(repositoryRoot, layer, roots, fileSystem);
    }

    private static List<string> FindForbiddenUsagesInBodies(
        SemanticModel semanticModel,
        CompilationUnitSyntax root,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        List<string> matches = new();

        foreach (SyntaxNode bodyNode in EnumerateExecutableBodies(root))
        {
            foreach (SyntaxNode node in bodyNode.DescendantNodes())
            {
                if (!TryGetReferencedSymbol(semanticModel, node, out ISymbol? symbol, out bool usedCandidateFallback) ||
                    symbol == null)
                {
                    continue;
                }

                SymbolDescriptor descriptor = ArchitectureForbiddenCallMatcher.FromRoslynSymbol(symbol);
                if (!ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, matchCache,
                        out string matchedPattern))
                {
                    continue;
                }

                string symbolName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string fallbackMarker = usedCandidateFallback ? " [ambiguous-candidate]" : string.Empty;
                matches.Add($"line {line}: {matchedPattern} -> {symbolName}{fallbackMarker}");
            }
        }

        return matches;
    }

    private static IEnumerable<SyntaxNode> EnumerateExecutableBodies(CompilationUnitSyntax root)
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

    private static bool TryGetReferencedSymbol(
        SemanticModel semanticModel,
        SyntaxNode node,
        out ISymbol? symbol,
        out bool usedCandidateFallback)
    {
        usedCandidateFallback = false;

        symbol = node switch
        {
            InvocationExpressionSyntax invocation => GetResolvedSymbol(semanticModel.GetSymbolInfo(invocation),
                out usedCandidateFallback),
            MemberAccessExpressionSyntax memberAccess => GetResolvedSymbol(semanticModel.GetSymbolInfo(memberAccess),
                out usedCandidateFallback),
            ObjectCreationExpressionSyntax objectCreation => GetResolvedSymbol(
                semanticModel.GetSymbolInfo(objectCreation), out usedCandidateFallback),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreation => GetResolvedSymbol(
                semanticModel.GetSymbolInfo(implicitObjectCreation), out usedCandidateFallback),
            IdentifierNameSyntax identifierName => GetResolvedSymbol(semanticModel.GetSymbolInfo(identifierName),
                out usedCandidateFallback),
            _ => null
        };

        return symbol != null;
    }

    private static ISymbol? GetResolvedSymbol(SymbolInfo symbolInfo, out bool usedCandidateFallback)
    {
        usedCandidateFallback = false;

        if (symbolInfo.Symbol != null)
        {
            return symbolInfo.Symbol;
        }

        if (symbolInfo.CandidateSymbols.Length > 0)
        {
            usedCandidateFallback = true;
            return symbolInfo.CandidateSymbols[0];
        }

        return null;
    }

    private static List<string> FindSourceFilesForNamespace(
        string repositoryRoot, ArchitectureLayer layer, string[] sourceRoots, IArchitectureFileSystem fileSystem)
    {
        List<string> result = new();

        foreach (string root in sourceRoots)
        {
            string fullRoot = Path.Combine(repositoryRoot, root);

            if (!fileSystem.DirectoryExists(fullRoot))
            {
                continue;
            }

            foreach (string filePath in fileSystem.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
            {
                // Checked relative to the scanned root (not the absolute path) so an ancestor
                // directory name outside the repository — e.g. the OS temp directory a test
                // fixture or CI checkout happens to live under — can never be mistaken for a
                // generated/build-output segment inside the repository itself.
                string relativeToRoot = Path.GetRelativePath(fullRoot, filePath);
                if (!ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot) && FileContainsNamespace(filePath, layer, fileSystem))
                {
                    result.Add(filePath);
                }
            }
        }

        return result;
    }

    private static bool FileContainsNamespace(string filePath, ArchitectureLayer layer, IArchitectureFileSystem fileSystem)
    {
        try
        {
            foreach (string line in fileSystem.ReadLines(filePath))
            {
                string trimmed = line.Trim();

                if (!trimmed.StartsWith("namespace ", StringComparison.Ordinal))
                {
                    continue;
                }

                string declared = trimmed[10..].TrimEnd('{', ' ', '\t', ';');
                if (ArchitectureLayerResolver.MatchesNamespace(layer, declared))
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }

        return false;
    }

    private static bool ContainsNamespace(CompilationUnitSyntax root, ArchitectureLayer layer)
    {
        IEnumerable<string> namespaces = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString());

        return namespaces.Any(ns => ArchitectureLayerResolver.MatchesNamespace(layer, ns));
    }

    private static string GetRelativePath(string root, string fullPath)
    {
        try
        {
            return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        }
        catch (Exception)
        {
            return fullPath;
        }
    }
}
