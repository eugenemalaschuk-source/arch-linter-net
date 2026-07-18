namespace ArchLinterNet.Core.Execution.Expressions;

// Evaluation-result wrapper suitable for selector/contract integration in #164. Per
// openspec/specs/cel-policy-model/spec.md: true means the candidate matches, false means it does
// not, and an evaluation failure is a configuration/evaluation error - never a silent non-match.
internal sealed class ArchitectureExpressionEvaluationResult
{
    private ArchitectureExpressionEvaluationResult(bool isMatch, bool isError, string? errorMessage)
    {
        IsMatch = isMatch;
        IsError = isError;
        ErrorMessage = errorMessage;
    }

    public bool IsMatch { get; }

    public bool IsError { get; }

    public string? ErrorMessage { get; }

    public static ArchitectureExpressionEvaluationResult Match(bool isMatch) => new(isMatch, false, null);

    public static ArchitectureExpressionEvaluationResult Error(string errorMessage) => new(false, true, errorMessage);
}
