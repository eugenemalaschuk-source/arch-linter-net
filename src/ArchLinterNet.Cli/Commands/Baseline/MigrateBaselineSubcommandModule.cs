using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class MigrateBaselineSubcommandModule : IBaselineSubcommandModule
{
    public string CommandName => "migrate";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        BaselineMigrateCommandHandler handler = new(runtime, console, fileSystem);
        Command command = new(CommandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> outputOption = new("--output");
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> dryRunOption = new("--dry-run");
        dryRunOption.Aliases.Add("--check");
        Option<bool> jsonOption = new("--json");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(outputOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(jsonOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselineMigrateCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(dryRunOption),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
