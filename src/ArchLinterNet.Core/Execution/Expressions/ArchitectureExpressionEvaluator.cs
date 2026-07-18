using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Evaluation;

namespace ArchLinterNet.Core.Execution.Expressions;

// Wraps CelCompiledPredicate.Evaluate for #164 to consume. Not called from any checker, matcher,
// or session class yet - see openspec/changes/core-cel-integration/design.md.
internal static class ArchitectureExpressionEvaluator
{
    public static ArchitectureExpressionEvaluationResult Evaluate(
        CelCompiledPredicate predicate, CelEvaluationContext context)
    {
        CelEvaluationResult result = predicate.Evaluate(context);
        if (!result.IsSuccess)
        {
            return ArchitectureExpressionEvaluationResult.Error(Describe(result.Diagnostics));
        }

        return ArchitectureExpressionEvaluationResult.Match(result.AsBool());
    }

    private static string Describe(IReadOnlyList<CelDiagnostic> diagnostics)
    {
        return string.Join("; ", diagnostics.Select(Describe));
    }

    private static string Describe(CelDiagnostic diagnostic)
    {
        return diagnostic.Span is { } span
            ? $"{diagnostic.Code} at [{span.Start}, {span.End}): {diagnostic.Message}"
            : $"{diagnostic.Code}: {diagnostic.Message}";
    }
}
