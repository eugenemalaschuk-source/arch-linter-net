namespace ArchLinterNet.Core.Model;

/// <summary>
/// Core-owned projection of selected external diagnostics. The normalized findings and their
/// governance-blocking semantics intentionally travel together so hosts cannot report a strict
/// finding while preserving an unrelated passing outcome.
/// </summary>
public sealed class ImportedExternalDiagnosticProjection
{
    /// <summary>An empty imported-diagnostic projection with no blocking findings.</summary>
    public static ImportedExternalDiagnosticProjection Empty { get; } =
        new(Array.Empty<ArchitectureFinding>());

    internal ImportedExternalDiagnosticProjection(IReadOnlyList<ArchitectureFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        Findings = Array.AsReadOnly(findings.ToArray());
    }

    /// <summary>Canonical imported-diagnostic findings in deterministic reporting order.</summary>
    public IReadOnlyList<ArchitectureFinding> Findings { get; }

    /// <summary>Whether this projection contains strict, governance-blocking findings.</summary>
    public bool HasBlockingFindings => Findings.Any(finding =>
        string.Equals(finding.Mode, "strict", StringComparison.Ordinal));
}
