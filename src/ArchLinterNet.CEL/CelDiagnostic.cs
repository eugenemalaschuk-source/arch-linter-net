namespace ArchLinterNet.CEL;

/// <summary>
/// A structured diagnostic produced during CEL compilation or evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Code"/> for programmatic branching. <see cref="Message"/> is a display-only
/// string and is NOT a machine contract — its wording may change across releases.
/// </para>
/// </remarks>
public sealed class CelDiagnostic
{
    /// <summary>Gets the stable, machine-readable diagnostic code.</summary>
    public CelDiagnosticCode Code { get; }

    /// <summary>Gets the broad category label for this diagnostic.</summary>
    public string Category { get; }

    /// <summary>Gets the severity of this diagnostic.</summary>
    public CelDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the source span identifying the expression range this diagnostic refers to,
    /// or <c>null</c> if the diagnostic is not tied to a specific source location.
    /// </summary>
    public CelSourceSpan? Span { get; }

    /// <summary>
    /// Gets a human-readable description of the diagnostic. Not a machine contract.
    /// </summary>
    public string Message { get; }

    internal CelDiagnostic(
        CelDiagnosticCode code,
        string category,
        CelDiagnosticSeverity severity,
        CelSourceSpan? span,
        string message)
    {
        Code = code;
        Category = category;
        Severity = severity;
        Span = span;
        Message = message;
    }

    /// <inheritdoc/>
    public override string ToString() => Span.HasValue
        ? $"[{Severity}] {Code} at {Span}: {Message}"
        : $"[{Severity}] {Code}: {Message}";
}
