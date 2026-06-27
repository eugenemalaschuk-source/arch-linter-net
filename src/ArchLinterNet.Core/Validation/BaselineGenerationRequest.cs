namespace ArchLinterNet.Core.Validation;

public sealed record BaselineGenerationRequest
{
    public required string PolicyPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public string Reason { get; init; } = "generated baseline";
}
