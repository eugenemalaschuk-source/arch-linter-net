using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchLinterNet.Core.Scanning;

/// <summary>
/// Walks the syntax tree once while carrying the executable-body regions that contain each node.
/// Matches are kept in the original method/accessor/local-function region order so identity and
/// suppression side effects remain unchanged even though each syntax node is queried only once.
/// </summary>
internal sealed class ArchitectureSourceExecutableBodyTraversal : CSharpSyntaxWalker
{
    private enum ExecutableBodyKind
    {
        Method,
        Accessor,
        LocalFunction,
    }

    private sealed class ExecutableBodyRegion(ExecutableBodyKind kind, ExecutableBodyRegion? parent)
    {
        public ExecutableBodyKind Kind { get; } = kind;

        public ExecutableBodyRegion? Parent { get; } = parent;

        public List<ArchitectureSourceScanner.ForbiddenCallMatch> Matches { get; } = new();
    }

    private readonly SemanticModel _semanticModel;
    private readonly IReadOnlyList<ForbiddenCallPattern> _patterns;
    private readonly Dictionary<string, bool> _matchCache;
    private readonly Action? _onSyntaxNodeVisited;
    private readonly Action? _onSemanticLookup;
    private readonly Dictionary<SyntaxNode, ExecutableBodyRegion> _bodyRootRegions = new();
    private readonly List<ExecutableBodyRegion> _methodBodies = new();
    private readonly List<ExecutableBodyRegion> _accessorBodies = new();
    private readonly List<ExecutableBodyRegion> _localFunctionBodies = new();
    private ExecutableBodyRegion? _activeBodyRegions;

    public ArchitectureSourceExecutableBodyTraversal(
        SemanticModel semanticModel,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache,
        Action? onSyntaxNodeVisited,
        Action? onSemanticLookup)
    {
        _semanticModel = semanticModel;
        _patterns = patterns;
        _matchCache = matchCache;
        _onSyntaxNodeVisited = onSyntaxNodeVisited;
        _onSemanticLookup = onSemanticLookup;
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node == null)
        {
            return;
        }

        _onSyntaxNodeVisited?.Invoke();

        if (_activeBodyRegions != null)
        {
            RecordNodeMatch(node);
        }

        if (TryGetExecutableBodyRoots(
                node, out ExecutableBodyKind kind, out SyntaxNode? firstBodyRoot, out SyntaxNode? secondBodyRoot))
        {
            if (_activeBodyRegions == null)
            {
                if (firstBodyRoot != null)
                {
                    VisitBodyRootDescendants(firstBodyRoot, CreateBodyRegion(kind, parent: null));
                }

                if (secondBodyRoot != null)
                {
                    VisitBodyRootDescendants(secondBodyRoot, CreateBodyRegion(kind, parent: null));
                }

                return;
            }

            if (firstBodyRoot != null)
            {
                _bodyRootRegions.Add(firstBodyRoot, CreateBodyRegion(kind, _activeBodyRegions));
            }

            if (secondBodyRoot != null)
            {
                _bodyRootRegions.Add(secondBodyRoot, CreateBodyRegion(kind, _activeBodyRegions));
            }
        }

        ExecutableBodyRegion? previousBodyRegions = _activeBodyRegions;
        if (_bodyRootRegions.Remove(node, out ExecutableBodyRegion? bodyRegion))
        {
            _activeBodyRegions = bodyRegion;
        }

