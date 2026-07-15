namespace ArchLinterNet.CEL.Diagnostics;

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

    /// <summary>
    /// Gets structured, machine-readable parameters for this diagnostic, keyed by stable
    /// parameter names (e.g. <c>expectedType</c>, <c>actualType</c>, <c>identifier</c>,
    /// <c>limitName</c>, <c>observedValue</c>). Machine consumers SHOULD read these instead of
    /// parsing <see cref="Message"/>. Empty when a diagnostic carries no parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    private static readonly IReadOnlyDictionary<string, string> _emptyParameters =
        new Dictionary<string, string>().AsReadOnly();

    internal CelDiagnostic(
        CelDiagnosticCode code,
        string category,
        CelDiagnosticSeverity severity,
        CelSourceSpan? span,
        string message,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        Code = code;
        Category = category;
        Severity = severity;
        Span = span;
        Message = message;
        // Copy to a truly frozen dictionary so callers cannot cast Parameters back and mutate it.
        Parameters = parameters is null || parameters.Count == 0
            ? _emptyParameters
            : new Dictionary<string, string>(parameters, StringComparer.Ordinal).AsReadOnly();
    }

    /// <inheritdoc/>
    public override string ToString() => Span.HasValue
        ? $"[{Severity}] {Code} at {Span}: {Message}"
        : $"[{Severity}] {Code}: {Message}";
}
