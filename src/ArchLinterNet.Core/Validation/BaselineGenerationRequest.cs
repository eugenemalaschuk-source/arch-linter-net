namespace ArchLinterNet.Core.Validation;

public sealed record BaselineGenerationRequest
{
    public required string PolicyPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public string Reason { get; init; } = "generated baseline";

    /// <summary>Repeatable <c>&lt;contract-id&gt;=&lt;reason&gt;</c> mappings for newly added entries.</summary>
    public IReadOnlyCollection<string>? ReasonForContract { get; init; }

    /// <summary>Repeatable <c>&lt;family&gt;=&lt;reason&gt;</c> mappings for newly added entries.</summary>
    public IReadOnlyCollection<string>? ReasonForFamily { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
