using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

/// <summary>Projects trusted imported diagnostics into the established baseline candidate shape.</summary>
public static class ArchitectureImportedDiagnosticBaselineProjector
{
    private const string StrictGroup = "strict_external";
    private const string AuditGroup = "audit_external";

    /// <summary>Projects selected diagnostics into exact existing baseline-candidate structures.</summary>
    public static IReadOnlyList<ArchitectureBaselineCandidate> ToBaselineCandidates(
        SarifExternalDiagnosticSelectionResult selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var candidates = new List<ArchitectureBaselineCandidate>(selection.Diagnostics.Count);
        foreach (SarifSelectedExternalDiagnostic selectedDiagnostic in selection.Diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArchitectureFinding finding = ArchitectureImportedDiagnosticProjector.ToFinding(selectedDiagnostic);
            var detail = (ImportedExternalDiagnostic)finding.Details;
            candidates.Add(new ArchitectureBaselineCandidate(
                detail.GovernanceMode == SarifExternalDiagnosticGovernanceMode.Strict ? StrictGroup : AuditGroup,
                detail.LogicalEvidenceId,
                detail.SelectedCanonicalIdentity,
                detail.SelectedCanonicalIdentity,
                finding.Identity));
        }

        return SortCandidates(candidates, ArchitectureViolationIdentityJson.Serialize);
    }

    /// <summary>
    /// Orders candidates using one precomputed representation of each structured identity.
    /// </summary>
    internal static IReadOnlyList<ArchitectureBaselineCandidate> SortCandidates(
        IEnumerable<ArchitectureBaselineCandidate> candidates,
        Func<ArchitectureViolationIdentity, string> identitySerializer)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(identitySerializer);

        return candidates
            // Candidate identity is immutable, and baseline projection can receive the reader's
            // full 100k-result allowance. Serialize once per candidate, not twice per sort
            // comparison, so ordering remains deterministic without an allocation-heavy hot path.
            .Select(candidate => new CandidateSortEntry(
                candidate,
                candidate.Identity is null ? string.Empty : identitySerializer(candidate.Identity)))
            .OrderBy(entry => entry.Candidate.ContractGroup, StringComparer.Ordinal)
            .ThenBy(entry => entry.Candidate.ContractId, StringComparer.Ordinal)
            .ThenBy(entry => entry.IdentitySortKey, StringComparer.Ordinal)
            .Select(entry => entry.Candidate)
            .ToArray();
    }

    private sealed record CandidateSortEntry(ArchitectureBaselineCandidate Candidate, string IdentitySortKey);
}
