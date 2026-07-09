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
