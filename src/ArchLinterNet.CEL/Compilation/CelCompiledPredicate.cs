using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// An immutable, thread-safe, concurrently reusable compiled CEL predicate that evaluates to
/// a <c>bool</c> value.
/// </summary>
/// <remarks>
/// <para>
/// The predicate path enforces a <c>bool</c> result type at compile time. Repeated evaluation
/// performs no parsing, binding, or type-checking. The expression is not convertible to a
/// delegate or expression tree.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelCompiledPredicate
{
    /// <summary>Gets the language profile this predicate was compiled under.</summary>
    public CelProfile Profile { get; }

    /// <summary>Gets the context schema this predicate is bound to.</summary>
    public CelContextSchema Schema { get; }

    /// <summary>Gets the cache-identity key for this compiled predicate.</summary>
    public CelCompilationKey CompilationKey { get; }

    /// <summary>Gets the compilation limits that were active when this predicate was compiled.</summary>
    public CelCompilationLimits CompilationLimits { get; }

    /// <summary>
    /// Gets the environment-level evaluation-limits ceiling captured at compile time.
    /// Per-call limits passed to <see cref="Evaluate(CelEvaluationContext, CelEvaluationLimits)"/>
    /// may tighten but must not exceed this ceiling.
    /// </summary>
    public CelEvaluationLimits EvaluationLimits { get; }

    internal CelCompiledPredicate(
        CelProfile profile,
        CelContextSchema schema,
        CelCompilationKey compilationKey,
        CelCompilationLimits compilationLimits,
        CelEvaluationLimits evaluationLimits)
    {
        Profile = profile;
        Schema = schema;
        CompilationKey = compilationKey;
        CompilationLimits = compilationLimits;
        EvaluationLimits = evaluationLimits;
    }

    /// <summary>
    /// Evaluates this predicate against the given context and limits, returning a structured
    /// <see cref="CelEvaluationResult"/> whose <see cref="CelEvaluationResult.AsBool"/> returns
    /// the boolean result.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="limits"/> exceeds the environment-level ceiling in
    /// <see cref="EvaluationLimits"/>. Per-call limits may only tighten the captured maximums.
    /// </exception>
    /// <exception cref="NotImplementedException">
    /// The evaluator is not yet implemented. This will be replaced by the real evaluator in task #327.
    /// </exception>
    public CelEvaluationResult Evaluate(CelEvaluationContext context, CelEvaluationLimits limits)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(limits);
        if (!limits.IsWithin(EvaluationLimits))
            throw new ArgumentException(
                "Per-call evaluation limits may tighten but must not exceed the environment-level " +
                $"ceiling captured at compile time ({EvaluationLimits.ComputeIdentity()}).",
                nameof(limits));
        throw new NotImplementedException(
            $"Evaluator not yet implemented for profile '{Profile.Id}' — see #327.");
    }

    /// <summary>
    /// Evaluates this predicate against the given context using the full environment-level
    /// ceiling captured in <see cref="EvaluationLimits"/>. Equivalent to
    /// <c>Evaluate(context, this.EvaluationLimits)</c>. This is a documented bounded default,
    /// not an unbounded evaluation path.
    /// </summary>
    /// <exception cref="NotImplementedException">
    /// The evaluator is not yet implemented. This will be replaced by the real evaluator in task #327.
    /// </exception>
    public CelEvaluationResult Evaluate(CelEvaluationContext context) =>
        Evaluate(context, EvaluationLimits);
}