        try
        {
            base.Visit(node);
        }
        finally
        {
            _activeBodyRegions = previousBodyRegions;
        }
    }

    public List<ArchitectureSourceScanner.ForbiddenCallMatch> CollectMatches()
    {
        int totalMatches = _methodBodies.Sum(body => body.Matches.Count)
                           + _accessorBodies.Sum(body => body.Matches.Count)
                           + _localFunctionBodies.Sum(body => body.Matches.Count);
        List<ArchitectureSourceScanner.ForbiddenCallMatch> matches = new(totalMatches);
        AppendMatches(_methodBodies, matches);
        AppendMatches(_accessorBodies, matches);
        AppendMatches(_localFunctionBodies, matches);
        return matches;
    }

    private void VisitBodyRootDescendants(SyntaxNode bodyRoot, ExecutableBodyRegion bodyRegion)
    {
        ExecutableBodyRegion? previousBodyRegions = _activeBodyRegions;
        _activeBodyRegions = bodyRegion;
        try
        {
            // DescendantNodes() deliberately excludes the executable root. Visiting only its
            // children preserves the existing expression-body and block-root coverage exactly.
            foreach (SyntaxNode child in bodyRoot.ChildNodes())
            {
                Visit(child);
            }
        }
        finally
        {
            _activeBodyRegions = previousBodyRegions;
        }
    }

    private ExecutableBodyRegion CreateBodyRegion(ExecutableBodyKind kind, ExecutableBodyRegion? parent)
    {
        ExecutableBodyRegion region = new(kind, parent);
        GetRegionList(kind).Add(region);
        return region;
    }

    private List<ExecutableBodyRegion> GetRegionList(ExecutableBodyKind kind)
    {
        return kind switch
        {
            ExecutableBodyKind.Method => _methodBodies,
            ExecutableBodyKind.Accessor => _accessorBodies,
            ExecutableBodyKind.LocalFunction => _localFunctionBodies,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private void RecordNodeMatch(SyntaxNode node)
    {
        if (!ArchitectureSourceScanner.TryGetReferencedSymbol(
                _semanticModel, node, out ISymbol? symbol, out bool usedCandidateFallback, _onSemanticLookup)
            || symbol == null)
        {
            return;
        }

        SymbolDescriptor descriptor = ArchitectureForbiddenCallMatcher.FromRoslynSymbol(symbol);
        if (!ArchitectureForbiddenCallMatcher.TryMatch(
                descriptor, _patterns, _matchCache, out string matchedPattern))
        {
            return;
        }

        string symbolName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        string fallbackMarker = usedCandidateFallback ? " [ambiguous-candidate]" : string.Empty;
        string display = $"line {line}: {matchedPattern} -> {symbolName}{fallbackMarker}";
        string? sourceMember = _semanticModel.GetEnclosingSymbol(node.SpanStart)
            ?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        ArchitectureSourceScanner.ForbiddenCallMatch match = new(
            display,
            symbol.ContainingAssembly?.Name,
            symbol.ContainingType?.ToDisplayString(),
            symbolName,
            sourceMember);

        // Each containing region receives the same immutable match reference. This preserves the
        // original candidate and suppression call count/order without repeating syntax traversal,
        // symbol resolution, display formatting, or enclosing-member lookup.
        for (ExecutableBodyRegion? region = _activeBodyRegions; region != null; region = region.Parent)
        {
            region.Matches.Add(match);
        }
    }

    private static void AppendMatches(
        IEnumerable<ExecutableBodyRegion> bodyRegions, List<ArchitectureSourceScanner.ForbiddenCallMatch> matches)
    {
        foreach (ExecutableBodyRegion bodyRegion in bodyRegions)
        {
            matches.AddRange(bodyRegion.Matches);
        }
    }
    private static bool TryGetExecutableBodyRoots(
        SyntaxNode declaration,
        out ExecutableBodyKind kind,
        out SyntaxNode? firstBodyRoot,
        out SyntaxNode? secondBodyRoot)
    {
        kind = default;
        firstBodyRoot = null;
        secondBodyRoot = null;

        switch (declaration)
        {
            case BaseMethodDeclarationSyntax method:
                kind = ExecutableBodyKind.Method;
                firstBodyRoot = method.Body;
                secondBodyRoot = method.ExpressionBody?.Expression;
                break;
            case AccessorDeclarationSyntax accessor:
                kind = ExecutableBodyKind.Accessor;
                firstBodyRoot = accessor.Body;
                secondBodyRoot = accessor.ExpressionBody?.Expression;
                break;
            case LocalFunctionStatementSyntax localFunction:
                kind = ExecutableBodyKind.LocalFunction;
                firstBodyRoot = localFunction.Body;
                secondBodyRoot = localFunction.ExpressionBody?.Expression;
                break;
            default:
                return false;
        }

        return firstBodyRoot != null || secondBodyRoot != null;
    }
}
