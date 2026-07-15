using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Tests for <see cref="CelParser"/>'s deferred-CEL-feature handling and structural-limit
/// enforcement (#325): UnsupportedFeature vs SyntaxError classification for deferred constructs
/// (arithmetic, conditional, list/map/message literals, root-qualified names), unary prefix-chain
/// rules, and MaxNestingDepth/MaxAstNodeCount/MaxIdentifierCount/MaxLiteralSize adversarial tests.
/// Split out of <c>CelParserTests</c> to keep both files under the repository size threshold.
/// </summary>
[TestFixture]
public sealed class CelParserDeferredFeatureTests
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

    // ── Deferred-but-valid CEL syntax: UnsupportedFeature, not SyntaxError ────

    [TestCase("a + b")]
    [TestCase("a - b")]
    [TestCase("a * b")]
    [TestCase("a / b")]
    [TestCase("a % b")]
    public void ArithmeticOperator_IsUnsupportedFeature(string source)
    {
        var diag = ParseFail(source);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void NegativeLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("-5 == 5");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ConditionalOperator_IsUnsupportedFeature()
    {
        var diag = ParseFail("a ? b : c");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ConditionalOperator_WithArithmeticInTrueBranch_IsUnsupportedFeature()
    {
        // The true branch must be fully validated (including any arithmetic it contains) via
        // the real grammar, not a truncated ParseOr() that would misreport this as a missing ':'.
        var diag = ParseFail("a ? b + c : d");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ConditionalOperator_WithArithmeticThenComparisonInTrueBranch_IsUnsupportedFeature()
    {
        // Pinned grammar: Relation = Addition [Relop Addition] — arithmetic must be absorbed at
        // the Addition level *before* a comparison operator is looked for, not only recognized
        // as a flat trailer after the whole ConditionalOr has already returned.
        var diag = ParseFail("a ? b + c == d : e");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ArithmeticInsideComparisonOperand_IsUnsupportedFeature()
    {
        var diag = ParseFail("a + b == c");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ArithmeticOnBothSidesOfComparison_IsUnsupportedFeature()
    {
        var diag = ParseFail("a + b == c + d");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void UnterminatedParenthesesContainingArithmetic_IsSyntaxError()
    {
        // The deferred-arithmetic classification must not preempt validating that the enclosing
        // parenthesized sub-expression is actually closed.
        var diag = ParseFail("(a + b");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ParenthesizedArithmetic_FollowedByComparison_IsUnsupportedFeature()
    {
        var diag = ParseFail("(a + b) == c");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void UnterminatedCallContainingArithmeticArgument_IsSyntaxError()
    {
        var diag = ParseFail("f(a + b");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void RootQualifiedMessageLiteralWithNonIdentifierFieldKey_IsSyntaxError()
    {
        // A root-qualified name's trailing "{...}" must get the same bare-identifier-field-key
        // validation as a non-root-qualified message literal, not bypass it entirely.
        var diag = ParseFail(".pkg.Type{1: 2}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ConditionalOperator_WithArithmeticInFalseBranch_IsUnsupportedFeature()
    {
        var diag = ParseFail("a ? b : c + d");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ConditionalOperator_WithNestedTernaryInFalseBranch_IsUnsupportedFeature()
    {
        // Pinned grammar: Expr = ConditionalOr ["?" ConditionalOr ":" Expr] — the false branch is
        // the full recursive Expr, so an unparenthesized nested ternary there is valid CEL syntax.
        var diag = ParseFail("a ? b : c ? d : e");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ConditionalOperator_WithUnparenthesizedNestedTernaryInTrueBranch_IsSyntaxError()
    {
        // The true branch is ConditionalOr only (no unparenthesized nested ternary allowed there
        // per the grammar) — the inner '?' is not consumed, so the outer ':' expectation fails.
        var diag = ParseFail("a ? b ? c : d : e");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ConditionalOperator_WithParenthesizedNestedTernaryInTrueBranch_IsUnsupportedFeature()
    {
        var diag = ParseFail("a ? (b ? c : d) : e");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void NullLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("x == null");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void UintLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("42u");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void BytesLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("b'abc'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ListLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("[1, 2, 3]");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void MapLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("{'a': 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void MessageLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("Type{field: 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void QualifiedMessageLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail("pkg.Type{field: 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void RootQualifiedName_IsUnsupportedFeature()
    {
        var diag = ParseFail(".pkg.Type");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void RootQualifiedMessageLiteral_IsUnsupportedFeature()
    {
        var diag = ParseFail(".pkg.Type{field: 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void MessageLiteralWithNonIdentifierFieldKey_IsSyntaxError()
    {
        // Message-literal field keys must be bare identifiers (field names) — unlike map
        // literals, an arbitrary expression key is not valid CEL for a message literal.
        var diag = ParseFail("Type{1: 2}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void MessageLiteralWithStringFieldKey_IsSyntaxError()
    {
        var diag = ParseFail("Type{'field': 1}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void MapLiteralWithNonIdentifierKey_IsStillUnsupportedFeature()
    {
        // Standalone "{" (no qualified-name receiver) is a map literal — arbitrary-expression
        // keys are valid CEL for it, unlike a message literal.
        var diag = ParseFail("{1: 2}");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    // ── Unary prefix chains: '!'/'-' each self-chain but never mix ────────────

    [Test]
    public void MixedBangThenMinus_IsSyntaxError()
    {
        var diag = ParseFail("!-x");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void MixedMinusThenBang_IsSyntaxError()
    {
        var diag = ParseFail("-!x");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void TripleBangChain_StillParsesAsNestedNot()
    {
        var node = (CelUnarySyntax)ParseOk("!!!x");
        Assert.That(node.Operand, Is.TypeOf<CelUnarySyntax>());
        Assert.That(((CelUnarySyntax)node.Operand).Operand, Is.TypeOf<CelUnarySyntax>());
    }

    [Test]
    public void TripleMinusChain_IsStillUnsupportedFeature()
    {
        var diag = ParseFail("---x");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    // ── Arithmetic is deferred at every nesting level, not only top-level ─────

    [Test]
    public void ArithmeticInsideCallArgument_IsUnsupportedFeature()
    {
        var diag = ParseFail("f(a + b)");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ArithmeticInsideParentheses_IsUnsupportedFeature()
    {
        var diag = ParseFail("(a + b)");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void ArithmeticInsideIndexExpression_IsUnsupportedFeature()
    {
        var diag = ParseFail("items[a + b]");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    // ── Structural limits (adversarial) ────────────────────────────────────────

    [Test]
    public void MaxNestingDepth_Exceeded_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 3, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = ParseFail("!!!!!x", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxNestingDepth"));
    }

    [Test]
    public void MaxNestingDepth_ExceededByParenNesting_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 3, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = ParseFail("(((((x)))))", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxNestingDepth"));
    }

    [Test]
    public void MaxNestingDepth_ExceededByMemberAccessChain_ReturnsBudgetExceeded()
    {
        // The public MaxNestingDepth doc explicitly lists "member access chains" as an example
        // of what the limit bounds — a long non-parenthesized chain must trip it too.
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 3, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = ParseFail("a.b.c.d.e", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxNestingDepth"));
    }

    [Test]
    public void MaxNestingDepth_ExceededByIndexChain_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 3, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = ParseFail("a[0][0][0][0]", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxNestingDepth"));
    }

    [Test]
    public void MaxIdentifierCount_Exceeded_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 1,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = ParseFail("a || b", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxIdentifierCount"));
    }

    [Test]
    public void MaxIdentifierCount_CountsMemberNamesAndCallNames()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 2,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        // "x" (1) + "startsWith" (2) — the third identifier ("y") must exceed the limit.
        var diag = ParseFail("x.startsWith(y)", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxIdentifierCount"));
    }

    [Test]
    public void MaxIdentifierCount_WithinLimit_Parses()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 2,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var node = (CelBinarySyntax)ParseOk("a || b", limits);
        Assert.That(node.Operator, Is.EqualTo(CelBinaryOperator.Or));
    }

    [Test]
    public void MaxAstNodeCount_Exceeded_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 3, maxLiteralSize: 1024);

        var diag = ParseFail("a == b && c == d", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxAstNodeCount"));
    }

    [Test]
    public void MaxLiteralSize_Exceeded_ByListLiteralElementCount_ReturnsBudgetExceeded()
    {
        // MaxLiteralSize is documented as bounding element count for list/map literals — even
        // though they're deferred (UnsupportedFeature), the validation pass must still respect it.
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 3);

        var diag = ParseFail("[1, 2, 3, 4, 5]", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxLiteralSize"));
    }

    [Test]
    public void MaxLiteralSize_Exceeded_ByMapLiteralEntryCount_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 2);

        var diag = ParseFail("{'a': 1, 'b': 2, 'c': 3}", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxLiteralSize"));
    }

    [Test]
    public void MaxLiteralSize_Exceeded_ByMessageLiteralFieldCount_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 2048, maxAstNodeCount: 1024, maxLiteralSize: 2);

        var diag = ParseFail("Type{a: 1, b: 2, c: 3}", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxLiteralSize"));
    }

    // ── profileId / category on every diagnostic ──────────────────────────────

    [Test]
    public void ParserDiagnostic_CarriesProfileIdAndParserCategory()
    {
        var diag = ParseFail("a => b");
        Assert.That(diag.Category, Is.EqualTo("parser"));
        Assert.That(diag.Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
    }

    // ── AST immutability: cast-and-mutate regression ──────────────────────────

    [Test]
    public void CallSyntax_Arguments_CannotMutateThroughCast()
    {
        var node = (CelCallSyntax)ParseOk("f(a, b)");

        Assert.That(node.Arguments as List<CelSyntaxNode>, Is.Null,
            "Arguments must not be a mutable List<> that can be mutated via cast.");
        Assert.That(node.Arguments as CelSyntaxNode[], Is.Null,
            "Arguments must not be a raw array that can be mutated via cast.");
    }
}
