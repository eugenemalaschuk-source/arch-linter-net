namespace ArchLinterNet.Core.BuildState;

// The ArchLinterNet build receipt (v1) — the authoritative proof that an artifact is current,
// emitted only by explicit ensure-built preparation. See "Authoritative verification" in
// docs/internal/analysis-build-state-blueprint.md.
public sealed record BuildReceiptV1(
    string ProjectPath,
    string AssemblyName,
    string? Configuration,
    string? TargetFramework,
    string BuildInputFingerprint,
    string AssemblyContentDigest);
