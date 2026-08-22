namespace ArchLinterNet.Cli.Commands.Gate.Application;

internal sealed record GateCommandOptions(
    string PolicyPath,
    string? BaselinePath,
    string Mode,
    string? ConditionSetName,
    string Format,
    IReadOnlyList<string> ContractIds,
    string? BaseContextPath,
    string? CurrentContextPath,
    bool ShowHelp,
    bool EnsureBuilt,
    bool NoRestore,
    string? Configuration,
    string? TargetFramework,
    string? Platform,
    string? RuntimeIdentifier);
