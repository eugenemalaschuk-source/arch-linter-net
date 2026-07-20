namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed record BaselineGenerateCommandOptions(
    string PolicyPath,
    string? OutputPath,
    string Reason,
    string Mode,
    string? ConditionSetName,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);

internal sealed record BaselineUpdateCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    string Reason,
    string Mode,
    string? ConditionSetName,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);

internal sealed record BaselinePruneCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);

internal sealed record BaselineDiffCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);

internal sealed record BaselineVerifyCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);

// Deliberately has no Mode/ContractIds — unlike the other baseline subcommands, migrate cannot be
// scoped: a version-2 document cannot preserve version-1 matching semantics for only part of a
// file, so every entry is always classified against the full current candidate set.
internal sealed record BaselineMigrateCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string? OutputPath,
    string? ConditionSetName,
    string Format,
    bool DryRun,
    bool ShowHelp);
