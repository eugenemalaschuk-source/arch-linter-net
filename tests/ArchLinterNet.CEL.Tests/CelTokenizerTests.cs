using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Focused tests for <see cref="CelTokenizer"/>: every token kind, string-escape handling,
/// malformed-input diagnostics, and structural-limit enforcement (#325).
/// </summary>
[TestFixture]
public sealed class CelTokenizerTests
{
    private static IReadOnlyList<CelToken> TokenizeOk(string source, CelCompilationLimits? limits = null)
    {
        var result = CelTokenizer.Tokenize(source, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.True, $"Expected tokenization to succeed for '{source}' but got: {result.Diagnostic}");
        return result.Tokens;
    }

    private static CelDiagnostic TokenizeFail(string source, CelCompilationLimits? limits = null)
    {
        var result = CelTokenizer.Tokenize(source, limits ?? CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False, $"Expected tokenization to fail for '{source}'");
        Assert.That(result.Diagnostic, Is.Not.Null);
        return result.Diagnostic!;
    }

    // ── Identifiers, keywords, reserved words ─────────────────────────────────

    [Test]
    public void Identifier_Tokenizes()
    {
        var tokens = TokenizeOk("source_1");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.Identifier));
        Assert.That(tokens[0].StringValue, Is.EqualTo("source_1"));
        Assert.That(tokens[0].IsReserved, Is.False);
    }

    [Test]
    public void ReservedIdentifier_IsFlagged()
    {
        var tokens = TokenizeOk("if");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.Identifier));
        Assert.That(tokens[0].IsReserved, Is.True);
    }

    [Test]
    public void NonAsciiLetter_IsNotPartOfAnIdentifier()
    {
        // The pinned grammar restricts IDENT to ASCII: [_a-zA-Z][_a-zA-Z0-9]*. "é" must not be
        // absorbed into an identifier — it tokenizes as an unexpected character instead.
        var result = CelTokenizer.Tokenize("é", CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostic!.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void NonAsciiLetter_TerminatesAPrecedingIdentifier()
    {
        // "a" followed by "é" must tokenize as identifier "a" then fail on "é", not as one
        // 2-character identifier "aé".
        var tokens = TokenizeOk("a");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.Identifier));
        Assert.That(tokens[0].StringValue, Is.EqualTo("a"));

        var result = CelTokenizer.Tokenize("aé", CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False);
    }

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void BoolLiteral_Tokenizes(string source, bool expected)
    {
        var tokens = TokenizeOk(source);
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BoolLiteral));
        Assert.That(tokens[0].BoolValue, Is.EqualTo(expected));
    }

    [Test]
    public void NullKeyword_TokenizesAsNullLiteral()
    {
        var tokens = TokenizeOk("null");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.NullLiteral));
    }

    [Test]
    public void InKeyword_TokenizesAsIn()
    {
        var tokens = TokenizeOk("in");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.In));
    }

    // ── Numeric literals ───────────────────────────────────────────────────────

    [TestCase("0", 0L)]
    [TestCase("123", 123L)]
    [TestCase("0x1A", 26L)]
    [TestCase("0XFF", 255L)]
    public void IntLiteral_Tokenizes(string source, long expected)
    {
        var tokens = TokenizeOk(source);
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.IntLiteral));
        Assert.That(tokens[0].IntValue, Is.EqualTo(expected));
    }

    [Test]
    public void UintLiteral_Tokenizes()
    {
        var tokens = TokenizeOk("42u");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.UintLiteral));
        Assert.That(tokens[0].IntValue, Is.EqualTo(42L));
    }

    [TestCase("1.5", 1.5)]
    [TestCase("0.5", 0.5)]
    [TestCase("1e10", 1e10)]
    [TestCase("1.5e-3", 1.5e-3)]
    [TestCase(".5", 0.5)]
    public void FloatLiteral_Tokenizes(string source, double expected)
    {
        var tokens = TokenizeOk(source);
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.FloatLiteral));
        Assert.That(tokens[0].FloatValue, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void TrailingDotWithNoFollowingDigit_IsNotAFloatLiteral()
    {
        // The pinned grammar requires DIGIT+ after the decimal point; "3." alone is not a
        // valid FLOAT_LIT — it must tokenize as an IntLiteral followed by a separate Dot.
        var tokens = TokenizeOk("3.");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.IntLiteral));
        Assert.That(tokens[0].IntValue, Is.EqualTo(3));
        Assert.That(tokens[1].Kind, Is.EqualTo(CelTokenKind.Dot));
    }

    [Test]
    public void IntLiteral_OutOfRange_IsSyntaxError()
    {
        var diag = TokenizeFail("99999999999999999999999999");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void HexIntLiteral_Malformed_IsSyntaxError()
    {
        var diag = TokenizeFail("0x");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── String literals ────────────────────────────────────────────────────────

    [TestCase("'hello'", "hello")]
    [TestCase("\"hello\"", "hello")]
    [TestCase(@"'a\nb'", "a\nb")]
    [TestCase(@"'a\tb'", "a\tb")]
    [TestCase(@"'a\\b'", "a\\b")]
    [TestCase(@"'a\'b'", "a'b")]
    [TestCase(@"'\x41'", "A")]
    [TestCase(@"'A'", "A")]
    [TestCase(@"'\U0001F600'", "\U0001F600")]
    public void StringLiteral_DecodesEscapes(string source, string expected)
    {
        var tokens = TokenizeOk(source);
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.StringLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo(expected));
    }

    [Test]
    public void RawStringLiteral_DoesNotProcessEscapes()
    {
        var tokens = TokenizeOk(@"r'a\nb'");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.StringLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo(@"a\nb"));
    }

    [Test]
    public void ByteStringLiteral_Tokenizes()
    {
        var tokens = TokenizeOk("b'abc'");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BytesLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo("abc"));
    }

    [Test]
    public void CombinedRawByteStringLiteral_Tokenizes()
    {
        // BYTES_LIT : ("b"|"B") STRING_LIT ; STRING_LIT : ["r"|"R"] STRING — "br'...'" is a raw
        // byte-string literal; escapes must not be processed.
        var tokens = TokenizeOk(@"br'a\nb'");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BytesLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo(@"a\nb"));
    }

    [Test]
    public void CombinedRawByteStringLiteral_UppercaseVariant_Tokenizes()
    {
        var tokens = TokenizeOk("BR'abc'");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BytesLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo("abc"));
    }

    [Test]
    public void ByteStringLiteral_HexEscapeUppercaseX_Tokenizes()
    {
        var tokens = TokenizeOk(@"b'\X41'");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BytesLiteral));
        Assert.That(tokens[0].StringValue, Is.EqualTo("A"));
    }

    [Test]
    public void ByteStringLiteral_UnicodeEscape_IsSyntaxError()
    {
        // \u and \U decode a Unicode code point, which has no meaning for a raw byte sequence —
        // only \x/\X (byte-value escapes) are valid inside a byte-string literal.
        var source = "b'" + "\\u0041" + "'";
        var diag = TokenizeFail(source);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void ByteStringLiteral_LongUnicodeEscape_IsSyntaxError()
    {
        var diag = TokenizeFail(@"b'\U00000041'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnterminatedStringLiteral_IsSyntaxError()
    {
        var diag = TokenizeFail("'unterminated");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_NewlineInsideSingleQuotes_IsSyntaxError()
    {
        var diag = TokenizeFail("'a\nb'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_UnknownEscape_IsSyntaxError()
    {
        var diag = TokenizeFail(@"'\q'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_MalformedHexEscape_IsSyntaxError()
    {
        var diag = TokenizeFail(@"'\xZZ'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_MalformedUnicodeEscape_IsSyntaxError()
    {
        var diag = TokenizeFail(@"'\u12'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_SurrogateRangeUnicodeEscape_IsSyntaxError()
    {
        var diag = TokenizeFail(@"'\U0000D800'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_SurrogateRangeShortUnicodeEscape_IsSyntaxError()
    {
        // \uD800 alone (not paired) is not a valid Unicode scalar value / CEL code point.
        var diag = TokenizeFail(@"'\uD800'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void StringLiteral_NullEscape_IsUnknownEscape_NotOctal()
    {
        // CEL has no standalone "\0" escape (only three-digit octal, which is out of v1 scope
        // per design.md decision 3) — "\0" must be rejected, not silently treated as NUL.
        var diag = TokenizeFail(@"'\0'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── Punctuation and operators ──────────────────────────────────────────────

    [Test]
    public void Punctuation_Tokenizes()
    {
        var cases = new (string Source, CelTokenKind Expected)[]
        {
            (".", CelTokenKind.Dot),
            (",", CelTokenKind.Comma),
            ("(", CelTokenKind.LParen),
            (")", CelTokenKind.RParen),
            ("[", CelTokenKind.LBracket),
            ("]", CelTokenKind.RBracket),
            ("{", CelTokenKind.LBrace),
            ("}", CelTokenKind.RBrace),
            ("!", CelTokenKind.Bang),
            ("&&", CelTokenKind.AmpAmp),
            ("||", CelTokenKind.PipePipe),
            ("==", CelTokenKind.EqEq),
            ("!=", CelTokenKind.NotEq),
            ("<", CelTokenKind.Lt),
            ("<=", CelTokenKind.LtEq),
            (">", CelTokenKind.Gt),
            (">=", CelTokenKind.GtEq),
            ("?", CelTokenKind.Question),
            (":", CelTokenKind.Colon),
            ("+", CelTokenKind.Plus),
            ("-", CelTokenKind.Minus),
            ("*", CelTokenKind.Star),
            ("/", CelTokenKind.Slash),
            ("%", CelTokenKind.Percent),
        };

        foreach (var (source, expected) in cases)
        {
            var tokens = TokenizeOk(source);
            Assert.That(tokens[0].Kind, Is.EqualTo(expected), $"source: '{source}'");
        }
    }

    [TestCase("=")]
    [TestCase("&")]
    [TestCase("|")]
    [TestCase("~")]
    [TestCase("^")]
    [TestCase("`")]
    public void InventedOrUnsupportedCharacter_IsSyntaxError(string source)
    {
        var diag = TokenizeFail(source);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── Comments and whitespace ────────────────────────────────────────────────

    [Test]
    public void LineComment_IsSkipped()
    {
        var tokens = TokenizeOk("true // trailing comment\n");
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.BoolLiteral));
        Assert.That(tokens[1].Kind, Is.EqualTo(CelTokenKind.Eof));
    }

    [Test]
    public void Whitespace_IsSkippedBetweenTokens()
    {
        var tokens = TokenizeOk("  true   ==   false  ");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CelTokenKind.BoolLiteral, CelTokenKind.EqEq, CelTokenKind.BoolLiteral, CelTokenKind.Eof,
        }));
    }

    [Test]
    public void CarriageReturn_IsSkippedAsWhitespace()
    {
        var tokens = TokenizeOk("true\r\n==\rfalse");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CelTokenKind.BoolLiteral, CelTokenKind.EqEq, CelTokenKind.BoolLiteral, CelTokenKind.Eof,
        }));
    }

    [Test]
    public void NonBreakingSpace_IsNotValidWhitespace()
    {
        // The pinned grammar defines whitespace as only tab, newline, form-feed, carriage
        // return, and space -- U+00A0 (NBSP) is not part of it, even though
        // char.IsWhiteSpace(c) considers it whitespace.
        var source = "true" + (char)0x00A0 + "==" + (char)0x00A0 + "false";
        var result = CelTokenizer.Tokenize(source, CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostic!.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void VerticalTab_IsNotValidWhitespace()
    {
        // U+000B (vertical tab) is whitespace per char.IsWhiteSpace() but not part of the
        // pinned grammar's whitespace definition.
        var source = "true" + (char)0x000B + "==" + (char)0x000B + "false";
        var result = CelTokenizer.Tokenize(source, CelCompilationLimits.SafeDefaults, CelProfile.V1.Id);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostic!.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void UnescapedCarriageReturn_InsideStringLiteral_IsSyntaxError()
    {
        var diag = TokenizeFail("'a\rb'");
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    // ── Source spans ────────────────────────────────────────────────────────────

    [Test]
    public void Token_HasCorrectSourceSpan()
    {
        var tokens = TokenizeOk("  abc");
        Assert.That(tokens[0].Span.Start, Is.EqualTo(2));
        Assert.That(tokens[0].Span.End, Is.EqualTo(5));
    }

    [Test]
    public void EmptySource_ProducesOnlyEofToken()
    {
        var tokens = TokenizeOk(string.Empty);
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CelTokenKind.Eof));
    }

    // ── Structural limits (adversarial) ────────────────────────────────────────

    [Test]
    public void MaxTokenCount_Exceeded_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 3, maxAstNodeCount: 1024, maxLiteralSize: 1024);

        var diag = TokenizeFail("true == false && true", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxTokenCount"));
    }

    [Test]
    public void MaxLiteralSize_Exceeded_ReturnsBudgetExceeded()
    {
        var limits = new CelCompilationLimits(
            maxExpressionLength: 4096, maxNestingDepth: 32, maxIdentifierCount: 64,
            maxTokenCount: 1024, maxAstNodeCount: 1024, maxLiteralSize: 4);

        var diag = TokenizeFail("'too long a string literal'", limits);
        Assert.That(diag.Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(diag.Parameters["limitName"], Is.EqualTo("MaxLiteralSize"));
    }

    // ── profileId on every diagnostic ──────────────────────────────────────────

    [Test]
    public void TokenizerDiagnostic_CarriesProfileIdAndParserCategory()
    {
        var diag = TokenizeFail("'unterminated");
        Assert.That(diag.Category, Is.EqualTo("parser"));
        Assert.That(diag.Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
    }
}
