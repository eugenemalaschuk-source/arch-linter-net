namespace ArchLinterNet.Core.Model;

// Typed evidence for the candidate-matching exclusion matchers (`exclude_types_matching`,
// `exclude_files_matching`) that type-placement and layout-convention contracts subtract from
// their positively-matched candidate set. Unlike source-set exclusion (a set-membership operation
// resolved once at load time, see ArchitectureSourceExpansionModels), these matchers are evaluated
// per candidate at run time against reflection/source-enriched facts - so this record is populated
// during contract execution (ArchitectureAnalysisSession), one entry per authored exclusion item
// per contract, rather than during ArchitecturePolicyDocumentLoader.Load.
public sealed record ArchitectureSubtractiveMatcherParticipation(
    string ContractId,
    string ContractName,
    string Field,
    int Index,
    bool Matched)
{
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    // True when the matcher's own applicability couldn't be determined for at least one candidate
    // it structurally matched (e.g. a `when` referencing source-path facts with no resolved source
    // file) - a distinct state from Matched: the matcher may or may not have actually excluded
    // anything, so it must not be reported as either matched or stale.
    public bool EvaluationFailed { get; init; }
}
