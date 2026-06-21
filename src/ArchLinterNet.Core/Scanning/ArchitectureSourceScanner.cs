using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureSourceScanner
{
    private static readonly string[] _defaultSourceRoots = ["src", "tests"];

    public static IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        string contractName,
        string? contractId,
        string repositoryRoot,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        string[]? sourceRoots = null,
        ArchitectureLayer? sourceLayer = null,
        ArchitectureIgnoreUsageTracker? usageTracker = null,
        IReadOnlyList<string>? preprocessorSymbols = null)
    {
        string[] roots = sourceRoots ?? _defaultSourceRoots;
        ArchitectureLayer effectiveLayer = sourceLayer
                                          ?? new ArchitectureLayer { Namespace = sourceNamespacePrefix };
        List<string> sourceFiles = FindSourceFilesForNamespace(repositoryRoot, effectiveLayer, roots);
        if (sourceFiles.Count == 0)
        {
            return Array.Empty<ArchitectureViolation>();
        }

        CSharpCompilation compilation = BuildCompilation(sourceFiles, preprocessorSymbols);
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
                .Where(match => !ArchitectureIgnoreMatcher.IsIgnored(relativePath, match, ignoredViolations, usageTracker))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(match => match, StringComparer.Ordinal)
                .ToArray();

            if (unignored.Count == 0)
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contractName,
                contractId,
                relativePath,
                "method-body",
                unignored));
        }

        return violations;
    }

    private static CSharpCompilation BuildCompilation(
        IReadOnlyList<string> sourceFiles,
        IReadOnlyList<string>? preprocessorSymbols = null)
    {
        CSharpParseOptions? parseOptions = preprocessorSymbols is { Count: > 0 }
            ? CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)
            : null;

        var syntaxTrees = sourceFiles
            .Select(filePath => CSharpSyntaxTree.ParseText(
                File.ReadAllText(filePath),
                options: parseOptions,
                path: filePath))
            .ToList();

        List<MetadataReference> references = BuildMetadataReferences();

        return CSharpCompilation.Create(
            "ArchitectureSourceScanner",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static List<MetadataReference> BuildMetadataReferences()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    paths.Add(path);
                }
            }
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string location;

            try
            {
                location = assembly.Location;
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
            {
                paths.Add(location);
            }
        }

        return paths
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
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

    private static List<string> FindSourceFilesForNamespace(string repositoryRoot, ArchitectureLayer layer, string[] sourceRoots)
    {
        List<string> result = new();

        foreach (string root in sourceRoots)
        {
            string fullRoot = Path.Combine(repositoryRoot, root);

            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (string filePath in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (FileContainsNamespace(filePath, layer))
                {
                    result.Add(filePath);
                }
            }
        }

        return result;
    }

    private static bool FileContainsNamespace(string filePath, ArchitectureLayer layer)
    {
        try
        {
            foreach (string line in File.ReadLines(filePath))
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
