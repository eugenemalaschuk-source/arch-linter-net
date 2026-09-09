using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Internal data returned by the canonical public API surface resolver. Keeping this separate from
// the application service prevents operation-specific snapshot handling from owning runner state.
internal sealed record ArchitecturePublicApiSurfaceResolution(
    ArchitecturePublicApiSurfaceContract? Contract,
    IReadOnlyList<PublicApiSnapshotEntry> Entries,
    IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
    string? Error,
    PublicApiFailureKind FailureKind = PublicApiFailureKind.InvalidInput)
{
    public static ArchitecturePublicApiSurfaceResolution Failed(
        string error, IReadOnlyCollection<BuildStatePreflightDiagnostic>? diagnostics = null)
    {
        return new ArchitecturePublicApiSurfaceResolution(
            null,
            Array.Empty<PublicApiSnapshotEntry>(),
            diagnostics ?? Array.Empty<BuildStatePreflightDiagnostic>(),
            error);
    }
}
