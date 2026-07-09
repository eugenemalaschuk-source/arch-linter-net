namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed record ExplainCommandOptions(
    string PolicyPath,
    string Mode,
    string Level,
    string Format,
    string? ConditionSetName,
    string? Source,
    string? Target,
    bool ShowHelp);
