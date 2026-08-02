namespace ArchLinterNet.Core.Profiling;

// A reserved field's only value today is NotApplicable — no persistent cache (#365) or parallel
// scanning (#408) exists yet. Modeled as an enum, not a bool, so #365/#408 can add real status
// values (e.g. Hit/Miss, Active) without renaming or restructuring this field later.
public enum AnalysisProfileReservedFieldStatus
{
    NotApplicable,
}
