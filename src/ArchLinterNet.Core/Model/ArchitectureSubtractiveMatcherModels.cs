namespace ArchLinterNet.Core.Model;

// The role is typed rather than inferred from a YAML field name, so coverage and explain consumers
// can distinguish a candidate-universe selector from a subtractive selector without duplicating the
// type/layout contract grammar. A non-matched exclusion is stale unless EvaluationFailed is true.
public enum ArchitectureSelectorParticipationKind
{
    Inclusion,
    Exclusion
}

// Typed evidence for the candidate matchers (`types_matching`, `files_matching`, and their
// subtractive counterparts) that type-placement and layout-convention contracts evaluate at run
// time. Unlike source-set selection (resolved once at load time), these matchers run per candidate
// against reflection/source-enriched facts, so participation is populated during contract execution.
public sealed record ArchitectureSubtractiveMatcherParticipation(
    string ContractId,
    string ContractName,
    string Field,
    int Index,
    bool Matched)
{
    public ArchitectureSelectorParticipationKind Kind { get; init; } = ArchitectureSelectorParticipationKind.Exclusion;

    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    // True when the matcher's own applicability couldn't be determined for at least one candidate
    // it structurally matched (e.g. a `when` referencing source-path facts with no resolved source
    // file) - a distinct state from Matched: the matcher may or may not have actually excluded
    // anything, so it must not be reported as either matched or stale.
    public bool EvaluationFailed { get; init; }

    public bool IsStaleExclusion => Kind == ArchitectureSelectorParticipationKind.Exclusion
        && !Matched
        && !EvaluationFailed;
}
