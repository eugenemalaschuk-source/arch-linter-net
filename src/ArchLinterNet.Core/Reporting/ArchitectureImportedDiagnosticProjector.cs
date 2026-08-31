using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Projects trusted, policy-selected external diagnostics through existing finding seams.</summary>
public static class ArchitectureImportedDiagnosticProjector
{
    /// <summary>
    /// Projects every selected diagnostic into canonical, deterministically ordered findings and
    /// their inseparable governance-blocking state.
    /// </summary>
    public static ImportedExternalDiagnosticProjection Project(
        SarifExternalDiagnosticSelectionResult selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var findings = new List<ArchitectureFinding>(selection.Diagnostics.Count);
        foreach (SarifSelectedExternalDiagnostic selectedDiagnostic in selection.Diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            findings.Add(ToFinding(selectedDiagnostic));
        }

        return new ImportedExternalDiagnosticProjection(
            ArchitectureFindingMapper.Order(findings, cancellationToken));
    }

    /// <summary>Projects one trusted selected diagnostic into its normalized finding.</summary>
    public static ArchitectureFinding ToFinding(SarifSelectedExternalDiagnostic selectedDiagnostic)
    {
        ArgumentNullException.ThrowIfNull(selectedDiagnostic);
        string logicalEvidenceId = ResolveLogicalEvidenceId(selectedDiagnostic);
        return ArchitectureFindingMapper.FromImportedExternalDiagnostic(
            new ImportedExternalDiagnostic(logicalEvidenceId, selectedDiagnostic));
    }

    private static string ResolveLogicalEvidenceId(SarifSelectedExternalDiagnostic selectedDiagnostic)
    {
        if (selectedDiagnostic.EvidenceProvenances.Count == 0)
        {
            throw new ArgumentException(
                "A selected imported diagnostic requires authorizing evidence provenance.",
                nameof(selectedDiagnostic));
        }

        string logicalEvidenceId = selectedDiagnostic.EvidenceProvenances[0].LogicalId;
        if (selectedDiagnostic.EvidenceProvenances.Any(provenance =>
                !string.Equals(provenance.LogicalId, logicalEvidenceId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "A selected imported diagnostic cannot merge distinct logical evidence controls.",
                nameof(selectedDiagnostic));
        }

        return logicalEvidenceId;
    }

}
