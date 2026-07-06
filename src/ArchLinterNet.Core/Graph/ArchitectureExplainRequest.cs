using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureExplainRequest
{
    public required string PolicyPath { get; init; }

    public required string Source { get; init; }

    public required string Target { get; init; }

    public string Mode { get; init; } = "all";

    public ArchitectureGraphLevel Level { get; init; } = ArchitectureGraphLevel.Namespace;

    public string? ConditionSetName { get; init; }
}
