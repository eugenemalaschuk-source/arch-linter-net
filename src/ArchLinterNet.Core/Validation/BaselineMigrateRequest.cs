namespace ArchLinterNet.Core.Validation;

// Deliberately has no Mode/ContractIds scoping, unlike the other baseline requests: a version-2
// document cannot preserve version-1 matching semantics for only part of a file (an out-of-scope
// legacy entry might be ambiguous under structured identity, discoverable only by correlating it),
// so migrate always classifies every entry in the file against the full current candidate set.
public sealed record BaselineMigrateRequest
{
    public required string PolicyPath { get; init; }

    public required string BaselinePath { get; init; }

    public string? OutputPath { get; init; }

    public string? ConditionSetName { get; init; }

    public bool DryRun { get; init; }
}
