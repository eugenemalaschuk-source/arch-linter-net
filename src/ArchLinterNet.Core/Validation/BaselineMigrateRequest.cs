namespace ArchLinterNet.Core.Validation;

public sealed record BaselineMigrateRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public string? OutputPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public bool DryRun { get; init; }
}
