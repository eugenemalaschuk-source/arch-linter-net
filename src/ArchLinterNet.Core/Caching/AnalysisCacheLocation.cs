namespace ArchLinterNet.Core.Caching;

// A resolved, safety-validated cache root. RootPath is always an absolute, canonicalized
// directory path that AnalysisCacheStore treats as the sole containment boundary for entry
// reads/writes/clears.
public sealed record AnalysisCacheLocation(string RootPath, AnalysisCacheMode Mode);
