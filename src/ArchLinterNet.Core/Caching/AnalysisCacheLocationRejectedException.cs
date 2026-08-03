namespace ArchLinterNet.Core.Caching;

// Thrown when a caller-selected --cache <path> (or a derived entry path) fails canonical
// containment/safety validation — see openspec/specs/analysis-cache/spec.md,
// "Cache location resolution rejects unsafe paths".
public sealed class AnalysisCacheLocationRejectedException(string message) : Exception(message);
