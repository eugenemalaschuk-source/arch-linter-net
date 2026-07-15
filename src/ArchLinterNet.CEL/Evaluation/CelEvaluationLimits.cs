using ArchLinterNet.CEL.Compilation;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// Immutable evaluation budget limits applied per <see cref="CelCompiledPredicate.Evaluate"/>
/// or <see cref="CelCompiledExpression.Evaluate"/> call.
/// </summary>
/// <remarks>
/// <para>
/// Per-call limits may tighten but must not exceed the environment-level evaluation limits
/// stored in <see cref="CelEnvironment.EvaluationLimits"/>. Every evaluation path is
/// intrinsically bounded;
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCostUnits);

        MaxIterations = maxIterations;
        MaxCostUnits = maxCostUnits;
    }

    /// <summary>
    /// Returns whether every budget in this instance is less than or equal to the corresponding
    /// budget in <paramref name="ceiling"/>.
    /// </summary>
    internal bool IsWithin(CelEvaluationLimits ceiling) =>
        MaxIterations <= ceiling.MaxIterations && MaxCostUnits <= ceiling.MaxCostUnits;

    /// <summary>Returns a stable identity string for use in <see cref="CelCompilationKey"/>.</summary>
    internal string ComputeIdentity() =>
        $"iters={MaxIterations},cost={MaxCostUnits}";
}
