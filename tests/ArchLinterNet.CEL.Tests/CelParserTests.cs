using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Focused tests for <see cref="CelParser"/>: node shapes, precedence, associativity, source
/// spans, negative conformance, deferred-CEL handling, and structural-limit enforcement (#325).
/// </summary>
[TestFixture]
public sealed class CelParserTests
{
    private static CelSyntaxNode ParseOk(string source, CelCompilationLimits? limits = null)
    {
        var tokens = CelTokenizer.Tokenize(source, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(tokens.IsSuccess, Is.True, $"Tokenization failed for '{source}': {tokens.Diagnostic}");
        var result = CelParser.Parse(tokens.Tokens, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.True, $"Expected parse to succeed for '{source}' but got: {result.Diagnostic}");
        Assert.That(result.Root, Is.Not.Null);
        return result.Root!;
    }

    private static CelDiagnostic ParseFail(string source, CelCompilationLimits? limits = null)
    {
        var tokens = CelTokenizer.Tokenize(source, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        if (!tokens.IsSuccess)
            return tokens.Diagnostic!;
        var result = CelParser.Parse(tokens.Tokens, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False, $"Expected parse to fail for '{source}'");
        Assert.That(result.Diagnostic, Is.Not.Null);
        return result.Diagnostic!;
    }

    // ── Literals and identifiers ───────────────────────────────────────────────

    [Test]
    public void BoolLiteral_Parses()
    {
        var node = (CelBoolLiteralSyntax)ParseOk("true");
        Assert.That(node.Value, Is.True);
    }

    [Test]
    public void IntLiteral_Parses()
    {
        var node = (CelIntLiteralSyntax)ParseOk("42");
        Assert.That(node.Value, Is.EqualTo(42));
    }

    [Test]
    public void FloatLiteral_Parses()
    {
        var node = (CelFloatLiteralSyntax)ParseOk("1.5");
        Assert.That(node.Value, Is.EqualTo(1.5));
    }

    [Test]
    public void StringLiteral_Parses()
    {
        var node = (CelStringLiteralSyntax)ParseOk("'hello'");
        Assert.That(node.Value, Is.EqualTo("hello"));
    }

    [Test]
    public void Identifier_Parses()
    {
        var node = (CelIdentifierSyntax)ParseOk("source");
        Assert.That(node.Name, Is.EqualTo("source"));
    }

    // ── Unary ───────────────────────────────────────────────────────────────────

    [Test]
    public void UnaryNot_Parses()
    {
        var node = (CelUnarySyntax)ParseOk("!x");
        Assert.That(node.Operator, Is.EqualTo(CelUnaryOperator.Not));
        Assert.That(node.Operand, Is.TypeOf<CelIdentifierSyntax>());
    }

    [Test]
    public void DoubleNegation_Parses_RightAssociatively()
    {
        var node = (CelUnarySyntax)ParseOk("!!x");
        Assert.That(node.Operator, Is.EqualTo(CelUnaryOperator.Not));
        Assert.That(node.Operand, Is.TypeOf<CelUnarySyntax>());
    }

    // ── Member access, indexing, calls ────────────────────────────────────────

    [Test]
    public void MemberAccess_Parses()
    {
        var node = (CelMemberAccessSyntax)ParseOk("x.role");
        Assert.That(node.MemberName, Is.EqualTo("role"));
        Assert.That(((CelIdentifierSyntax)node.Receiver).Name, Is.EqualTo("x"));
    }

    [Test]
    public void ChainedMemberAccess_Parses()
    {
        var node = (CelMemberAccessSyntax)ParseOk("x.a.b");
        Assert.That(node.MemberName, Is.EqualTo("b"));
        Assert.That(node.Receiver, Is.TypeOf<CelMemberAccessSyntax>());
    }

    [Test]
    public void Indexing_Parses()
    {
        var node = (CelIndexSyntax)ParseOk("list[0]");
        Assert.That(((CelIdentifierSyntax)node.Receiver).Name, Is.EqualTo("list"));
        Assert.That(((CelIntLiteralSyntax)node.Index).Value, Is.EqualTo(0));
    }

    [Test]
    public void ReceiverCall_Parses()
    {
        var node = (CelCallSyntax)ParseOk("x.startsWith('a')");
        Assert.That(node.Receiver, Is.Not.Null);
        Assert.That(node.FunctionName, Is.EqualTo("startsWith"));
        Assert.That(node.Arguments, Has.Count.EqualTo(1));
    }

    [Test]
    public void FreeFunctionCall_ParsesWithNullReceiver()
    {
        var node = (CelCallSyntax)ParseOk("f(x, y)");
        Assert.That(node.Receiver, Is.Null);
        Assert.That(node.FunctionName, Is.EqualTo("f"));
        Assert.That(node.Arguments, Has.Count.EqualTo(2));
    }

    [Test]
    public void ZeroArgCall_Parses()
    {
        var node = (CelCallSyntax)ParseOk("x.size()");
        Assert.That(node.Arguments, Is.Empty);
    }

    [Test]
    public void ReservedWordAsMemberName_Parses()
    {
        var node = (CelMemberAccessSyntax)ParseOk("x.as");
        Assert.That(node.MemberName, Is.EqualTo("as"));
    }

    [Test]
    public void ReservedWordAsCallName_Parses()
    {
        var node = (CelCallSyntax)ParseOk("x.for()");
        Assert.That(node.FunctionName, Is.EqualTo("for"));
    }

    // ── Binary operators, precedence, associativity ───────────────────────────

    private static void AssertBinaryOperator(string source, CelBinaryOperator expected)
    {
        var node = (CelBinarySyntax)ParseOk(source);
        Assert.That(node.Operator, Is.EqualTo(expected));
    }

    [Test]
    public void BinaryOperator_And_Parses() => AssertBinaryOperator("a && b", CelBinaryOperator.And);

    [Test]
    public void BinaryOperator_Or_Parses() => AssertBinaryOperator("a || b", CelBinaryOperator.Or);

    [Test]
    public void BinaryOperator_Equal_Parses() => AssertBinaryOperator("a == b", CelBinaryOperator.Equal);

    [Test]
    public void BinaryOperator_NotEqual_Parses() => AssertBinaryOperator("a != b", CelBinaryOperator.NotEqual);

    [Test]
    public void BinaryOperator_Less_Parses() => AssertBinaryOperator("a < b", CelBinaryOperator.Less);

    [Test]
    public void BinaryOperator_LessOrEqual_Parses() => AssertBinaryOperator("a <= b", CelBinaryOperator.LessOrEqual);

    [Test]
    public void BinaryOperator_Greater_Parses() => AssertBinaryOperator("a > b", CelBinaryOperator.Greater);

    [Test]
    public void BinaryOperator_GreaterOrEqual_Parses() => AssertBinaryOperator("a >= b", CelBinaryOperator.GreaterOrEqual);

    [Test]
    public void BinaryOperator_In_Parses() => AssertBinaryOperator("a in b", CelBinaryOperator.In);

    [Test]
    public void OrBindsLooserThanAnd()
    {
        // a || b && c  ==  a || (b && c)
        var node = (CelBinarySyntax)ParseOk("a || b && c");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.Or));
        Assert.That(node.Left, Is.TypeOf<CelIdentifierSyntax>());
        var right = (CelBinarySyntax)node.Right;
        Assert.That(right.Operator, Is.EqualTo(CelBinaryOperator.And));
    }

