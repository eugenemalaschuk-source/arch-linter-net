namespace ArchLinterNet.Core.Validation;

public sealed record ValidationRequest
{
    public required string PolicyPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public string? BaselinePath { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public bool EnforceUnmatchedIgnoredViolationsPolicy { get; init; }
}
