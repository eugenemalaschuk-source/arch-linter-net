using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// A single lexical token produced by <see cref="CelTokenizer"/>.
/// </summary>
internal sealed class CelToken
{
    /// <summary>Gets the lexical kind of this token.</summary>
    public CelTokenKind Kind { get; }

    /// <summary>Gets the exact source span this token occupies.</summary>
    public CelSourceSpan Span { get; }

    /// <summary>Gets the raw source text of this token.</summary>
    public string Text { get; }

    /// <summary>
    /// Gets the decoded identifier name, or decoded string/byte-string literal content.
    /// <c>null</c> for token kinds that carry no text payload.
    /// </summary>
    public string? StringValue { get; }

    /// <summary>Gets the decoded integer value for <see cref="CelTokenKind.IntLiteral"/> or <see cref="CelTokenKind.UintLiteral"/>.</summary>
    public long IntValue { get; }

    /// <summary>Gets the decoded floating-point value for <see cref="CelTokenKind.FloatLiteral"/>.</summary>
    public double FloatValue { get; }

    /// <summary>Gets the decoded boolean value for <see cref="CelTokenKind.BoolLiteral"/>.</summary>
    public bool BoolValue { get; }

    /// <summary>
    /// Gets a value indicating whether an <see cref="CelTokenKind.Identifier"/> token's text is a
    /// CEL reserved identifier (valid in member-selector position, invalid as a bare reference).
    /// </summary>
    public bool IsReserved { get; }

    public CelToken(
        CelTokenKind kind,
        CelSourceSpan span,
        string text,
        string? stringValue = null,
        long intValue = 0,
        double floatValue = 0,
        bool boolValue = false,
        bool isReserved = false)
    {
        Kind = kind;
        Span = span;
        Text = text;
        StringValue = stringValue;
        IntValue = intValue;
        FloatValue = floatValue;
        BoolValue = boolValue;
        IsReserved = isReserved;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}({Text}){Span}";
}
