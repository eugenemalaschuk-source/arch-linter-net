namespace ArchLinterNet.Core.Model;

public enum ExpressionParticipationResult
{
    Matched,
    NotMatched,
    EvaluationFailed,
}

// Surfaces one `when` predicate's already-computed source text, YAML location, and match result on
// a diagnostic payload, additively. Evaluation failures never reach a violation payload in practice -
// cel-policy-model's fail-closed semantics report those as a separate configuration/evaluation error
// before any violation is constructed - but the result is modeled as a full tri-state so a future
// caller cannot mistake "not populated" for "evaluated false".
//
// A single violation can involve more than one `when`-bearing selector at once (e.g. a contextual
// dependency contract's `source.when` AND its matched `forbidden[*].when`), so callers collect these
// into a list rather than a single nullable field - Location disambiguates which selector each entry
// came from.
public sealed record ExpressionParticipation(
    string ContractName,
    string Location,
    string Source,
    string? YamlPath,
    ExpressionParticipationResult Result);
