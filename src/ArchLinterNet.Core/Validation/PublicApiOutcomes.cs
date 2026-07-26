using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Distinguishes "the gate ran and found drift" from "the operation never completed". The 0.5.1
// contract reserves exit code 1 for the former and 2 for the latter, so the category has to survive
// as typed data rather than being re-derived from a message string by the host.
public enum PublicApiFailureKind
{
    None,

    // The operation completed and refused to proceed because the surface drifted.
    Drift,

    // Unknown contract, unusable snapshot, unsafe path, blocked preflight, unresolved assembly.
    InvalidInput,
}

public sealed record PublicApiCaptureOutcome(
    bool Succeeded,
    string? Snapshot,
    int EntryCount,
    string? ResolvedOutputPath,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null,
    PublicApiFailureKind FailureKind = PublicApiFailureKind.None);

public sealed record PublicApiDiffOutcome(
    bool Succeeded,
    bool InSync,
    PublicApiDelta Delta,
    string? ResolvedSnapshotPath,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null,
    PublicApiFailureKind FailureKind = PublicApiFailureKind.None);

public sealed record PublicApiUpdateOutcome(
    bool Succeeded,
    string? Snapshot,
    PublicApiDelta Delta,
    bool DryRun,
    string? ResolvedSnapshotPath,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null,
    PublicApiFailureKind FailureKind = PublicApiFailureKind.None);

// StaleDeclarations are inline `declared_api` entries the live surface no longer exports;
// UndeclaredSurface are live entries the inline list never acknowledged. Both are reported even
// when drift is accepted, so a migration always leaves an auditable record of what changed.
public sealed record PublicApiMigrateOutcome(
    bool Succeeded,
    string? Snapshot,
    IReadOnlyList<string> StaleDeclarations,
    IReadOnlyList<string> UndeclaredSurface,
    string? ResolvedOutputPath,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error = null,
    PublicApiFailureKind FailureKind = PublicApiFailureKind.None)
{
    public bool HasDrift => StaleDeclarations.Count > 0 || UndeclaredSurface.Count > 0;
}
