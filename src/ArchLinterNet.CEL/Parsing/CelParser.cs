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
/// comparisons (<c>== != &lt; &lt;= &gt; &gt;= in</c>) &lt; deferred arithmetic (<c>+ - * / %</c>)
/// &lt; unary <c>!</c>/<c>-</c> &lt; postfix member access/indexing — matching the pinned
/// grammar's full <c>Relation</c>/<c>Addition</c>/<c>Multiplication</c> chain so a deferred
/// arithmetic sub-expression nested anywhere (inside a comparison, a ternary branch, a call
/// argument, ...) is consumed at the correct grammar level rather than only recognized when it
/// happens to trail a fully-reduced <c>ConditionalOr</c>.
/// </para>
/// <para>
/// Fails fast on genuine syntax errors: the first malformed construct stops parsing and returns
/// exactly one <c>SyntaxError</c>/<c>BudgetExceeded</c> diagnostic immediately (via exception) —
/// see design decision 6. Deferred-but-valid CEL constructs (arithmetic, the conditional operator,
/// list/map/message literals, root-qualified names) do NOT throw immediately when recognized;
/// instead the parser validates their own syntax fully, records a single pending
/// <c>UnsupportedFeature</c> diagnostic (first one wins), and keeps parsing normally with a
/// <see cref="CelDeferredSyntax"/> placeholder standing in for the construct. Only once the whole
/// top-level expression has parsed successfully (every enclosing paren/bracket/brace closed, every
/// ternary's `:` and false branch present, etc.) does <see cref="Parse"/> report the pending
/// diagnostic — see design decision 17. This is what makes `(a + b` correctly `SyntaxError`
/// (unterminated paren, never reaches the pending check) while `(a + b)` is `UnsupportedFeature`
/// (fully validated, arithmetic reported only after the paren closes).
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
    private int _identifierCount;
    private (CelSourceSpan Span, string Message, string Feature)? _pendingDeferred;

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

            // Only decided now — after the entire expression, including every enclosing
            // paren/bracket/brace and ternary branch, has been verified structurally complete.
            if (parser._pendingDeferred is { } deferred)
            {
                return CelParseResult.Failed(CelParseDiagnostics.UnsupportedFeature(
                    deferred.Span, deferred.Message, profileId, deferred.Feature));
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

    /// <summary>Records the first deferred-construct diagnostic encountered; later calls are no-ops.</summary>
    private void MarkDeferred(CelSourceSpan span, string message, string feature) =>
        _pendingDeferred ??= (span, message, feature);

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
                // Pinned grammar: Expr = ConditionalOr ["?" ConditionalOr ":" Expr]. The true
                // branch is ConditionalOr precedence (ParseOr already absorbs any arithmetic it
                // contains via ParseComparison/ParseAdditionLevel below — no unparenthesized
                // nested ternary is valid there). The false branch is the full recursive Expr
                // (nested ternary IS allowed there). Both are fully validated — including any
                // arithmetic or nested ternary — before this ternary's own deferred marker is
                // recorded, so "a ? b" (missing ':'/false branch) still throws SyntaxError instead
                // of ever reaching MarkDeferred.
                var questionToken = Advance();
                _ = ParseOr();
                Expect(CelTokenKind.Colon, "':'");
                var falseBranch = ParseExpression();
                var span = Merge(questionToken.Span, falseBranch.Span);
                MarkDeferred(span, "The conditional operator ('? :') is deferred in Profile v1.", "conditional");
                return Track(new CelDeferredSyntax(span));
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

    /// <summary>Pinned grammar: <c>Relation = Addition [Relop Addition]</c> — each comparison operand is itself an Addition-level (arithmetic-absorbing) parse.</summary>
    private CelSyntaxNode ParseComparison()
    {
        var left = ParseAdditionLevel();
        if (!TryMatchComparisonOperator(out var op))
            return left;

        var right = ParseAdditionLevel();
        var node = Track(new CelBinarySyntax(Merge(left.Span, right.Span), op, left, right));

        if (IsComparisonOperator(Current.Kind))
            throw Fail(Current.Span, "Chained comparison operators require explicit parentheses in Profile v1.");

        return node;
    }

    /// <summary>
    /// Pinned grammar: <c>Addition = Multiplication {("+"|"-") Multiplication}</c>,
    /// <c>Multiplication = Unary {("*"|"/"|"%") Unary}</c>. Both levels are collapsed into one
    /// flat operator-chain loop here: since Profile v1 builds no real arithmetic AST (every
    /// operator in this chain is deferred), the precedence distinction between <c>+</c>/<c>-</c>
    /// and <c>*</c>/<c>/</c>/<c>%</c> would only affect tree *shape*, never which tokens are
    /// consumed — and only consumption matters for correctly delimiting the deferred region and
    /// validating what follows. Each right-hand operand is still a real <see cref="ParseUnary"/>
    /// parse, so a malformed chain (e.g. a dangling trailing operator) still throws
    /// <c>SyntaxError</c> from the underlying operand parse before any marking happens.
    /// </summary>
    private CelSyntaxNode ParseAdditionLevel()
    {
        var left = ParseUnary();
        while (IsDeferredBinaryOperator(Current.Kind))
        {
            Advance();
            var right = ParseUnary();
            var span = Merge(left.Span, right.Span);
            MarkDeferred(span, "Arithmetic operators are deferred in Profile v1.", "arithmetic");
            left = Track(new CelDeferredSyntax(span));
        }

        return left;
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

    /// <summary>
    /// Pinned grammar: <c>Unary: Member | "!" {"!"} Member | "-" {"-"} Member</c> — a unary
    /// prefix chain may repeat only the SAME operator; mixing `!` and `-` (e.g. `!-x`, `-!x`) has
    /// no valid CEL interpretation and is a syntax error, not deferred syntax.
    /// </summary>
    private CelSyntaxNode ParseUnary()
    {
        if (Check(CelTokenKind.Bang))
            return ParsePrefixChain(CelTokenKind.Bang);
        if (Check(CelTokenKind.Minus))
            return ParsePrefixChain(CelTokenKind.Minus);

        // '+' has no unary/prefix form in the pinned CEL grammar (only '!' and '-' do), and
        // '*'/'/'/'%' never have a prefix form either — encountering any of them here is always
        // invented/invalid syntax, handled by ParsePrimary's default case (SyntaxError).
        return ParsePostfix();
    }

    private CelSyntaxNode ParsePrefixChain(CelTokenKind opKind)
    {
        var opTokens = new List<CelToken>();
        try
        {
            while (Check(opKind))
            {
                var opToken = Advance();
                opTokens.Add(opToken);
                _depth++;
                if (_depth > _limits.MaxNestingDepth)
                    throw FailBudget(opToken.Span, "MaxNestingDepth", _depth);
            }

            var otherOp = opKind == CelTokenKind.Bang ? CelTokenKind.Minus : CelTokenKind.Bang;
            if (Check(otherOp))
            {
                throw Fail(
                    Current.Span,
                    $"Cannot mix '{(opKind == CelTokenKind.Bang ? "!" : "-")}' and " +
                    $"'{(opKind == CelTokenKind.Bang ? "-" : "!")}' in a unary prefix chain.");
            }

            var operand = ParsePostfix();

            if (opKind == CelTokenKind.Bang)
            {
                var result = operand;
                for (var i = opTokens.Count - 1; i >= 0; i--)
                    result = Track(new CelUnarySyntax(Merge(opTokens[i].Span, result.Span), CelUnaryOperator.Not, result));
                return result;
            }

            // '-' has a real unary form in the pinned grammar but is deferred (arithmetic) in
            // v1, never accepted as part of the literal — see design decision 1.
            var span = Merge(opTokens[0].Span, operand.Span);
            MarkDeferred(span, "Arithmetic operators (including unary '-') are deferred in Profile v1.", "arithmetic");
            return Track(new CelDeferredSyntax(span));
        }
        finally
        {
            _depth -= opTokens.Count;
        }
    }

    /// <summary>
    /// <c>isQualifiedNameChain</c> tracks whether <c>expr</c> is currently eligible to
    /// be a message-literal receiver — true only for a bare identifier / root-qualified-name chain
    /// built purely from <c>.selector</c> steps, with no intervening call, index, or
    /// parenthesization. This is deliberately tracked as explicit parse state rather than inferred
    /// from the resulting <see cref="CelSyntaxNode"/>'s shape: a parenthesized qualified name (e.g.
    /// <c>(Type)</c>) produces the exact same <see cref="CelIdentifierSyntax"/> node a bare
    /// <c>Type</c> would, but per the pinned grammar a message literal's <c>SELECTOR ("." SELECTOR)*</c>
    /// prefix is a primary-level production, not a generic postfix step usable after any
    /// expression that happens to evaluate to an identifier shape — so <c>(Type){field: 1}</c> must
    /// be <c>SyntaxError</c> even though <c>Type{field: 1}</c> is <c>UnsupportedFeature</c>.
    /// </summary>
    /// <remarks>
    /// <c>pendingReservedRoot</c> is non-null exactly when <c>expr</c>'s chain started
    /// with a reserved identifier (e.g. <c>package</c>) — per the pinned grammar, a reserved word
    /// is only valid as the root of a <c>SELECTOR ("." SELECTOR)*</c> chain that is ITSELF only
    /// valid when it terminates in a message literal (<c>"{" fieldInits "}"</c>); it is invalid as
    /// a plain qualified-name reference, a call, or an index target. Checking only the token
    /// immediately following the reserved root (as an earlier fix did) is insufficient —
    /// <c>package.Type</c> must also be rejected, not only <c>package</c> alone — so this flag is
    /// carried through the *entire* postfix chain and only resolved once the chain is known to
    /// have ended: cleared (satisfied) the moment a message literal is actually reached, and
    /// checked — throwing if still set — after the loop exits with no message literal reached. See
    /// design decision 23.
    /// </remarks>
    private CelSyntaxNode ParsePostfix()
    {
        var expr = ParsePrimary(out var isQualifiedNameChain, out var pendingReservedRoot);
        var chainDepth = 0;
        try
        {
            while (true)
            {
                if (Check(CelTokenKind.Dot))
                {
                    EnterChainStep(ref chainDepth);
                    Advance();
                    var nameToken = ExpectSelectorName();
                    TrackIdentifier(nameToken.Span);
                    if (Check(CelTokenKind.LParen))
                    {
                        Advance();
                        var args = ParseArguments();
                        var closeParen = Expect(CelTokenKind.RParen, "')'");
                        expr = Track(new CelCallSyntax(Merge(expr.Span, closeParen.Span), expr, nameToken.Text, args));
                        isQualifiedNameChain = false; // a call result is never a qualified name
                    }
                    else
                    {
                        expr = Track(new CelMemberAccessSyntax(Merge(expr.Span, nameToken.Span), expr, nameToken.Text));
                    }
                }
                else if (Check(CelTokenKind.LBracket))
                {
                    EnterChainStep(ref chainDepth);
                    Advance();
                    var index = ParseExpression();
                    var closeBracket = Expect(CelTokenKind.RBracket, "']'");
                    expr = Track(new CelIndexSyntax(Merge(expr.Span, closeBracket.Span), expr, index));
                    isQualifiedNameChain = false; // an index result is never a qualified name
                }
                else if (Check(CelTokenKind.LBrace) && isQualifiedNameChain)
                {
                    // Message literal construction (a qualified name followed by a brace body) is
                    // valid CEL syntax per the pinned grammar, but only after a qualified-name-shaped
                    // receiver — e.g. `1{}` has no such receiver and is genuinely invalid, not deferred.
                    var openBrace = Current;
                    var closeBrace = ParseBraceBody(openBrace.Span, isMessageLiteral: true);
                    var span = Merge(expr.Span, closeBrace.Span);
                    MarkDeferred(span, "Message literal syntax is deferred in Profile v1.", "message-literal");
                    expr = Track(new CelDeferredSyntax(span));
                    isQualifiedNameChain = false;
                    pendingReservedRoot = null; // satisfied — the chain reached a message literal
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            _depth -= chainDepth;
        }

        if (pendingReservedRoot is { } reserved)
        {
            throw Fail(
                reserved.Span,
                $"'{reserved.Text}' is a reserved identifier; it is only valid as the root of a " +
                "message-literal type name (must be followed by a '{...}' body somewhere in the chain).");
        }

        return expr;
    }

    /// <summary>
    /// The pinned grammar's member-access-chain nesting depth counts each `.selector`/`[index]`
    /// step (the public <c>MaxNestingDepth</c> doc explicitly lists "member access chains" as an
    /// example), not only recursive constructs like parentheses.
    /// </summary>
    private void EnterChainStep(ref int chainDepth)
    {
        chainDepth++;
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
            throw FailBudget(Current.Span, "MaxNestingDepth", _depth);
    }

    /// <summary>
    /// Validates a <c>"{" [entry ("," entry)*] "}"</c> body and returns the closing brace token.
    /// A standalone <c>{</c> (<paramref name="isMessageLiteral"/> <c>false</c>) is a map literal —
    /// entry keys are arbitrary expressions. A <c>{</c> following a qualified-name receiver
    /// (<paramref name="isMessageLiteral"/> <c>true</c>) is a message literal per
    /// <c>IDENT ("." IDENT)* "{" ... "}"</c> — entry keys MUST be bare field-name identifiers, so
    /// e.g. <c>Type{1: 2}</c> is a syntax error, not deferred syntax. Entry count is bounded by
    /// <c>MaxLiteralSize</c>, matching its "element count for list/map literals" contract.
    /// </summary>
    private CelToken ParseBraceBody(CelSourceSpan openSpan, bool isMessageLiteral)
    {
        Advance(); // consume '{'
        var entryCount = 0;
        if (!Check(CelTokenKind.RBrace))
        {
            ParseBraceEntry(isMessageLiteral);
            TrackLiteralElement(openSpan, ++entryCount);
            while (Check(CelTokenKind.Comma))
            {
                Advance();
                if (Check(CelTokenKind.RBrace))
                    break;
                ParseBraceEntry(isMessageLiteral);
                TrackLiteralElement(openSpan, ++entryCount);
            }
        }

        return Expect(CelTokenKind.RBrace, "'}'");
    }

    /// <summary>Validates one <c>key ":" value</c> entry. Discards the parsed nodes — only used to verify well-formedness.</summary>
    private void ParseBraceEntry(bool isMessageLiteral)
    {
        if (isMessageLiteral)
        {
            // Message-literal field keys are bare identifiers (field names), never a general
            // expression — `Type{1: 2}` is not valid CEL under any interpretation.
            var fieldToken = Current;
            if (fieldToken.Kind != CelTokenKind.Identifier)
                throw Fail(fieldToken.Span, $"Expected a field name but found '{fieldToken.Text}'.");
            Advance();
            TrackIdentifier(fieldToken.Span);
        }
        else
        {
            ParseExpression();
        }

        Expect(CelTokenKind.Colon, "':'");
        ParseExpression();
    }

    private CelToken ExpectSelectorName()
    {
        if (Current.Kind != CelTokenKind.Identifier)
            throw Fail(Current.Span, $"Expected a member name after '.' but found '{Current.Text}'.");
        return Advance();
    }

    private List<CelSyntaxNode> ParseArguments()
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

    /// <summary>
    /// See the <see cref="ParsePostfix"/> doc for what <paramref name="isQualifiedNameChain"/> and
    /// <paramref name="pendingReservedRoot"/> mean.
    /// </summary>
    private CelSyntaxNode ParsePrimary(out bool isQualifiedNameChain, out CelToken? pendingReservedRoot)
    {
        isQualifiedNameChain = false;
        pendingReservedRoot = null;
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
                Advance();
                MarkDeferred(token.Span, "Profile v1 has no null value.", "null");
                return Track(new CelDeferredSyntax(token.Span));
            case CelTokenKind.UintLiteral:
                Advance();
                MarkDeferred(token.Span, "Unsigned integer literals are deferred in Profile v1.", "uint");
                return Track(new CelDeferredSyntax(token.Span));
            case CelTokenKind.BytesLiteral:
                Advance();
                MarkDeferred(token.Span, "Byte-string literals are deferred in Profile v1.", "bytes");
                return Track(new CelDeferredSyntax(token.Span));
            case CelTokenKind.TripleQuotedStringLiteral:
                Advance();
                MarkDeferred(token.Span, "Triple-quoted string literals are deferred in Profile v1.", "triple-quoted-string");
                return Track(new CelDeferredSyntax(token.Span));
            case CelTokenKind.LBrace:
                return ParseMapLiteralPrimary(token);
            case CelTokenKind.LBracket:
                return ParseListLiteralPrimary(token);
            case CelTokenKind.Dot:
                return ParseRootQualifiedNamePrimary(out isQualifiedNameChain, out pendingReservedRoot);
            case CelTokenKind.LParen:
                {
                    // isQualifiedNameChain stays false regardless of what the parenthesized
                    // sub-expression contains — per the pinned grammar, a message literal's
                    // qualified-name prefix is a primary-level production, not a generic postfix
                    // step usable after any parenthesized expression that happens to evaluate to
                    // an identifier shape. "(Type){field: 1}" must be SyntaxError even though
                    // "Type{field: 1}" is UnsupportedFeature.
                    Advance();
                    var inner = ParseExpression();
                    Expect(CelTokenKind.RParen, "')'");
                    return inner;
                }

            case CelTokenKind.Identifier:
                return ParseIdentifierPrimary(token, out isQualifiedNameChain, out pendingReservedRoot);
            case CelTokenKind.Eof:
                throw Fail(token.Span, "Expected an expression but reached the end of input.");
            default:
                throw Fail(token.Span, $"Unexpected token '{token.Text}'.");
        }
    }

    /// <summary>
    /// Standalone <c>{</c> (no preceding qualified name) is a map literal — arbitrary-expression
    /// keys. Validates the brace contents before classifying as deferred — a malformed or
    /// unterminated <c>{...}</c> is a syntax error, not deferred syntax.
    /// </summary>
    private CelDeferredSyntax ParseMapLiteralPrimary(CelToken token)
    {
        var closeBrace = ParseBraceBody(token.Span, isMessageLiteral: false);
        var span = Merge(token.Span, closeBrace.Span);
        MarkDeferred(span, "Map/message literal syntax is deferred in Profile v1.", "map-literal");
        return Track(new CelDeferredSyntax(span));
    }

    /// <summary>
    /// Validates the bracket contents before classifying as deferred — a bare <c>[</c> with no
    /// valid contents (e.g. <c>[</c> alone) is a syntax error, not deferred. Element count is
    /// bounded by <c>MaxLiteralSize</c>.
    /// </summary>
    private CelDeferredSyntax ParseListLiteralPrimary(CelToken token)
    {
        Advance();
        var elementCount = 0;
        if (!Check(CelTokenKind.RBracket))
        {
            ParseExpression();
            TrackLiteralElement(token.Span, ++elementCount);
            while (Check(CelTokenKind.Comma))
            {
                Advance();
                if (Check(CelTokenKind.RBracket))
                    break;
                ParseExpression();
                TrackLiteralElement(token.Span, ++elementCount);
            }
        }

        var closeBracket = Expect(CelTokenKind.RBracket, "']'");
        var span = Merge(token.Span, closeBracket.Span);
        MarkDeferred(span, "List literal syntax is deferred in Profile v1.", "list-literal");
        return Track(new CelDeferredSyntax(span));
    }

    /// <summary>
    /// A leading dot followed by exactly one identifier is root/absolute-qualified name syntax —
    /// deferred in v1. Only the leading <c>"." IDENT</c> is consumed here (plus an immediately
    /// following call, e.g. <c>.f(...)</c>, mirroring <see cref="ParseIdentifierPrimary"/>); any
    /// further <c>.member</c>/<c>.call()</c>/<c>[index]</c>/message-literal steps are left to the
    /// caller's normal <see cref="ParsePostfix"/> loop, exactly like a non-root-qualified
    /// identifier chain, so e.g. <c>.pkg.f()</c> and <c>.pkg.Type{field: 1}</c> get identical
    /// call/message-literal/<c>MaxNestingDepth</c> handling to <c>pkg.f()</c> /
    /// <c>pkg.Type{field: 1}</c> instead of a bespoke (and previously incomplete) parallel path.
    /// A bare <c>.</c> with no following identifier is a syntax error, not deferred syntax. See
    /// <see cref="ParsePostfix"/>'s remarks for how a reserved root token's
    /// validity is resolved (<paramref name="pendingReservedRoot"/> output).
    /// </summary>
    private CelSyntaxNode ParseRootQualifiedNamePrimary(out bool isQualifiedNameChain, out CelToken? pendingReservedRoot)
    {
        var dotToken = Advance();
        var nameToken = ExpectSelectorName();
        var span = Merge(dotToken.Span, nameToken.Span);
        pendingReservedRoot = nameToken.IsReserved ? nameToken : null;
        TrackIdentifier(nameToken.Span);
        MarkDeferred(span, "Root-qualified ('.'-prefixed) name syntax is deferred in Profile v1.", "root-qualified-name");

        if (Check(CelTokenKind.LParen))
        {
            // Root-qualified free function call, e.g. ".f(...)" — the result is a call, not a
            // qualified name (mirrors ParseIdentifierPrimary's own call handling). A reserved
            // root immediately followed by "(" never satisfies pendingReservedRoot (only a
            // message literal does), so this call is left to fail at the caller's chain-exit
            // check, e.g. ".package()" — see design decision 23.
            isQualifiedNameChain = false;
            Advance();
            var args = ParseArguments();
            var closeParen = Expect(CelTokenKind.RParen, "')'");
            return Track(new CelCallSyntax(Merge(span, closeParen.Span), null, nameToken.Text, args));
        }

        isQualifiedNameChain = true;
        return Track(new CelIdentifierSyntax(span, nameToken.Text));
    }

    private CelSyntaxNode ParseIdentifierPrimary(CelToken token, out bool isQualifiedNameChain, out CelToken? pendingReservedRoot)
    {
        Advance();
        pendingReservedRoot = token.IsReserved ? token : null;
        TrackIdentifier(token.Span);
        if (Check(CelTokenKind.LParen))
        {
            // A reserved token here (e.g. "package(...)") never satisfies pendingReservedRoot —
            // left to fail at the caller's chain-exit check, per design decision 23.
            isQualifiedNameChain = false; // a free-function call result is never a qualified name
            Advance();
            var args = ParseArguments();
            var closeParen = Expect(CelTokenKind.RParen, "')'");
            return Track(new CelCallSyntax(Merge(token.Span, closeParen.Span), null, token.Text, args));
        }

        isQualifiedNameChain = true;
        return Track(new CelIdentifierSyntax(token.Span, token.Text));
    }

    private T Track<T>(T node) where T : CelSyntaxNode
    {
        _nodeCount++;
        if (_nodeCount > _limits.MaxAstNodeCount)
            throw FailBudget(node.Span, "MaxAstNodeCount", _nodeCount);
        return node;
    }

    /// <summary>
    /// Counts one distinct identifier reference (variable reference, function name, or member
    /// name) against <see cref="CelCompilationLimits.MaxIdentifierCount"/>.
    /// </summary>
    private void TrackIdentifier(CelSourceSpan span)
    {
        _identifierCount++;
        if (_identifierCount > _limits.MaxIdentifierCount)
            throw FailBudget(span, "MaxIdentifierCount", _identifierCount);
    }

    /// <summary>
    /// Counts one element/entry of a list/map/message literal being validated against
    /// <see cref="CelCompilationLimits.MaxLiteralSize"/>, matching its documented "element count
    /// for list/map literals" contract (string/bytes literals are bounded by content length
    /// instead, in the tokenizer).
    /// </summary>
    private void TrackLiteralElement(CelSourceSpan span, int count)
    {
        if (count > _limits.MaxLiteralSize)
            throw FailBudget(span, "MaxLiteralSize", count);
    }

    private static CelSourceSpan Merge(CelSourceSpan a, CelSourceSpan b) =>
        new(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));

    private CelParseException Fail(CelSourceSpan span, string message) =>
        new(CelParseDiagnostics.SyntaxError(span, message, _profileId));

    private CelParseException FailBudget(CelSourceSpan span, string limitName, long observedValue) =>
        new(CelParseDiagnostics.BudgetExceeded(span, limitName, observedValue, _profileId));

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "S3871:Exception types should be \"public\"",
        Justification = "Internal control-flow signal local to CelParser's recursive-descent " +
            "parse; never crosses the assembly boundary and is always caught inside Parse() " +
            "before a result is returned.")]
    private sealed class CelParseException : Exception
    {
        public CelDiagnostic Diagnostic { get; }

        public CelParseException(CelDiagnostic diagnostic) => Diagnostic = diagnostic;
    }
}
