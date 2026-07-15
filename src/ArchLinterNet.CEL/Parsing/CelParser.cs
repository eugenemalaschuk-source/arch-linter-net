using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// Precedence-climbing (Pratt) parser converting a Profile v1 token stream into an immutable
/// internal syntax tree.
/// </summary>
/// <remarks>
/// <para>
/// Implements the frozen precedence/associativity table from the <c>cel-profile-v1</c> spec,
/// lowest to highest binding power: <c>||</c> &lt; <c>&amp;&amp;</c> &lt; non-associative
/// comparisons (<c>== != &lt; &lt;= &gt; &gt;= in</c>) &lt; unary <c>!</c> &lt; postfix
/// member access/indexing.
/// </para>
/// <para>
/// Fails fast: the first syntax error, unsupported-feature condition, or structural-limit
/// violation stops parsing and returns exactly one diagnostic — see design decision 6 in
/// <c>openspec/changes/2026-07-15-cel-tokenizer-parser/design.md</c>.
/// </para>
/// </remarks>
internal sealed class CelParser
{
    private readonly IReadOnlyList<CelToken> _tokens;
    private readonly CelCompilationLimits _limits;
    private readonly CelProfileId _profileId;
    private int _pos;
    private int _depth;
    private int _nodeCount;

    private CelParser(IReadOnlyList<CelToken> tokens, CelCompilationLimits limits, CelProfileId profileId)
    {
        _tokens = tokens;
        _limits = limits;
        _profileId = profileId;
    }

    public static CelParseResult Parse(IReadOnlyList<CelToken> tokens, CelCompilationLimits limits, CelProfileId profileId)
    {
        var parser = new CelParser(tokens, limits, profileId);
        try
        {
            var root = parser.ParseExpression();
            if (parser.Current.Kind != CelTokenKind.Eof)
            {
                throw parser.Fail(
                    parser.Current.Span, $"Unexpected trailing input starting with '{parser.Current.Text}'.");
            }

            return CelParseResult.Success(root);
        }
        catch (CelParseException ex)
        {
            return CelParseResult.Failed(ex.Diagnostic);
        }
    }

    private CelToken Current => _tokens[_pos];

    private CelToken Advance()
    {
        var token = _tokens[_pos];
        if (_pos < _tokens.Count - 1)
            _pos++;
        return token;
    }

    private bool Check(CelTokenKind kind) => Current.Kind == kind;

    private CelToken Expect(CelTokenKind kind, string what)
    {
        if (!Check(kind))
            throw Fail(Current.Span, $"Expected {what} but found '{Current.Text}'.");
        return Advance();
    }

    /// <summary>Top-level expression entry — also the recursion point for parens, index expressions, and call arguments.</summary>
    private CelSyntaxNode ParseExpression()
    {
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
            throw FailBudget(Current.Span, "MaxNestingDepth", _depth);
        try
        {
            var expr = ParseOr();
            if (Check(CelTokenKind.Question))
            {
                throw FailUnsupported(
                    Current.Span, "The conditional operator ('? :') is deferred in Profile v1.", "conditional");
            }

            // Checked here (not only at the top-level Parse() entry) so `f(a + b)`, `(a + b)`,
            // and `items[a + b]` all correctly report UnsupportedFeature instead of a generic
            // "expected ')'/']'/','" SyntaxError from the enclosing construct.
            if (IsDeferredBinaryOperator(Current.Kind))
            {
                throw FailUnsupported(
                    Current.Span, "Arithmetic operators are deferred in Profile v1.", "arithmetic");
            }

            return expr;
        }
        finally
        {
            _depth--;
        }
    }

    private CelSyntaxNode ParseOr()
    {
        var left = ParseAnd();
        while (Check(CelTokenKind.PipePipe))
        {
            Advance();
            var right = ParseAnd();
            left = Track(new CelBinarySyntax(Merge(left.Span, right.Span), CelBinaryOperator.Or, left, right));
        }

        return left;
    }

    private CelSyntaxNode ParseAnd()
    {
        var left = ParseComparison();
        while (Check(CelTokenKind.AmpAmp))
        {
            Advance();
            var right = ParseComparison();
            left = Track(new CelBinarySyntax(Merge(left.Span, right.Span), CelBinaryOperator.And, left, right));
        }

        return left;
    }

