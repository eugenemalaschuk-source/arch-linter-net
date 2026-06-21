namespace ArchLinterNet.Core.Resolution;

public sealed record ArchitectureNamespaceMatch(
    bool Matched,
    string Pattern,
    string? MatchedNamespacePrefix);
