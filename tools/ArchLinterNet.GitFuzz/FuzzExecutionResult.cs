namespace ArchLinterNet.GitFuzz;

internal readonly record struct FuzzExecutionResult(
    FuzzExecutionOutcome Outcome,
    int CanonicalDigestRuns,
    int FailClosedDigestRuns)
{
    public override string ToString()
        => $"{Outcome}; canonical_digest_runs={CanonicalDigestRuns}; fail_closed_digest_runs={FailClosedDigestRuns}";
}
