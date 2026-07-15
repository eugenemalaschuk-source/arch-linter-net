using System.Globalization;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// Converts a CEL expression source string into a token stream for <see cref="CelParser"/>.
/// </summary>
/// <remarks>
/// Tokenizes every token kind normative for the pinned CEL grammar, including constructs deferred
/// by Profile v1 (arithmetic operators, <c>null</c>/<c>uint</c>/byte-string literals), so the
/// parser can distinguish valid-but-deferred CEL syntax from genuinely invented syntax. See the
/// tokenizer/parser implementation-scope decisions in
/// <c>openspec/changes/2026-07-15-cel-tokenizer-parser/design.md</c>.
/// </remarks>
internal static class CelTokenizer
{
    private static readonly HashSet<string> _reservedWords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "else", "for", "function", "if", "import",
        "let", "loop", "package", "namespace", "return", "var", "void", "while",
    };

    public static CelTokenizeResult Tokenize(string source, CelCompilationLimits limits, CelProfileId profileId)
    {
        var tokens = new List<CelToken>();
        var pos = 0;

        while (true)
        {
            SkipWhitespaceAndComments(source, ref pos);

            if (pos >= source.Length)
            {
                tokens.Add(new CelToken(CelTokenKind.Eof, new CelSourceSpan(pos, pos), string.Empty));
                break;
            }

            var start = pos;
            var c = source[pos];
            CelToken? token;
            CelDiagnostic? error;

            if (TryMatchStringPrefix(source, pos, out var prefixIsRaw, out var prefixIsBytes, out var prefixLength))
            {
                pos += prefixLength;
                (token, error) = LexString(source, ref pos, start, isRaw: prefixIsRaw, isBytes: prefixIsBytes, profileId);
            }
            else if (c is '\'' or '"')
            {
                (token, error) = LexString(source, ref pos, start, isRaw: false, isBytes: false, profileId);
            }
            else if (IsAsciiDigit(c) || (c == '.' && pos + 1 < source.Length && IsAsciiDigit(source[pos + 1])))
            {
                (token, error) = LexNumber(source, ref pos, profileId);
            }
            else if (IsIdentifierStart(c))
            {
                (token, error) = LexIdentifierOrKeyword(source, ref pos);
            }
            else
            {
                (token, error) = LexPunctuation(source, ref pos, profileId);
            }

            if (error is not null)
                return CelTokenizeResult.Failed(error);
            if (token is null)
                return CelTokenizeResult.Failed(CelParseDiagnostics.SyntaxError(
                    new CelSourceSpan(start, start + 1), $"Unexpected character '{c}'.", profileId));

            if (token.Kind is CelTokenKind.StringLiteral or CelTokenKind.BytesLiteral
                && token.StringValue is not null && token.StringValue.Length > limits.MaxLiteralSize)
            {
                return CelTokenizeResult.Failed(CelParseDiagnostics.BudgetExceeded(
                    token.Span, "MaxLiteralSize", token.StringValue.Length, profileId));
            }

            tokens.Add(token);
            if (tokens.Count > limits.MaxTokenCount)
            {
                return CelTokenizeResult.Failed(CelParseDiagnostics.BudgetExceeded(
                    token.Span, "MaxTokenCount", tokens.Count, profileId));
            }
        }

        return CelTokenizeResult.Success(tokens);
    }

    private static void SkipWhitespaceAndComments(string source, ref int pos)
    {
        while (pos < source.Length)
        {
            var c = source[pos];
            if (IsWhitespace(c))
            {
                pos++;
                continue;
            }

            if (c == '/' && pos + 1 < source.Length && source[pos + 1] == '/')
            {
                pos += 2;
                while (pos < source.Length && source[pos] != '\n')
                    pos++;
                continue;
            }

            break;
        }
    }

    // The pinned grammar's WHITESPACE token is exactly [\t\n\f\r ] — not char.IsWhiteSpace(),
    // which also accepts non-ASCII Unicode space separators (e.g. NBSP U+00A0) and other
    // whitespace-category characters (e.g. vertical tab U+000B) the grammar does not include.
    private static bool IsWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';

    /// <summary>
    /// Matches a string-literal prefix per the pinned grammar's
    /// <c>BYTES_LIT : ("b"|"B") STRING_LIT</c> / <c>STRING_LIT : ["r"|"R"] STRING</c> productions:
    /// an optional byte marker (<c>b</c>/<c>B</c>) followed by an optional raw marker
    /// (<c>r</c>/<c>R</c>), immediately followed by the opening quote — e.g. <c>r'...'</c>,
    /// <c>b'...'</c>, and combined <c>br'...'</c>/<c>bR'...'</c>/<c>Br'...'</c>/<c>BR'...'</c>.
    /// The reverse order (<c>rb"..."</c>) has no lexical form in the pinned grammar and is not
    /// matched here — it is left to fall through to <see cref="IsIdentifierStart"/> and produce
    /// an ordinary (malformed) parse, consistent with genuinely invented syntax.
    /// </summary>
    private static bool TryMatchStringPrefix(string source, int pos, out bool isRaw, out bool isBytes, out int prefixLength)
    {
        isRaw = false;
        isBytes = false;
        var p = pos;

        if (p < source.Length && source[p] is 'b' or 'B')
        {
            isBytes = true;
            p++;
        }

        if (p < source.Length && source[p] is 'r' or 'R')
        {
            isRaw = true;
            p++;
        }

        if (p > pos && p < source.Length && source[p] is '\'' or '"')
        {
            prefixLength = p - pos;
            return true;
        }

        prefixLength = 0;
        return false;
    }

    // The pinned CEL grammar restricts IDENT to ASCII: [_a-zA-Z][_a-zA-Z0-9]*. char.IsLetter/
    // IsLetterOrDigit would accept Unicode letters, which is not valid CEL syntax.
    private static bool IsIdentifierStart(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || (c >= '0' && c <= '9');

    private static bool IsHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    // DIGIT in the pinned CEL grammar is [0-9] only; char.IsDigit(c) would also accept
    // non-ASCII Unicode decimal digits (e.g. Arabic-indic), which is not valid CEL syntax.
    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static (CelToken?, CelDiagnostic?) LexIdentifierOrKeyword(string source, ref int pos)
    {
        var start = pos;
        pos++;
        while (pos < source.Length && IsIdentifierPart(source[pos]))
            pos++;
        var text = source[start..pos];
        var span = new CelSourceSpan(start, pos);

        return text switch
        {
            "true" => (new CelToken(CelTokenKind.BoolLiteral, span, text, boolValue: true), null),
            "false" => (new CelToken(CelTokenKind.BoolLiteral, span, text, boolValue: false), null),
            "null" => (new CelToken(CelTokenKind.NullLiteral, span, text), null),
            "in" => (new CelToken(CelTokenKind.In, span, text), null),
            _ => (new CelToken(CelTokenKind.Identifier, span, text, stringValue: text, isReserved: _reservedWords.Contains(text)), null),
        };
    }

    private static (CelToken?, CelDiagnostic?) LexNumber(string source, ref int pos, CelProfileId profileId)
    {
        var start = pos;

        if (source[pos] == '0' && pos + 1 < source.Length && source[pos + 1] is 'x' or 'X')
            return LexHexInteger(source, ref pos, start, profileId);

        var sawDot = false;
        var sawExp = false;

        if (source[pos] == '.')
        {
            sawDot = true;
            pos++;
        }

        while (pos < source.Length && IsAsciiDigit(source[pos]))
            pos++;

        // The pinned grammar requires at least one digit after the decimal point
        // (DIGIT+ "." DIGIT+ | DIGIT* "." DIGIT+) — "3." alone is not a valid FLOAT_LIT.
        if (!sawDot && pos < source.Length && source[pos] == '.'
            && pos + 1 < source.Length && IsAsciiDigit(source[pos + 1]))
        {
            sawDot = true;
            pos++;
            while (pos < source.Length && IsAsciiDigit(source[pos]))
                pos++;
        }

        if (pos < source.Length && source[pos] is 'e' or 'E')
        {
            var save = pos;
            pos++;
            if (pos < source.Length && source[pos] is '+' or '-')
                pos++;
            var digitsStart = pos;
            while (pos < source.Length && IsAsciiDigit(source[pos]))
                pos++;
            if (pos > digitsStart)
                sawExp = true;
            else
                pos = save;
        }

        var span = new CelSourceSpan(start, pos);
        var text = source[start..pos];

        if (sawDot || sawExp)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dval))
                return (null, CelParseDiagnostics.SyntaxError(span, "Malformed floating-point literal.", profileId));
            return (new CelToken(CelTokenKind.FloatLiteral, span, text, floatValue: dval), null);
        }

        var isUnsigned = false;
        if (pos < source.Length && source[pos] is 'u' or 'U')
        {
            isUnsigned = true;
            pos++;
            span = new CelSourceSpan(start, pos);
            text = source[start..pos];
        }

        var digitsOnly = isUnsigned ? text[..^1] : text;
        if (!ulong.TryParse(digitsOnly, NumberStyles.None, CultureInfo.InvariantCulture, out var uval))
            return (null, CelParseDiagnostics.SyntaxError(span, "Integer literal out of range.", profileId));

        if (isUnsigned)
            return (new CelToken(CelTokenKind.UintLiteral, span, text, intValue: unchecked((long)uval)), null);

        if (uval > long.MaxValue)
            return (null, CelParseDiagnostics.SyntaxError(span, "Integer literal out of range.", profileId));
        return (new CelToken(CelTokenKind.IntLiteral, span, text, intValue: (long)uval), null);
    }

    private static (CelToken?, CelDiagnostic?) LexHexInteger(string source, ref int pos, int start, CelProfileId profileId)
    {
        pos += 2;
        var hexStart = pos;
        while (pos < source.Length && IsHexDigit(source[pos]))
            pos++;
        if (pos == hexStart)
            return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Malformed hex integer literal.", profileId));

        var hexDigits = source[hexStart..pos];
        var isUnsigned = false;
        if (pos < source.Length && source[pos] is 'u' or 'U')
        {
            isUnsigned = true;
            pos++;
        }

        var span = new CelSourceSpan(start, pos);
        var text = source[start..pos];
        if (!ulong.TryParse(hexDigits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var uval))
            return (null, CelParseDiagnostics.SyntaxError(span, "Hex integer literal out of range.", profileId));

        if (isUnsigned)
            return (new CelToken(CelTokenKind.UintLiteral, span, text, intValue: unchecked((long)uval)), null);

        if (uval > long.MaxValue)
            return (null, CelParseDiagnostics.SyntaxError(span, "Hex integer literal out of range.", profileId));
        return (new CelToken(CelTokenKind.IntLiteral, span, text, intValue: (long)uval), null);
    }

    private static (CelToken?, CelDiagnostic?) LexString(
        string source, ref int pos, int start, bool isRaw, bool isBytes, CelProfileId profileId)
    {
        var quote = source[pos];
        pos++;
        var sb = new System.Text.StringBuilder();

        while (true)
        {
            if (pos >= source.Length)
                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Unterminated string literal.", profileId));

            var c = source[pos];
            if (c == quote)
            {
                pos++;
                break;
            }

            if (c is '\n' or '\r')
                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Unterminated string literal (unescaped newline).", profileId));

            if (c == '\\' && !isRaw)
            {
                var (ok, error) = AppendEscape(source, ref pos, sb, start, isBytes, profileId);
                if (!ok)
                    return (null, error);
                continue;
            }

            sb.Append(c);
            pos++;
        }

        var span = new CelSourceSpan(start, pos);
        var text = source[start..pos];
        var kind = isBytes ? CelTokenKind.BytesLiteral : CelTokenKind.StringLiteral;
        return (new CelToken(kind, span, text, stringValue: sb.ToString()), null);
    }

    private static (bool, CelDiagnostic?) AppendEscape(
        string source, ref int pos, System.Text.StringBuilder sb, int literalStart, bool isBytes, CelProfileId profileId)
    {
        pos++; // consume '\'
        if (pos >= source.Length)
            return (false, CelParseDiagnostics.SyntaxError(new CelSourceSpan(literalStart, pos), "Unterminated escape sequence.", profileId));

        var esc = source[pos];
        switch (esc)
        {
            case 'n': sb.Append('\n'); pos++; return (true, null);
            case 't': sb.Append('\t'); pos++; return (true, null);
            case 'r': sb.Append('\r'); pos++; return (true, null);
            case '\\': sb.Append('\\'); pos++; return (true, null);
            case '\'': sb.Append('\''); pos++; return (true, null);
            case '"': sb.Append('"'); pos++; return (true, null);
            case '`': sb.Append('`'); pos++; return (true, null);
            case '?': sb.Append('?'); pos++; return (true, null);
            case 'a': sb.Append('\a'); pos++; return (true, null);
            case 'b': sb.Append('\b'); pos++; return (true, null);
            case 'f': sb.Append('\f'); pos++; return (true, null);
            case 'v': sb.Append('\v'); pos++; return (true, null);
            case 'x': case 'X': return AppendHexByteEscape(source, ref pos, sb, literalStart, profileId);
            // Unlike '\U' (below), the pinned grammar's byte-string escape set includes '\u' —
            // only the 8-digit '\U' form is string-only.
            case 'u': return AppendUnicode4Escape(source, ref pos, sb, literalStart, profileId);
            case 'U':
                if (isBytes)
                {
                    return (false, CelParseDiagnostics.SyntaxError(
                        new CelSourceSpan(literalStart, pos + 1), "'\\U' is not a valid escape sequence in a byte-string literal.", profileId));
                }

                return AppendUnicodeEscape(source, ref pos, sb, literalStart, profileId);
            default:
                return (false, CelParseDiagnostics.SyntaxError(
                    new CelSourceSpan(literalStart, pos + 1), $"Unknown escape sequence '\\{esc}'.", profileId));
        }
    }

    private static (bool, CelDiagnostic?) AppendHexByteEscape(
        string source, ref int pos, System.Text.StringBuilder sb, int literalStart, CelProfileId profileId)
    {
        pos++; // consume 'x'
        var digitsStart = pos;
        while (pos < source.Length && pos - digitsStart < 2 && IsHexDigit(source[pos]))
            pos++;
        if (pos - digitsStart != 2)
        {
            return (false, CelParseDiagnostics.SyntaxError(
                new CelSourceSpan(literalStart, pos), "Malformed \\x escape sequence: expected 2 hex digits.", profileId));
        }

        sb.Append((char)Convert.ToInt32(source[digitsStart..pos], 16));
        return (true, null);
    }

    private static (bool, CelDiagnostic?) AppendUnicode4Escape(
        string source, ref int pos, System.Text.StringBuilder sb, int literalStart, CelProfileId profileId)
    {
        pos++; // consume 'u'
        var digitsStart = pos;
        while (pos < source.Length && pos - digitsStart < 4 && IsHexDigit(source[pos]))
            pos++;
        if (pos - digitsStart != 4)
        {
            return (false, CelParseDiagnostics.SyntaxError(
                new CelSourceSpan(literalStart, pos), "Malformed \\u escape sequence: expected 4 hex digits.", profileId));
        }

        var codepoint = Convert.ToInt32(source[digitsStart..pos], 16);
        if (codepoint is >= 0xD800 and <= 0xDFFF)
        {
            return (false, CelParseDiagnostics.SyntaxError(
                new CelSourceSpan(literalStart, pos), "\\u escape sequence must not encode a surrogate code point.", profileId));
        }

        sb.Append((char)codepoint);
        return (true, null);
    }

    private static (bool, CelDiagnostic?) AppendUnicodeEscape(
        string source, ref int pos, System.Text.StringBuilder sb, int literalStart, CelProfileId profileId)
    {
        pos++; // consume 'U'
        var digitsStart = pos;
        while (pos < source.Length && pos - digitsStart < 8 && IsHexDigit(source[pos]))
            pos++;
        if (pos - digitsStart != 8)
        {
            return (false, CelParseDiagnostics.SyntaxError(
                new CelSourceSpan(literalStart, pos), "Malformed \\U escape sequence: expected 8 hex digits.", profileId));
        }

        var codepoint = Convert.ToInt32(source[digitsStart..pos], 16);
        if (codepoint > 0x10FFFF || (codepoint >= 0xD800 && codepoint <= 0xDFFF))
        {
            return (false, CelParseDiagnostics.SyntaxError(
                new CelSourceSpan(literalStart, pos), "\\U escape sequence is outside the valid Unicode range.", profileId));
        }

        sb.Append(char.ConvertFromUtf32(codepoint));
        return (true, null);
    }

    private static (CelToken?, CelDiagnostic?) LexPunctuation(string source, ref int pos, CelProfileId profileId)
    {
        var start = pos;
        var c = source[pos];
        pos++;

        switch (c)
        {
            case '.': return (Simple(CelTokenKind.Dot, start, pos, "."), null);
            case ',': return (Simple(CelTokenKind.Comma, start, pos, ","), null);
            case '(': return (Simple(CelTokenKind.LParen, start, pos, "("), null);
            case ')': return (Simple(CelTokenKind.RParen, start, pos, ")"), null);
            case '[': return (Simple(CelTokenKind.LBracket, start, pos, "["), null);
            case ']': return (Simple(CelTokenKind.RBracket, start, pos, "]"), null);
            case '{': return (Simple(CelTokenKind.LBrace, start, pos, "{"), null);
            case '}': return (Simple(CelTokenKind.RBrace, start, pos, "}"), null);
            case '?': return (Simple(CelTokenKind.Question, start, pos, "?"), null);
            case ':': return (Simple(CelTokenKind.Colon, start, pos, ":"), null);
            case '+': return (Simple(CelTokenKind.Plus, start, pos, "+"), null);
            case '-': return (Simple(CelTokenKind.Minus, start, pos, "-"), null);
            case '*': return (Simple(CelTokenKind.Star, start, pos, "*"), null);
            case '/': return (Simple(CelTokenKind.Slash, start, pos, "/"), null);
            case '%': return (Simple(CelTokenKind.Percent, start, pos, "%"), null);
            case '!':
                if (pos < source.Length && source[pos] == '=')
                {
                    pos++;
                    return (Simple(CelTokenKind.NotEq, start, pos, "!="), null);
                }

                return (Simple(CelTokenKind.Bang, start, pos, "!"), null);
            case '=':
                if (pos < source.Length && source[pos] == '=')
                {
                    pos++;
                    return (Simple(CelTokenKind.EqEq, start, pos, "=="), null);
                }

                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Unexpected character '='.", profileId));
            case '<':
                if (pos < source.Length && source[pos] == '=')
                {
                    pos++;
                    return (Simple(CelTokenKind.LtEq, start, pos, "<="), null);
                }

                return (Simple(CelTokenKind.Lt, start, pos, "<"), null);
            case '>':
                if (pos < source.Length && source[pos] == '=')
                {
                    pos++;
                    return (Simple(CelTokenKind.GtEq, start, pos, ">="), null);
                }

                return (Simple(CelTokenKind.Gt, start, pos, ">"), null);
            case '&':
                if (pos < source.Length && source[pos] == '&')
                {
                    pos++;
                    return (Simple(CelTokenKind.AmpAmp, start, pos, "&&"), null);
                }

                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Unexpected character '&'.", profileId));
            case '|':
                if (pos < source.Length && source[pos] == '|')
                {
                    pos++;
                    return (Simple(CelTokenKind.PipePipe, start, pos, "||"), null);
                }

                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), "Unexpected character '|'.", profileId));
            default:
                return (null, CelParseDiagnostics.SyntaxError(new CelSourceSpan(start, pos), $"Unexpected character '{c}'.", profileId));
        }
    }

    private static CelToken Simple(CelTokenKind kind, int start, int end, string text) =>
        new(kind, new CelSourceSpan(start, end), text);
}
