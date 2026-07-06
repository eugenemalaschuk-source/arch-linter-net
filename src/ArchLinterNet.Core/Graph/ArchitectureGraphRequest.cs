using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Graph;

public sealed record ArchitectureGraphRequest
{
    public required string PolicyPath { get; init; }

    public string Mode { get; init; } = "all";

    public ArchitectureGraphLevel Level { get; init; } = ArchitectureGraphLevel.Namespace;

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }
}
