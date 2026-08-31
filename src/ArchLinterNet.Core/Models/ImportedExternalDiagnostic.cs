namespace ArchLinterNet.Core.Model;

/// <summary>
/// A policy-governed external static-analysis diagnostic selected from already trusted evidence.
/// </summary>
/// <remarks>
/// The selected diagnostic is retained as source evidence. This type does not reinterpret it as a
/// native architecture dependency or repeat SARIF trust/selection validation.
/// </remarks>
public sealed record ImportedExternalDiagnostic : ArchitectureDiagnostic
{
    /// <summary>Creates an imported diagnostic from one deterministic selected source result.</summary>
    public ImportedExternalDiagnostic(string logicalEvidenceId, SarifSelectedExternalDiagnostic selectedDiagnostic)
        : base("imported external diagnostic", RequireLogicalEvidenceId(logicalEvidenceId))
    {
        ArgumentNullException.ThrowIfNull(selectedDiagnostic);
        if (selectedDiagnostic.EvidenceProvenances.Count == 0)
        {
            throw new ArgumentException(
                "An imported diagnostic requires at least one authorizing evidence provenance.",
                nameof(selectedDiagnostic));
        }

        if (selectedDiagnostic.EvidenceProvenances.Any(provenance =>
                !string.Equals(provenance.LogicalId, logicalEvidenceId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every authorizing provenance must belong to the imported diagnostic's logical evidence control.",
                nameof(selectedDiagnostic));
        }

        LogicalEvidenceId = logicalEvidenceId;
        SelectedDiagnostic = selectedDiagnostic;
    }

    /// <summary>The declared evidence control that governs this diagnostic.</summary>
    public string LogicalEvidenceId { get; }

    /// <summary>The #521 selected source diagnostic and its immutable trust provenance.</summary>
    public SarifSelectedExternalDiagnostic SelectedDiagnostic { get; }

    /// <summary>The original source diagnostic facts retained from the selected SARIF result.</summary>
    public SarifEvidenceSourceDiagnostic SourceDiagnostic => SelectedDiagnostic.SourceDiagnostic;

    /// <summary>The source or deterministic fallback fingerprint selected by #521.</summary>
    public SarifExternalDiagnosticFingerprint Fingerprint => SelectedDiagnostic.Fingerprint;

    /// <summary>The policy-mapped strict or audit governance mode selected by #521.</summary>
    public SarifExternalDiagnosticGovernanceMode GovernanceMode => SelectedDiagnostic.GovernanceMode;

    /// <summary>The stable selected-result reference used to construct canonical finding identity.</summary>
    public string SelectedCanonicalIdentity => SelectedDiagnostic.CanonicalIdentity;

    /// <summary>Every ordered trusted artifact/run context that authorized this selected occurrence.</summary>
    public IReadOnlyList<SarifEvidenceProvenance> EvidenceProvenances => SelectedDiagnostic.EvidenceProvenances;

    /// <inheritdoc />
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ImportedExternalDiagnostic;

    private static string RequireLogicalEvidenceId(string logicalEvidenceId)
    {
        if (string.IsNullOrWhiteSpace(logicalEvidenceId))
        {
            throw new ArgumentException("A logical evidence identifier is required.", nameof(logicalEvidenceId));
        }

        return logicalEvidenceId;
    }
}
