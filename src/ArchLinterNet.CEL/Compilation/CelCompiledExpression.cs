using ArchLinterNet.CEL.Binding;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// An immutable, thread-safe, concurrently reusable compiled CEL expression that evaluates to
/// any <see cref="CelValue"/>.
/// </summary>
/// <remarks>
/// <para>
/// Repeated evaluation performs no parsing, binding, or type-checking.
/// The expression is not convertible to a delegate or expression tree.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelCompiledExpression
{
    /// <summary>Gets the language profile this expression was compiled under.</summary>
    public CelProfile Profile { get; }

    /// <summary>Gets the context schema this expression is bound to.</summary>
    public CelContextSchema Schema { get; }

    /// <summary>Gets the cache-identity key for this compiled expression.</summary>
    public CelCompilationKey CompilationKey { get; }

    /// <summary>Gets the compilation limits that were active when this expression was compiled.</summary>
    public CelCompilationLimits CompilationLimits { get; }

    /// <summary>
    /// Gets the environment-level evaluation-limits ceiling captured at compile time.
    /// Per-call limits passed to <see cref="Evaluate(CelEvaluationContext, CelEvaluationLimits)"/>
    /// may tighten but must not exceed this ceiling.
    /// </summary>
    public CelEvaluationLimits EvaluationLimits { get; }

    /// <summary>
    /// Gets the internal bound-expression plan produced by the binder. Never exposed publicly;
    /// consumed only by the evaluator (#327).
    /// </summary>
    internal CelBoundExpression Bound { get; }

    internal CelCompiledExpression(
        CelProfile profile,
        CelContextSchema schema,
        CelCompilationKey compilationKey,
        CelCompilationLimits compilationLimits,
        CelEvaluationLimits evaluationLimits,
        CelBoundExpression bound)
    {
        Profile = profile;
        Schema = schema;
        CompilationKey = compilationKey;
        CompilationLimits = compilationLimits;
        EvaluationLimits = evaluationLimits;
        Bound = bound;
    }

    /// <summary>
    /// Evaluates this expression against the given context and limits,
    /// returning a structured <see cref="CelEvaluationResult"/>.
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
    /// Evaluates this expression against the given context using the full environment-level
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
