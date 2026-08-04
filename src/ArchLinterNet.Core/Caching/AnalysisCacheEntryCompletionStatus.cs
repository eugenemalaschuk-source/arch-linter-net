namespace ArchLinterNet.Core.Caching;

// A cache entry is only ever published for a successful, non-partial, non-cancelled original run
// (see AnalysisCacheStore.Put) — modeled as an enum rather than a bool so a future outcome (e.g. a
// deliberately reusable ValidationFailure fact set) can be added without restructuring the field.
public enum AnalysisCacheEntryCompletionStatus
{
    Success,
}
