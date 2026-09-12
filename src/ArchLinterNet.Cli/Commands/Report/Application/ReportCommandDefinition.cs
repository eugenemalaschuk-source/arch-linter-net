using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Report.Application;

internal sealed class ReportCommandDefinition(ReportCommandHandler handler)
{
    private const int DefaultMaxDetails = 20;

    public Command Create()
    {
        Command command = new("report", "Render architecture governance reports from canonical artifacts.");
        command.Subcommands.Add(CreatePr());
        return command;
    }

    private Command CreatePr()
    {
        Command command = new("pr", "Render a deterministic architecture pull-request report.");
        Option<string> health = new("--health");
        Option<string> change = new("--change");
        Option<string> output = new("--output");
        Option<int> maxDetails = new("--max-details")
        {
            DefaultValueFactory = _ => DefaultMaxDetails,
        };
        Option<string> repositoryUrl = new("--repository-url");
        repositoryUrl.Aliases.Add("--repository-base-url");
        Option<string> headSha = new("--head-sha");
        Option<string> artifactUrl = new("--artifact-url");
        artifactUrl.Aliases.Add("--run-url");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");

        command.Options.Add(health);
        command.Options.Add(change);
        command.Options.Add(output);
        command.Options.Add(maxDetails);
        command.Options.Add(repositoryUrl);
        command.Options.Add(headSha);
        command.Options.Add(artifactUrl);
        command.Options.Add(help);
        command.SetAction(result => handler.Execute(new PrReportCommandOptions(
            result.GetValue(health) ?? string.Empty,
            result.GetValue(change) ?? string.Empty,
            result.GetValue(output),
            result.GetValue(maxDetails),
            result.GetValue(help),
            result.GetValue(repositoryUrl),
            result.GetValue(headSha),
            result.GetValue(artifactUrl))));
        return command;
    }
}
