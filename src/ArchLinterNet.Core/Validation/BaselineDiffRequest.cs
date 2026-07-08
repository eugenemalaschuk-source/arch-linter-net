namespace ArchLinterNet.Core.Validation;

public sealed record BaselineDiffRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }
}
