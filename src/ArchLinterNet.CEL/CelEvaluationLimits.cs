namespace ArchLinterNet.CEL;

/// <summary>
/// Immutable evaluation budget limits applied per <see cref="CelCompiledPredicate.Evaluate"/>
/// or <see cref="CelCompiledExpression.Evaluate"/> call.
/// </summary>
/// <remarks>
/// <para>
/// Per-call limits may tighten but not exceed the environment-level limits set by
/// <see cref="CelCompilationLimits"/>. Every evaluation path is intrinsically bounded;
/// no unbounded evaluation overload exists.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelEvaluationLimits
{
    /// <summary>
    /// Gets a pre-configured instance with conservative safe defaults suitable for
    /// policy-linting workloads.
    /// </summary>
    public static readonly CelEvaluationLimits SafeDefaults = new(
        maxIterations: 1_000,
        maxCostUnits: 100_000L);

    /// <summary>
    /// Gets the maximum number of evaluation steps (iterations, recursive descents, etc.)
    /// permitted per evaluation call.
    /// </summary>
    public int MaxIterations { get; }

    /// <summary>
    /// Gets the maximum accumulated cost units permitted per evaluation call,
    /// where cost is an abstract measure of computational work.
    /// </summary>
    public long MaxCostUnits { get; }

    /// <summary>
    /// Creates a custom <see cref="CelEvaluationLimits"/> instance.
    /// </summary>
    public CelEvaluationLimits(int maxIterations, long maxCostUnits)
    {
        if (maxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIterations));
        if (maxCostUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCostUnits));

        MaxIterations = maxIterations;
        MaxCostUnits = maxCostUnits;
    }
}
