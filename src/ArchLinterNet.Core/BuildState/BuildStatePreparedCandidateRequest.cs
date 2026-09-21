namespace ArchLinterNet.Core.BuildState;

// Internal CI seam: the host has already run the authoritative candidate restore/build and asks
// Core to publish receipt evidence for those existing outputs. This request deliberately has no
// build-capable mode; PublishPreparedBuildReceipts must remain a receipt-publication operation.
internal sealed record BuildStatePreparedCandidateRequest(
    string PolicyPath,
    string? ConditionSetName = null,
    string? RequestedConfiguration = null,
    string? RequestedTargetFramework = null,
    string? RequestedPlatform = null,
    string? RequestedRuntimeIdentifier = null,
    CancellationToken CancellationToken = default);
