namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// Identifies the lexical kind of a <see cref="CelToken"/>.
/// </summary>
/// <remarks>
/// Includes token kinds for CEL constructs deferred by Profile v1 (e.g. arithmetic operators,
/// <c>null</c>/<c>uint</c>/byte-string literals) so the tokenizer can recognize valid-but-excluded
/// CEL syntax and let the parser report <c>UnsupportedFeature</c> instead of <c>SyntaxError</c>.
/// </remarks>
internal enum CelTokenKind
{
    /// <summary>End of input.</summary>
    Eof,

    /// <summary>An identifier, possibly a reserved word (see <see cref="CelToken.IsReserved"/>).</summary>
    Identifier,

    /// <summary>The <c>true</c> or <c>false</c> keyword literal.</summary>
    BoolLiteral,

    /// <summary>The <c>null</c> keyword literal. Deferred: Profile v1 has no null value.</summary>
    NullLiteral,

    /// <summary>The <c>in</c> keyword operator.</summary>
    In,

    /// <summary>A signed 64-bit integer literal (decimal or hex).</summary>
    IntLiteral,

    /// <summary>A <c>u</c>/<c>U</c>-suffixed unsigned integer literal. Deferred in Profile v1.</summary>
    UintLiteral,

    /// <summary>An IEEE 754 double-precision floating-point literal.</summary>
    FloatLiteral,

    /// <summary>A single/double-quoted or raw string literal.</summary>
    StringLiteral,

    /// <summary>
    /// A <c>'''...'''</c>/<c>"""..."""</c> triple-quoted string literal. Deferred: triple-quote
    /// lexing is out of scope for Profile v1.
    /// </summary>
    TripleQuotedStringLiteral,

    /// <summary>
    /// A single/double-quoted string literal containing a well-formed three-digit octal escape
    /// sequence (<c>\NNN</c>). Deferred: octal escape decoding is out of scope for Profile v1
    /// (the containing string literal as a whole defers, even if it also uses otherwise-supported
    /// escapes). A malformed octal sequence (not exactly three octal digits, or out of the
    /// <c>\000</c>-<c>\377</c> byte range) is <c>SyntaxError</c>, not this kind.
    /// </summary>
    StringLiteralWithOctalEscape,

    /// <summary>A <c>b"..."</c>/<c>B"..."</c> byte-string literal. Deferred: Profile v1 has no Bytes type.</summary>
    BytesLiteral,

    /// <summary><c>.</c></summary>
    Dot,

    /// <summary><c>,</c></summary>
    Comma,

    /// <summary><c>(</c></summary>
    LParen,

    /// <summary><c>)</c></summary>
    RParen,

    /// <summary><c>[</c></summary>
    LBracket,

    /// <summary><c>]</c></summary>
    RBracket,

    /// <summary><c>{</c>. Deferred: Profile v1 has no map/message literal syntax.</summary>
    LBrace,

    /// <summary><c>}</c>. Deferred: Profile v1 has no map/message literal syntax.</summary>
    RBrace,

    /// <summary><c>!</c></summary>
    Bang,

    /// <summary><c>&amp;&amp;</c></summary>
    AmpAmp,

    /// <summary><c>||</c></summary>
    PipePipe,

    /// <summary><c>==</c></summary>
    EqEq,

    /// <summary><c>!=</c></summary>
    NotEq,

    /// <summary><c>&lt;</c></summary>
    Lt,

    /// <summary><c>&lt;=</c></summary>
    LtEq,

    /// <summary><c>&gt;</c></summary>
    Gt,

    /// <summary><c>&gt;=</c></summary>
    GtEq,

    /// <summary><c>?</c>. Deferred: part of the conditional operator.</summary>
    Question,

    /// <summary><c>:</c>. Deferred: part of the conditional operator.</summary>
    Colon,

    /// <summary><c>+</c>. Deferred: arithmetic is excluded from Profile v1.</summary>
    Plus,

    /// <summary><c>-</c>. Deferred: arithmetic is excluded from Profile v1.</summary>
    Minus,

    /// <summary><c>*</c>. Deferred: arithmetic is excluded from Profile v1.</summary>
    Star,

    /// <summary><c>/</c>. Deferred: arithmetic is excluded from Profile v1.</summary>
    Slash,

    /// <summary><c>%</c>. Deferred: arithmetic is excluded from Profile v1.</summary>
    Percent,
}
