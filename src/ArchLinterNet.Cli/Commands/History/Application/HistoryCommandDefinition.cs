using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.History.Application;

internal sealed class HistoryCommandDefinition(HistoryIngestCommandHandler handler)
{
    public Command Create()
    {
        Command history = new("history", "Analyze release architecture forensics evidence over an explicit Git range.");
        Command ingest = new("ingest", "Run canonical Git ingestion over an explicit exclusive-from/inclusive-to range.");
        Option<string> repository = new("--repository");
        repository.DefaultValueFactory = _ => ".";
        Option<string> from = new("--from");
        Option<string> to = new("--to");
        Option<string> format = new("--format");
        format.DefaultValueFactory = _ => "json";
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        ingest.Options.Add(repository);
        ingest.Options.Add(from);
        ingest.Options.Add(to);
        ingest.Options.Add(format);
        ingest.Options.Add(help);
        ingest.SetAction(result => handler.Execute(new HistoryIngestCommandOptions(
            result.GetValue(repository) ?? ".",
            result.GetValue(from) ?? string.Empty,
            result.GetValue(to) ?? string.Empty,
            result.GetValue(format) ?? "json",
            result.GetValue(help))));
        history.Subcommands.Add(ingest);
        return history;
    }
}
