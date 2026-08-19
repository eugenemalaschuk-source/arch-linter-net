namespace ArchLinterNet.Cli.Commands.History.Application;

internal sealed record HistoryIngestCommandOptions(
    string Repository,
    string From,
    string To,
    string Format,
    bool ShowHelp);
