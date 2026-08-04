namespace ArchLinterNet.Core.Profiling;

// Modeled as an enum, not a bool, so #365/#408 can add real status values without renaming or
// restructuring this field later. NotApplicable remains the value for #408 (parallel scanning,
// not implemented yet). #365 (persistent cache) now reports Active whenever a run configured
// --cache/WithCache() with anything other than AnalysisCacheMode.Disabled — see
// AnalysisProfileCacheCounters.
public enum AnalysisProfileReservedFieldStatus
{
    NotApplicable,
    Active,
}
