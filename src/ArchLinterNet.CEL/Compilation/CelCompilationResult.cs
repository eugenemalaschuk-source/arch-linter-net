using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// The structured result of compiling a CEL expression via <see cref="CelEnvironment"/>.
/// </summary>
/// <typeparam name="T">
/// The compiled program type — either <see cref="CelCompiledPredicate"/> or
/// <see cref="CelCompiledExpression"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// Check <see cref="IsSuccess"/> before reading <see cref="Program"/>. When
/// <see cref="IsSuccess"/> is <c>false</c>, <see cref="Diagnostics"/> describes the failure
/// and <see cref="Program"/> is <c>null</c>.
/// </para>
/// </remarks>
public sealed class CelCompilationResult<T> where T : class
{
    /// <summary>Gets a value indicating whether compilation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the compiled, immutable, thread-safe program, or <c>null</c> when
    /// <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public T? Program { get; }

    /// <summary>Gets any diagnostics produced during compilation.</summary>
    public IReadOnlyList<CelDiagnostic> Diagnostics { get; }

    /// <summary>Gets the cache-identity key for this compilation attempt.</summary>
    public CelCompilationKey CompilationKey { get; }

    internal CelCompilationResult(bool isSuccess, T? program, IReadOnlyList<CelDiagnostic> diagnostics, CelCompilationKey key)
    {
        IsSuccess = isSuccess;
        Program = program;
        // Copy to a truly frozen list so callers cannot cast Diagnostics back to T[]/List<> and mutate it.
        Diagnostics = new List<CelDiagnostic>(diagnostics).AsReadOnly();
        CompilationKey = key;
    }

    internal static CelCompilationResult<T> BudgetExceeded(CelCompilationKey key) =>
        new(
            isSuccess: false,
            program: null,
            diagnostics:
            [
                new CelDiagnostic(
                    CelDiagnosticCode.BudgetExceeded,
                    "limits",
                    CelDiagnosticSeverity.Error,
                    span: null,
                    $"Expression length {key.NormalizedSource.Length} exceeds MaxExpressionLength limit " +
                    $"(profile '{key.ProfileId}').",
                    parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["limitName"] = "MaxExpressionLength",
                        ["observedValue"] = key.NormalizedSource.Length.ToString(),
                        ["profileId"] = key.ProfileId.Value,
                    }),
            ],
            key: key);

}
