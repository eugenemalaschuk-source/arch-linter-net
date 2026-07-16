namespace ArchLinterNet.CEL.Diagnostics;

/// <summary>
/// Stable, machine-readable code identifying the category of a <see cref="CelDiagnostic"/>.
/// </summary>
/// <remarks>
/// Display messages in <see cref="CelDiagnostic.Message"/> are for human consumption only;
/// code consumers must check <see cref="CelDiagnosticCode"/> for programmatic branching.
/// </remarks>
public enum CelDiagnosticCode
{
    /// <summary>The expression source contains a syntax error.</summary>
    SyntaxError,

    /// <summary>The expression uses a CEL feature not supported by the active profile.</summary>
    UnsupportedFeature,

    /// <summary>An identifier, member, or function could not be resolved against the schema.</summary>
    BindingError,

    /// <summary>A type mismatch was detected during type checking.</summary>
    TypeMismatch,

    /// <summary>A schema or value mismatch was detected (e.g. wrong kind supplied to a variable).</summary>
    SchemaMismatch,

    /// <summary>A compilation or evaluation budget was exceeded.</summary>
    BudgetExceeded,

    /// <summary>Evaluation produced a runtime failure that is not a budget issue.</summary>
    EvaluationFailure,

    /// <summary>
    /// The requested operation is not yet implemented. Retained as a published enum member for
    /// compatibility even though the current Profile v1 compile/evaluate flow no longer emits it.
    /// </summary>
    NotYetImplemented,
}
