namespace ArchLinterNet.Core.History.Evidence;

// A connected component of the candidate endpoint-overlap graph `H`. A component either canonicalizes
// into one accepted lineage or stays `ambiguous_dag` as a whole; timestamps never break the tie.
internal sealed class RenameComponent(int index, IReadOnlyList<RenameCandidate> candidates, bool accepted, IReadOnlyList<RenameCandidate> acceptedSequence)
{
    public int Index { get; } = index;

    public IReadOnlyList<RenameCandidate> Candidates { get; } = candidates;

    public bool Accepted { get; } = accepted;

    // The unique ancestry-ordered sequence when accepted; empty otherwise.
    public IReadOnlyList<RenameCandidate> AcceptedSequence { get; } = acceptedSequence;

    public string StatusText => Accepted ? "accepted" : "ambiguous_dag";
}