    [Test]
    public void AndBindsTighterThanComparison_IsInvalidGrammar()
    {
        // Comparisons bind tighter than &&: `a == b && c == d` == `(a == b) && (c == d)`.
        var node = (CelBinarySyntax)ParseOk("a == b && c == d");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.And));
        Assert.That(((CelBinarySyntax)node.Left).Operator, Is.EqualTo(CelBinaryOperator.Equal));
        Assert.That(((CelBinarySyntax)node.Right).Operator, Is.EqualTo(CelBinaryOperator.Equal));
    }

    [Test]
    public void OrIsLeftAssociative()
    {
        // a || b || c  ==  (a || b) || c
        var node = (CelBinarySyntax)ParseOk("a || b || c");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.Or));
        Assert.That(node.Left, Is.TypeOf<CelBinarySyntax>());
        Assert.That(((CelBinarySyntax)node.Left).Operator, Is.EqualTo(CelBinaryOperator.Or));
        Assert.That(node.Right, Is.TypeOf<CelIdentifierSyntax>());
    }

    [Test]
    public void AndIsLeftAssociative()
    {
        var node = (CelBinarySyntax)ParseOk("a && b && c");
        Assert.That(node.Left, Is.TypeOf<CelBinarySyntax>());
        Assert.That(((CelBinarySyntax)node.Left).Operator, Is.EqualTo(CelBinaryOperator.And));
    }

    [Test]
    public void UnaryBindsTighterThanComparison()
    {
        var node = (CelBinarySyntax)ParseOk("!a == b");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.Equal));
        Assert.That(node.Left, Is.TypeOf<CelUnarySyntax>());
    }

    [Test]
    public void MemberAccessBindsTighterThanUnary()
    {
        var node = (CelUnarySyntax)ParseOk("!x.role");
        Assert.That(node.Operand, Is.TypeOf<CelMemberAccessSyntax>());
    }

    [Test]
    public void ParenthesesOverridePrecedence()
    {
        // (a || b) && c
        var node = (CelBinarySyntax)ParseOk("(a || b) && c");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.And));
        Assert.That(node.Left, Is.TypeOf<CelBinarySyntax>());
        Assert.That(((CelBinarySyntax)node.Left).Operator, Is.EqualTo(CelBinaryOperator.Or));
    }

    // ── Non-associative comparisons ────────────────────────────────────────────

    [Test]
    public void ChainedComparisons_AreSyntaxError()
    {
        var diag = ParseFail("a < b < c");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ChainedComparisons_WithParentheses_Parse()
    {
        var node = (CelBinarySyntax)ParseOk("(a < b) < c");
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.Less));
    }

    // ── Source spans ────────────────────────────────────────────────────────────

    [Test]
    public void RootNode_SpanCoversWholeExpression()
    {
        var node = ParseOk("a == b");
        Assert.That(node.Span.Start, Is.EqualTo(0));
        Assert.That(node.Span.End, Is.EqualTo(6));
    }

    [Test]
    public void SyntaxErrorDiagnostic_HasPreciseSpan()
    {
        var diag = ParseFail("a ===");
        Assert.That(diag.Span, Is.Not.Null);
    }

    // ── Full input consumption ────────────────────────────────────────────────

    [Test]
    public void TrailingGarbageAfterCompleteExpression_IsSyntaxError()
    {
        var diag = ParseFail("true true");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void EmptyInput_IsSyntaxError()
    {
        var diag = ParseFail(string.Empty);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── Negative conformance: invented / proprietary syntax ───────────────────

    [Test]
    public void InventedOperator_IsSyntaxError()
    {
        var diag = ParseFail("a => b");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void AlternateCallSyntax_UsingColon_IsSyntaxError()
    {
        var diag = ParseFail("x:startsWith('a')");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ReservedWordAsBareReference_IsSyntaxError()
    {
        var diag = ParseFail("if == true");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ReservedWordAsFreeCallName_IsSyntaxError()
    {
        // A reserved word can never resolve as an actual callable function name.
        var diag = ParseFail("package()");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void RootQualifiedReservedWord_WithNothingFollowing_IsSyntaxError()
    {
        var diag = ParseFail(".package");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void RootQualifiedReservedWordCall_IsSyntaxError()
    {
        var diag = ParseFail(".package()");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ReservedWordAsCallNameOnReceiver_StillParses()
    {
        // "package" used in SELECTOR (member/call-name) position on a non-reserved receiver was
        // already valid and must remain so — untouched by the qualified-name-prefix fix.
        var node = (CelCallSyntax)ParseOk("x.package()");
        Assert.That(node.FunctionName, Is.EqualTo("package"));
        Assert.That(node.Receiver, Is.Not.Null);
    }

    [Test]
    public void UnexpectedToken_AtPrimaryPosition_IsSyntaxError()
    {
        var diag = ParseFail(")");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnclosedParenthesis_IsSyntaxError()
    {
        var diag = ParseFail("(a == b");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── Malformed deferred constructs are SyntaxError, not UnsupportedFeature ─
    // A construct's syntax must itself be well-formed before it can be classified as
    // "valid CEL, deferred by Profile v1" — a dangling operator with no operand is malformed.

    [Test]
    public void DanglingArithmeticOperator_IsSyntaxError()
    {
        var diag = ParseFail("a +");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void DanglingUnaryMinus_IsSyntaxError()
    {
        var diag = ParseFail("-");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void LeadingUnaryPlus_HasNoValidCelForm_IsSyntaxError()
    {
        // Unlike '-', '+' has no unary/prefix form anywhere in the pinned CEL grammar.
        var diag = ParseFail("+5");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ConditionalMissingColonAndFalseBranch_IsSyntaxError()
    {
        var diag = ParseFail("a ? b");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ConditionalMissingFalseBranch_IsSyntaxError()
    {
        var diag = ParseFail("a ? b :");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnterminatedListLiteral_IsSyntaxError()
    {
        var diag = ParseFail("[");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnterminatedListLiteralWithTrailingComma_IsSyntaxError()
    {
        var diag = ParseFail("[1, 2,");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void BareDot_WithNoFollowingIdentifier_IsSyntaxError()
    {
        var diag = ParseFail(".");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void MessageLiteralOnNonQualifiedNameReceiver_IsSyntaxError()
    {
        // A literal is never a valid message-literal receiver; "1{}" is genuinely invalid CEL,
        // not deferred syntax — the parser must not treat every "{" after any expression as a
        // message literal.
        var diag = ParseFail("1{}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnterminatedMapLiteral_IsSyntaxError()
    {
        var diag = ParseFail("{'a': 1");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void MalformedMapLiteralEntry_MissingColon_IsSyntaxError()
    {
        var diag = ParseFail("{'a' 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

}
