using System.CommandLine;

namespace ArchLinterNet.Cli.Commands.Coverage.Application;

internal sealed class CoverageCommandDefinition(CoverageCommandHandler handler)
{
    public Command Create()
    {
        Command coverage = new("coverage", "Render architecture coverage reports.");
        Command report = new("report", "Render Markdown from strict validation JSON.");
        Option<string> input = new("--input");
        Option<string> changedFiles = new("--changed-files");
        Option<string> repoRoot = new("--repo-root");
        repoRoot.DefaultValueFactory = _ => ".";
        Option<string> output = new("--output");
        Option<int?> maxFailures = new("--max-failure-diagnostics");
        Option<string> diffStatus = new("--diff-status");
        diffStatus.DefaultValueFactory = _ => "ok";
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");
        report.Options.Add(input);
        report.Options.Add(changedFiles);
        report.Options.Add(repoRoot);
        report.Options.Add(output);
        report.Options.Add(maxFailures);
        report.Options.Add(diffStatus);
        report.Options.Add(help);
        report.SetAction(result => handler.Execute(new CoverageReportCommandOptions(
            result.GetValue(input) ?? string.Empty, result.GetValue(changedFiles), result.GetValue(repoRoot) ?? ".",
            result.GetValue(output), result.GetValue(maxFailures), result.GetValue(diffStatus) ?? "ok", result.GetValue(help))));
        coverage.Subcommands.Add(report);
        return coverage;
    }
}
