using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.History.Application;

internal sealed class HistoryCommandDefinition(HistoryIngestCommandHandler handler)
{
    public Command Create()
    {
        Command history = new("history", "Analyze release architecture forensics evidence over an explicit Git range.");
        Command analyze = new("analyze", "Generate a versioned release architecture forensics report over an explicit Git range.");
        Option<string> repository = new("--repository");
        repository.DefaultValueFactory = _ => ".";
        Option<string> from = new("--from");
        Option<string> to = new("--to");
        Option<string?> policy = new("--policy");
        Option<bool> enrichDotNet = new("--enrich-dotnet");
        Option<string> format = new("--format");
        format.DefaultValueFactory = _ => "json";
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        analyze.Options.Add(repository);
        analyze.Options.Add(from);
        analyze.Options.Add(to);
        analyze.Options.Add(policy);
        analyze.Options.Add(enrichDotNet);
        analyze.Options.Add(format);
        analyze.Options.Add(help);
        analyze.SetAction(result => handler.Execute(new HistoryIngestCommandOptions(
            result.GetValue(repository) ?? ".",
            result.GetValue(from) ?? string.Empty,
            result.GetValue(to) ?? string.Empty,
            result.GetValue(format) ?? "json",
            result.GetValue(help),
            result.GetValue(policy),
            result.GetValue(enrichDotNet))));
        history.Subcommands.Add(analyze);
        return history;
    }
}
