namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// Identifies the required CEL result type for a compilation, used as part of
/// <see cref="CelCompilationKey"/> cache identity.
/// </summary>
public enum CelRequiredResultType
{
    /// <summary>The expression must evaluate to a <c>bool</c> value (predicate path).</summary>
    Predicate,

    /// <summary>The expression may evaluate to any CEL value (general path).</summary>
    General,
}
