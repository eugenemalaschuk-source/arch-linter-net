namespace ArchLinterNet.Core.Caching;

// Every non-hit outcome carries a stable, typed reason — never an unexplained execution failure.
// See openspec/specs/analysis-cache/spec.md, "Miss and reject outcomes are typed and fail safe".
public enum AnalysisCacheRejectReason
{
    Disabled,
    Missing,
    Corrupt,
    Truncated,
    ForeignSchema,
    IncompatibleFormatVersion,
    IncompatibleToolVersion,
    KeyMismatch,
    IntegrityMismatch,
    ProjectSetMismatch,
    ArtifactSetMismatch,
    IneligibleBuildInput,
    InputChangedDuringExecution,
    IncompleteOriginalRun,
    Cancelled,
    PathUnsafe,
    SizeExceeded,
}
