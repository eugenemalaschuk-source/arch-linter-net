using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

/// <summary>Projects trusted imported diagnostics into the established baseline candidate shape.</summary>
public static class ArchitectureImportedDiagnosticBaselineProjector
{
    private const string StrictGroup = "strict";
    private const string AuditGroup = "audit";

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
                SourceDisplayIdentity(detail),
                detail.SelectedCanonicalIdentity,
                finding.Identity));
        }

        return candidates
            .OrderBy(candidate => candidate.ContractGroup, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ContractId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Identity, ArchitectureViolationIdentityComparer.Instance)
            .ToArray();
    }

    private static string SourceDisplayIdentity(ImportedExternalDiagnostic diagnostic)
    {
        string tool = diagnostic.EvidenceProvenances[0].ToolName ?? "<unknown-tool>";
        string rule = diagnostic.SourceDiagnostic.RuleId ?? "<unknown-rule>";
        return tool + "/" + rule;
    }

    private sealed class ArchitectureViolationIdentityComparer : IComparer<ArchitectureViolationIdentity?>
    {
        public static ArchitectureViolationIdentityComparer Instance { get; } = new();

        public int Compare(ArchitectureViolationIdentity? left, ArchitectureViolationIdentity? right) =>
            StringComparer.Ordinal.Compare(
                left is null ? string.Empty : ArchitectureViolationIdentityJson.Serialize(left),
                right is null ? string.Empty : ArchitectureViolationIdentityJson.Serialize(right));
    }
}