    private CelSyntaxNode ParseComparison()
    {
        var left = ParseUnary();
        if (!TryMatchComparisonOperator(out var op))
            return left;

        var right = ParseUnary();
        var node = Track(new CelBinarySyntax(Merge(left.Span, right.Span), op, left, right));

        if (IsComparisonOperator(Current.Kind))
            throw Fail(Current.Span, "Chained comparison operators require explicit parentheses in Profile v1.");

        return node;
    }

    private bool TryMatchComparisonOperator(out CelBinaryOperator op)
    {
        op = default;
        if (!IsComparisonOperator(Current.Kind))
            return false;

        op = Current.Kind switch
        {
            CelTokenKind.EqEq => CelBinaryOperator.Equal,
            CelTokenKind.NotEq => CelBinaryOperator.NotEqual,
            CelTokenKind.Lt => CelBinaryOperator.Less,
            CelTokenKind.LtEq => CelBinaryOperator.LessOrEqual,
            CelTokenKind.Gt => CelBinaryOperator.Greater,
            CelTokenKind.GtEq => CelBinaryOperator.GreaterOrEqual,
            CelTokenKind.In => CelBinaryOperator.In,
            _ => throw new InvalidOperationException("Unreachable: guarded by IsComparisonOperator."),
        };
        Advance();
        return true;
    }

    private static bool IsDeferredBinaryOperator(CelTokenKind kind) => kind is
        CelTokenKind.Plus or CelTokenKind.Minus or CelTokenKind.Star or CelTokenKind.Slash or CelTokenKind.Percent;

    private static bool IsComparisonOperator(CelTokenKind kind) => kind is
        CelTokenKind.EqEq or CelTokenKind.NotEq or
        CelTokenKind.Lt or CelTokenKind.LtEq or
        CelTokenKind.Gt or CelTokenKind.GtEq or
        CelTokenKind.In;

    private CelSyntaxNode ParseUnary()
    {
        if (Check(CelTokenKind.Bang))
        {
            var bangToken = Advance();
            _depth++;
            if (_depth > _limits.MaxNestingDepth)
                throw FailBudget(bangToken.Span, "MaxNestingDepth", _depth);
            try
            {
                var operand = ParseUnary();
                return Track(new CelUnarySyntax(Merge(bangToken.Span, operand.Span), CelUnaryOperator.Not, operand));
            }
            finally
            {
                _depth--;
            }
        }

        if (Check(CelTokenKind.Minus) || Check(CelTokenKind.Plus))
        {
            throw FailUnsupported(
                Current.Span, "Arithmetic operators (including unary '+'/'-') are deferred in Profile v1.", "arithmetic");
        }

        return ParsePostfix();
    }

