using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Evidence;

// Builds the candidate endpoint-overlap graph `H` and decides which components canonicalize.
//
// The rule asks for exactly one all-candidate permutation that is ancestry-ordered, endpoint-linked,
// and lifecycle-clean. Enumerating permutations is factorial, so this first requires the component's
// candidate commits to be pairwise strictly ancestry-comparable — which fixes the permutation
// uniquely — and then checks linking and the lifecycle guard on that single ordering.
internal sealed class RenameLineageResolver(CommitGraph graph, IReadOnlyDictionary<string, List<GitCommit>> addDeleteCommitsByPath)
{
    public IReadOnlyList<RenameComponent> Resolve(IReadOnlyList<RenameCandidate> candidates)
    {
        List<List<RenameCandidate>> groups = GroupByEndpointOverlap(candidates);
        List<RenameComponent> components = [];
        for (int index = 0; index < groups.Count; index++)
        {
            List<RenameCandidate> group = groups[index];
            List<RenameCandidate> sequence = TryCanonicalize(group);
            bool accepted = sequence.Count > 0;
            foreach (RenameCandidate candidate in group)
            {
                candidate.ComponentIndex = index;
                candidate.Accepted = accepted;
            }

            components.Add(new RenameComponent(index, group, accepted, sequence));
        }

        return components;
    }

    private static List<List<RenameCandidate>> GroupByEndpointOverlap(IReadOnlyList<RenameCandidate> candidates)
    {
        Dictionary<string, int> componentOfPath = new(StringComparer.Ordinal);
        List<List<RenameCandidate>> groups = [];
        foreach (RenameCandidate candidate in candidates)
        {
            bool hasSource = componentOfPath.TryGetValue(candidate.SourcePath, out int sourceGroup);
            bool hasDestination = componentOfPath.TryGetValue(candidate.DestinationPath, out int destinationGroup);
            int target;
            if (hasSource && hasDestination && sourceGroup != destinationGroup)
            {
                target = Merge(groups, componentOfPath, sourceGroup, destinationGroup);
            }
            else if (hasSource)
            {
                target = sourceGroup;
            }
            else if (hasDestination)
            {
                target = destinationGroup;
            }
            else
            {
                target = groups.Count;
                groups.Add([]);
            }

            componentOfPath[candidate.SourcePath] = target;
            componentOfPath[candidate.DestinationPath] = target;
            groups[target].Add(candidate);
        }

        // Candidates order canonically inside a component; components order by their minimum
        // candidate record key, so component numbering never depends on enumeration order.
        foreach (List<RenameCandidate> group in groups)
        {
            group.Sort(RenameCandidate.CompareCanonical);
        }

        return [.. groups
            .Where(static group => group.Count > 0)
            .OrderBy(static group => group[0], Comparer<RenameCandidate>.Create(RenameCandidate.CompareCanonical))];
    }

    private static int Merge(List<List<RenameCandidate>> groups, Dictionary<string, int> componentOfPath, int keep, int absorb)
    {
        groups[keep].AddRange(groups[absorb]);
        groups[absorb] = [];
        foreach (string path in componentOfPath.Where(entry => entry.Value == absorb).Select(static entry => entry.Key).ToList())
        {
            componentOfPath[path] = keep;
        }

        return keep;
    }

    // Returns the accepted sequence, or an empty list when the component is `ambiguous_dag`.
    private List<RenameCandidate> TryCanonicalize(List<RenameCandidate> group)
    {
        if (group.Count == 1)
        {
            return group;
        }

        if (!AllPairwiseStrictlyComparable(group))
        {
            return [];
        }

        List<RenameCandidate> ordered = [.. group];
        ordered.Sort(CompareByAncestry);
        return HasUnbrokenChain(ordered) ? ordered : [];
    }

    // A permutation is fixed uniquely only when every pair of candidate commits is strictly
    // ancestry-comparable; any incomparable pair (a fork, or two candidates in one commit) leaves
    // the ordering ambiguous.
    private bool AllPairwiseStrictlyComparable(List<RenameCandidate> group)
    {
        for (int outer = 0; outer < group.Count; outer++)
        {
            for (int inner = outer + 1; inner < group.Count; inner++)
            {
                if (!AreStrictlyComparable(group[outer], group[inner]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private int CompareByAncestry(RenameCandidate left, RenameCandidate right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        return graph.IsStrictAncestor(left.Commit.Id, right.Commit.Id) ? -1 : 1;
    }

    // The lifecycle guard: an intervening ordinary add/delete of the shared path between two
    // adjacent candidates breaks the chain even though the candidates are otherwise endpoint-linked.
    private bool HasUnbrokenChain(List<RenameCandidate> ordered)
    {
        for (int index = 0; index + 1 < ordered.Count; index++)
        {
            if (!string.Equals(ordered[index].DestinationPath, ordered[index + 1].SourcePath, StringComparison.Ordinal)
                || HasLifecycleBreak(ordered[index], ordered[index + 1]))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreStrictlyComparable(RenameCandidate left, RenameCandidate right)
        => graph.IsStrictAncestor(left.Commit.Id, right.Commit.Id) || graph.IsStrictAncestor(right.Commit.Id, left.Commit.Id);

    // Deleting and later recreating the shared path breaks rename-chain continuity, even though all
    // same-path events still belong to one baseline path identity.
    private bool HasLifecycleBreak(RenameCandidate earlier, RenameCandidate later)
    {
        if (!addDeleteCommitsByPath.TryGetValue(earlier.DestinationPath, out List<GitCommit>? touching))
        {
            return false;
        }

        return touching.Any(between => graph.IsStrictAncestor(earlier.Commit.Id, between.Id) && graph.IsStrictAncestor(between.Id, later.Commit.Id));
    }
}
