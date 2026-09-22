namespace ArchLinterNet.Core.BuildState;

// Internal CI seam: the host asks Core to perform the authoritative candidate preparation once
// and publish receipts inside that successful build path, or supplies the nonce-bound proof from
// an already-completed solution build so publication can stay verification-only. Later projections
// only use Ordinary mode with the resulting receipts.
internal sealed record BuildStatePreparedCandidateRequest(
    string PolicyPath,
    string? ConditionSetName = null,
    string? RequestedConfiguration = null,
    string? RequestedTargetFramework = null,
    string? RequestedPlatform = null,
    string? RequestedRuntimeIdentifier = null,
    bool NoRestore = false,
    CancellationToken CancellationToken = default,
    string? PreparedBuildProofDirectory = null,
    string? PreparedBuildProofNonce = null,
    bool BuildAlreadyCompleted = false);
