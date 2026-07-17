using System.Reflection;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Deterministic (non-timing) instrumentation proving the benchmarking issue's (#168) required
/// guarantee: "repeated evaluation performs no parser/binder/type-checker work". This does not use
/// wall-clock measurement — it verifies the guarantee structurally, using reflection over the
/// shipped types, and by observing object identity across many evaluations.
/// </summary>
[TestFixture]
public sealed class CelRepeatedEvaluationNoReparseTests
{
    [Test]
    public void CelCompiledPredicate_Evaluate_HasNoDataFlowPathToTokenizeParseOrBind()
    {
        // Evaluate's only overloads take (CelEvaluationContext, CelEvaluationLimits) or
        // (CelEvaluationContext) — neither a source string (CelTokenizer.Tokenize's input), a
        // token stream (CelParser.Parse's input), nor a syntax node (CelBinder.Bind's input) is
        // reachable from either parameter list, so Evaluate() has no data available to re-tokenize,
        // re-parse, or re-bind even in principle.
        var evaluateOverloads = typeof(CelCompiledPredicate)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(CelCompiledPredicate.Evaluate))
            .Select(m => m.GetParameters().Select(p => p.ParameterType).ToArray())
            .ToList();

        Assert.That(evaluateOverloads, Has.Count.EqualTo(2),
            "CelCompiledPredicate.Evaluate must have exactly two overloads: (context) and (context, limits).");
        Assert.That(evaluateOverloads, Has.Some.EqualTo(new[] { typeof(CelEvaluationContext) }));
        Assert.That(evaluateOverloads, Has.Some.EqualTo(new[] { typeof(CelEvaluationContext), typeof(CelEvaluationLimits) }));

        foreach (var parameterTypes in evaluateOverloads)
        {
            Assert.That(parameterTypes, Has.None.EqualTo(typeof(string)),
                "No Evaluate overload may accept a source string — accepting one would allow re-tokenizing.");
        }
    }

    [Test]
    public void CelCompiledPredicate_BoundPlan_IsFixedAtConstructionWithNoSetter()
    {
        // The internal Bound property (the compiled program's held plan, consumed directly by
        // CelEvaluator.Evaluate) is get-only: the compiler enforces that no code path — including
        // Evaluate() itself — can ever replace it after construction. A property with a setter
        // would at least leave open the possibility of a lazy re-bind; get-only rules that out
        // categorically, not just "as currently implemented".
        var boundProperty = typeof(CelCompiledPredicate).GetProperty(
            "Bound", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(boundProperty, Is.Not.Null, "Expected an internal 'Bound' property on CelCompiledPredicate.");
        Assert.That(boundProperty!.CanWrite, Is.False, "Bound must be get-only (no setter) to guarantee it is immutable post-construction.");
    }

    [Test]
    public void CelCompiledPredicate_RepeatedEvaluation_NeverReplacesTheBoundPlanAcrossManyCalls()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("no-reparse-v1");
        var flag = schemaBuilder.AddVariable("flag", CelType.Bool);
        var schema = schemaBuilder.Build();

        var environment = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .Build();

        var compilation = environment.CompilePredicate("flag");
        Assert.That(compilation.IsSuccess, Is.True);
        var predicate = compilation.Program!;

        var context = environment.CreateEvaluationContextBuilder()
            .Set(flag, CelValue.Bool(true))
            .Build();

        var boundProperty = typeof(CelCompiledPredicate).GetProperty(
            "Bound", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var boundBeforeAnyEvaluation = boundProperty.GetValue(predicate);
        Assert.That(boundBeforeAnyEvaluation, Is.Not.Null);

        // Not a timing measurement: this asserts the exact same bound-plan object reference is
        // observed both before the loop and after 1000 evaluations — the compiled program's
        // internal state literally never changes across repeated evaluation.
        const int RepeatedEvaluationCount = 1000;
        for (var i = 0; i < RepeatedEvaluationCount; i++)
        {
            var result = predicate.Evaluate(context);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.AsBool(), Is.True);
        }

        var boundAfterRepeatedEvaluation = boundProperty.GetValue(predicate);
        Assert.That(boundAfterRepeatedEvaluation, Is.SameAs(boundBeforeAnyEvaluation),
            "The bound plan must be the exact same object across 1000 evaluations — a different " +
            "reference would mean the compiled program was re-parsed/re-bound somewhere along the way.");
    }
}
