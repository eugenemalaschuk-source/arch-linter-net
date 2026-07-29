using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    internal int FindingIdentityCursor => _findingIdentityCandidates.Count;

    internal IReadOnlyList<ArchitectureViolation> AttachFindingIdentities(
        IReadOnlyCollection<ArchitectureViolation> violations,
        int cursor)
    {
        var available = _findingIdentityCandidates
            .Skip(cursor)
            .Where(candidate => candidate.Identity is not null)
            .ToList();
        var attached = new List<ArchitectureViolation>(violations.Count);

        foreach (ArchitectureViolation violation in violations)
        {
            ArchitectureBaselineCandidate[] matches = available
                .Where(candidate => CandidateMatchesViolation(candidate, violation))
                .ToArray();
            ArchitectureBaselineCandidate[] selected = violation.Payload is CompositionPayload
                ? matches
                : SelectOneCandidatePerReportedReference(violation, matches);
            foreach (ArchitectureBaselineCandidate candidate in selected)
            {
                available.Remove(candidate);
            }

            ArchitectureViolationIdentity[] identities = selected
                .Select(candidate => candidate.Identity!)
                .ToArray();
            attached.Add(identities.Length == 0
                ? violation
                : violation with { Identity = identities[0], Identities = identities });
        }

        return attached;
    }

    private static bool CandidateMatchesViolation(
        ArchitectureBaselineCandidate candidate,
        ArchitectureViolation violation)
    {
        if (candidate.ContractId != violation.ContractId || candidate.SourceType != violation.SourceType)
        {
            return false;
        }

        string? targetMember = candidate.Identity?.TargetMember;
        return violation.ForbiddenReferences.Any(reference =>
            ReferenceMatches(reference, targetMember)
            || ReferenceMatches(reference, candidate.ForbiddenReference));
    }

    private static ArchitectureBaselineCandidate[] SelectOneCandidatePerReportedReference(
        ArchitectureViolation violation,
        IReadOnlyCollection<ArchitectureBaselineCandidate> candidates)
    {
        var selected = new List<ArchitectureBaselineCandidate>();
        foreach (string reference in violation.ForbiddenReferences)
        {
            ArchitectureBaselineCandidate? candidate = candidates.FirstOrDefault(candidate =>
                !selected.Contains(candidate)
                && (ReferenceMatches(reference, candidate.Identity?.TargetMember)
                    || ReferenceMatches(reference, candidate.ForbiddenReference)));
            if (candidate is not null)
            {
                selected.Add(candidate);
            }
        }

        return selected.ToArray();
    }

    private static bool ReferenceMatches(string reportedReference, string? identityReference)
    {
        return identityReference is not null
            && (reportedReference.Equals(identityReference, StringComparison.Ordinal)
                || reportedReference.StartsWith(identityReference + "@", StringComparison.Ordinal)
                || reportedReference.StartsWith(identityReference + " ", StringComparison.Ordinal));
    }
}
