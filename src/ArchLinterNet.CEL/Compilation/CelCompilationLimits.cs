namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// Immutable compilation budget limits applied to a <see cref="CelEnvironment"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every compilation path in <c>ArchLinterNet.CEL</c> is intrinsically bounded.
/// No unbounded compilation overload exists.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelCompilationLimits
{
    /// <summary>
    /// Gets a pre-configured instance with conservative safe defaults suitable for
    /// policy-linting workloads. Use this for the vast majority of use cases.
    /// </summary>
    public static readonly CelCompilationLimits SafeDefaults = new(
        maxExpressionLength: 4096,
        maxNestingDepth: 32,
        maxIdentifierCount: 64,
        maxTokenCount: 2048,
        maxAstNodeCount: 1024,
        maxLiteralSize: 1024);

    /// <summary>
    /// Gets the maximum number of UTF-16 characters accepted in an expression source string.
    /// </summary>
    public int MaxExpressionLength { get; }

    /// <summary>
    /// Gets the maximum nesting depth for sub-expressions (parentheses, member access chains, etc.).
    /// </summary>
    public int MaxNestingDepth { get; }

    /// <summary>
    /// Gets the maximum number of distinct identifier references (variable and function names)
    /// permitted within a single expression.
    /// </summary>
    public int MaxIdentifierCount { get; }

    /// <summary>
    /// Gets the maximum number of tokens the tokenizer may produce for a single expression.
    /// Reserved: not yet enforced until the tokenizer (#325) lands.
    /// </summary>
    public int MaxTokenCount { get; }

    /// <summary>
    /// Gets the maximum number of AST nodes the parser may produce for a single expression.
    /// Reserved: not yet enforced until the parser (#325) lands.
    /// </summary>
    public int MaxAstNodeCount { get; }

    /// <summary>
    /// Gets the maximum size (in UTF-16 characters, for string literals; in element count, for
    /// list/map literals) of any single literal accepted in source. Reserved: not yet enforced
    /// until the parser (#325) lands.
    /// </summary>
    public int MaxLiteralSize { get; }

    /// <summary>
    /// Creates a custom <see cref="CelCompilationLimits"/> instance.
    /// </summary>
    public CelCompilationLimits(
        int maxExpressionLength,
        int maxNestingDepth,
        int maxIdentifierCount,
        int maxTokenCount,
        int maxAstNodeCount,
        int maxLiteralSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExpressionLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIdentifierCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokenCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAstNodeCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLiteralSize);

        MaxExpressionLength = maxExpressionLength;
        MaxNestingDepth = maxNestingDepth;
        MaxIdentifierCount = maxIdentifierCount;
        MaxTokenCount = maxTokenCount;
        MaxAstNodeCount = maxAstNodeCount;
        MaxLiteralSize = maxLiteralSize;
    }

    /// <summary>Returns a stable identity string for use in <see cref="CelCompilationKey"/>.</summary>
    internal string ComputeIdentity() =>
        $"len={MaxExpressionLength},nest={MaxNestingDepth},ids={MaxIdentifierCount}," +
        $"tok={MaxTokenCount},ast={MaxAstNodeCount},lit={MaxLiteralSize}";
}
