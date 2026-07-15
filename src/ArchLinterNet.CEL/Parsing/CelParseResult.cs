using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>The result of running <see cref="CelParser"/> over one token stream.</summary>
internal sealed class CelParseResult
{
    /// <summary>Gets a value indicating whether parsing succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the root syntax node, or <c>null</c> when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public CelSyntaxNode? Root { get; }

    /// <summary>Gets the diagnostic explaining failure, or <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public CelDiagnostic? Diagnostic { get; }

    private CelParseResult(bool isSuccess, CelSyntaxNode? root, CelDiagnostic? diagnostic)
    {
        IsSuccess = isSuccess;
        Root = root;
        Diagnostic = diagnostic;
    }

    public static CelParseResult Success(CelSyntaxNode root) => new(true, root, null);

    public static CelParseResult Failed(CelDiagnostic diagnostic) => new(false, null, diagnostic);
}
