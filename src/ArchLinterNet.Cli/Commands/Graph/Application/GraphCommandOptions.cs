namespace ArchLinterNet.Cli.Commands.Graph.Application;

internal sealed record GraphCommandOptions(
    string PolicyPath,
    string Mode,
    string Level,
    string Format,
    string? ConditionSetName,
    IReadOnlyList<string> ContractIds,
    bool ShowHelp);
