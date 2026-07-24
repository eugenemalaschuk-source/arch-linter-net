namespace ArchLinterNet.Cli.Commands.Validate;

internal sealed record ValidateCommandOptions(
    string PolicyPath,
    string Mode,
    string Format,
    IReadOnlyList<string> ContractIds,
    string? ConditionSetName,
    bool TimingsEnabled,
    string? BaselinePath,
    bool ShowHelp,
    bool ShowVersion,
    bool EnsureBuilt = false,
    bool NoRestore = false,
    string? Configuration = null,
    string? TargetFramework = null);