    private CelSyntaxNode ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Check(CelTokenKind.Dot))
            {
                Advance();
                var nameToken = ExpectSelectorName();
                if (Check(CelTokenKind.LParen))
                {
                    Advance();
                    var args = ParseArguments();
                    var closeParen = Expect(CelTokenKind.RParen, "')'");
                    expr = Track(new CelCallSyntax(Merge(expr.Span, closeParen.Span), expr, nameToken.Text, args));
                }
                else
                {
                    expr = Track(new CelMemberAccessSyntax(Merge(expr.Span, nameToken.Span), expr, nameToken.Text));
                }
            }
            else if (Check(CelTokenKind.LBracket))
            {
                Advance();
                var index = ParseExpression();
                var closeBracket = Expect(CelTokenKind.RBracket, "']'");
                expr = Track(new CelIndexSyntax(Merge(expr.Span, closeBracket.Span), expr, index));
            }
            else if (Check(CelTokenKind.LBrace))
            {
                // `IDENT ("." IDENT)* "{" ... "}"` — message literal construction. Valid CEL
                // syntax per the pinned grammar; message/proto literals are deferred in v1.
                throw FailUnsupported(
                    Current.Span, "Message literal syntax is deferred in Profile v1.", "message-literal");
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private CelToken ExpectSelectorName()
    {
        if (Current.Kind != CelTokenKind.Identifier)
            throw Fail(Current.Span, $"Expected a member name after '.' but found '{Current.Text}'.");
        return Advance();
    }

    private IReadOnlyList<CelSyntaxNode> ParseArguments()
    {
        var args = new List<CelSyntaxNode>();
        if (Check(CelTokenKind.RParen))
            return args;

        args.Add(ParseExpression());
        while (Check(CelTokenKind.Comma))
        {
            Advance();
            args.Add(ParseExpression());
        }

        return args;
    }

    private CelSyntaxNode ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case CelTokenKind.BoolLiteral:
                Advance();
                return Track(new CelBoolLiteralSyntax(token.Span, token.BoolValue));
            case CelTokenKind.IntLiteral:
                Advance();
                return Track(new CelIntLiteralSyntax(token.Span, token.IntValue));
            case CelTokenKind.FloatLiteral:
                Advance();
                return Track(new CelFloatLiteralSyntax(token.Span, token.FloatValue));
            case CelTokenKind.StringLiteral:
                Advance();
                return Track(new CelStringLiteralSyntax(token.Span, token.StringValue!));
            case CelTokenKind.NullLiteral:
                throw FailUnsupported(token.Span, "Profile v1 has no null value.", "null");
            case CelTokenKind.UintLiteral:
                throw FailUnsupported(token.Span, "Unsigned integer literals are deferred in Profile v1.", "uint");
            case CelTokenKind.BytesLiteral:
                throw FailUnsupported(token.Span, "Byte-string literals are deferred in Profile v1.", "bytes");
            case CelTokenKind.LBrace:
                throw FailUnsupported(token.Span, "Map/message literal syntax is deferred in Profile v1.", "map-literal");
            case CelTokenKind.LBracket:
                throw FailUnsupported(token.Span, "List literal syntax is deferred in Profile v1.", "list-literal");
            case CelTokenKind.Dot:
                // `"." IDENT ("." IDENT)*` — root/absolute-qualified name syntax. Valid CEL
                // syntax per the pinned grammar; deferred in v1 (no package-qualified names).
                throw FailUnsupported(
                    token.Span, "Root-qualified ('.'-prefixed) name syntax is deferred in Profile v1.", "root-qualified-name");
            case CelTokenKind.Plus:
            case CelTokenKind.Minus:
            case CelTokenKind.Star:
            case CelTokenKind.Slash:
            case CelTokenKind.Percent:
                throw FailUnsupported(token.Span, "Arithmetic operators are deferred in Profile v1.", "arithmetic");
            case CelTokenKind.LParen:
                {
                    Advance();
                    var inner = ParseExpression();
                    Expect(CelTokenKind.RParen, "')'");
                    return inner;
                }

            case CelTokenKind.Identifier:
                {
                    if (token.IsReserved)
                        throw Fail(token.Span, $"'{token.Text}' is a reserved identifier and cannot be used as a value.");
                    Advance();
                    if (Check(CelTokenKind.LParen))
                    {
                        Advance();
                        var args = ParseArguments();
                        var closeParen = Expect(CelTokenKind.RParen, "')'");
                        return Track(new CelCallSyntax(Merge(token.Span, closeParen.Span), null, token.Text, args));
                    }

                    return Track(new CelIdentifierSyntax(token.Span, token.Text));
                }

            case CelTokenKind.Eof:
                throw Fail(token.Span, "Expected an expression but reached the end of input.");
            default:
                throw Fail(token.Span, $"Unexpected token '{token.Text}'.");
        }
    }

    private T Track<T>(T node) where T : CelSyntaxNode
    {
        _nodeCount++;
        if (_nodeCount > _limits.MaxAstNodeCount)
            throw FailBudget(node.Span, "MaxAstNodeCount", _nodeCount);
        return node;
    }

    private static CelSourceSpan Merge(CelSourceSpan a, CelSourceSpan b) =>
        new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));

    private CelParseException Fail(CelSourceSpan span, string message) =>
        new(CelParseDiagnostics.SyntaxError(span, message, _profileId));

    private CelParseException FailUnsupported(CelSourceSpan span, string message, string feature) =>
        new(CelParseDiagnostics.UnsupportedFeature(span, message, _profileId, feature));

    private CelParseException FailBudget(CelSourceSpan span, string limitName, long observedValue) =>
        new(CelParseDiagnostics.BudgetExceeded(span, limitName, observedValue, _profileId));

    private sealed class CelParseException : Exception
    {
        public CelDiagnostic Diagnostic { get; }

        public CelParseException(CelDiagnostic diagnostic) => Diagnostic = diagnostic;
    }
}
