using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// The structured result of evaluating a compiled CEL expression against an
/// <see cref="CelEvaluationContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Check <see cref="IsSuccess"/> before reading <see cref="Value"/>. When <see cref="IsSuccess"/>
/// is <c>false</c>, <see cref="Diagnostics"/> describes the failure and <see cref="Value"/> is
/// <c>null</c>.
/// </para>
/// </remarks>
public sealed class CelEvaluationResult
{
    /// <summary>Gets a value indicating whether evaluation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the evaluated CEL value, or <c>null</c> if <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public CelValue? Value { get; }

    /// <summary>Gets any diagnostics produced during evaluation (non-empty when <see cref="IsSuccess"/> is <c>false</c>).</summary>
    public IReadOnlyList<CelDiagnostic> Diagnostics { get; }

    internal CelEvaluationResult(bool isSuccess, CelValue? value, IReadOnlyList<CelDiagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        Value = value;
        // Copy to a truly frozen list so callers cannot cast Diagnostics back to T[]/List<> and mutate it.
        Diagnostics = new List<CelDiagnostic>(diagnostics).AsReadOnly();
    }

    /// <summary>
    /// Returns the evaluation result as a boolean.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="IsSuccess"/> is <c>false</c>, or if <see cref="Value"/> is not of kind
    /// <see cref="CelValueKind.Bool"/>.
    /// </exception>
    public bool AsBool()
    {
        if (!IsSuccess)
            throw new InvalidOperationException("Cannot read AsBool() from a failed evaluation result.");
        if (Value is null || Value.Kind != CelValueKind.Bool)
            throw new InvalidOperationException(
                $"Cannot read AsBool() from an evaluation result of kind {Value?.Kind.ToString() ?? "null"}.");
        return Value.AsBool();
    }
}
