namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureExplainOutcome(
    string Source,
    string Target,
    IReadOnlyList<string>? Path,
    IReadOnlyList<string> ContractIds);
