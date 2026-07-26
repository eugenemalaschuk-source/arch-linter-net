using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class UpdateBaselineSubcommandModule : IBaselineSubcommandModule
{
    public string CommandName => "update";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        BaselineUpdateCommandHandler handler = new(runtime, console, fileSystem);
        Command command = new(CommandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> outputOption = new("--output");
        BaselineOptionsFactory.ReasonOptionSet reasonOptions = BaselineOptionsFactory.CreateReasonOptions();
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        BaselineOptionsFactory.WriteOptionSet writeOptions = BaselineOptionsFactory.CreateWriteOptions();
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(outputOption);
        BaselineOptionsFactory.AddTo(command, reasonOptions);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        BaselineOptionsFactory.AddTo(command, writeOptions);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselineUpdateCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(outputOption),
            BaselineOptionsFactory.Read(parseResult, reasonOptions),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : "human",
            BaselineOptionsFactory.Read(parseResult, writeOptions),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
