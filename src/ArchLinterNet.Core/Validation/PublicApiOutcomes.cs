using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record PublicApiCaptureOutcome(
    bool Succeeded,
    string? Snapshot,
    int EntryCount,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null);

public sealed record PublicApiDiffOutcome(
    bool Succeeded,
    bool InSync,
    PublicApiDelta Delta,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null);

public sealed record PublicApiUpdateOutcome(
    bool Succeeded,
    string? Snapshot,
    PublicApiDelta Delta,
    bool DryRun,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null);

// StaleDeclarations are inline `declared_api` entries the live surface no longer exports;
// UndeclaredSurface are live entries the inline list never acknowledged. Both are reported even
// when drift is accepted, so a migration always leaves an auditable record of what changed.
public sealed record PublicApiMigrateOutcome(
    bool Succeeded,
    string? Snapshot,
    IReadOnlyList<string> StaleDeclarations,
    IReadOnlyList<string> UndeclaredSurface,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null)
{
    public bool HasDrift => StaleDeclarations.Count > 0 || UndeclaredSurface.Count > 0;
}
