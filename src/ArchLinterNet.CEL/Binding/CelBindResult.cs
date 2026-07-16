using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Binding;

/// <summary>The result of running <see cref="CelBinder"/> over one successfully parsed syntax tree.</summary>
internal sealed class CelBindResult
{
    /// <summary>Gets a value indicating whether binding and type-checking succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the bound expression tree, or <c>null</c> when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public CelBoundExpression? Bound { get; }

    /// <summary>Gets the diagnostic explaining failure, or <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public CelDiagnostic? Diagnostic { get; }

    private CelBindResult(bool isSuccess, CelBoundExpression? bound, CelDiagnostic? diagnostic)
    {
        IsSuccess = isSuccess;
        Bound = bound;
        Diagnostic = diagnostic;
    }

    public static CelBindResult Success(CelBoundExpression bound) => new(true, bound, null);

    public static CelBindResult Failed(CelDiagnostic diagnostic) => new(false, null, diagnostic);
}
