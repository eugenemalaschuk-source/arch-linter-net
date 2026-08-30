using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>One trusted evidence result carrying its immutable authorization snapshot.</summary>
public sealed record SarifExternalDiagnosticSelectionInput
{
    /// <summary>Creates one selection input from a trust-validated evidence result.</summary>
    public SarifExternalDiagnosticSelectionInput(SarifEvidenceReadResult evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence;
    }

    /// <summary>The already trust-validated source evidence and authorization snapshot.</summary>
    public SarifEvidenceReadResult Evidence { get; }
}
