namespace ArchLinterNet.CEL;

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
        maxIdentifierCount: 64);

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
    /// Creates a custom <see cref="CelCompilationLimits"/> instance.
    /// </summary>
    public CelCompilationLimits(int maxExpressionLength, int maxNestingDepth, int maxIdentifierCount)
    {
        if (maxExpressionLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExpressionLength));
        if (maxNestingDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxNestingDepth));
        if (maxIdentifierCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIdentifierCount));

        MaxExpressionLength = maxExpressionLength;
        MaxNestingDepth = maxNestingDepth;
        MaxIdentifierCount = maxIdentifierCount;
    }

    /// <summary>Returns a stable identity string for use in <see cref="CelCompilationKey"/>.</summary>
    internal string ComputeIdentity() =>
        $"len={MaxExpressionLength},nest={MaxNestingDepth},ids={MaxIdentifierCount}";
}
