using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>The result of running <see cref="CelTokenizer"/> over one expression source.</summary>
internal sealed class CelTokenizeResult
{
    /// <summary>Gets a value indicating whether tokenization succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the produced token stream (always terminated by an <see cref="CelTokenKind.Eof"/>
    /// token), or empty when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public IReadOnlyList<CelToken> Tokens { get; }

    /// <summary>Gets the diagnostic explaining failure, or <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public CelDiagnostic? Diagnostic { get; }

    private CelTokenizeResult(bool isSuccess, IReadOnlyList<CelToken> tokens, CelDiagnostic? diagnostic)
    {
        IsSuccess = isSuccess;
        Tokens = tokens;
        Diagnostic = diagnostic;
    }

    public static CelTokenizeResult Success(IReadOnlyList<CelToken> tokens) => new(true, tokens, null);

    public static CelTokenizeResult Failed(CelDiagnostic diagnostic) => new(false, Array.Empty<CelToken>(), diagnostic);
}
