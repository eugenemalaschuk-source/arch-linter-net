using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class GenerateBaselineSubcommandModule : IDefaultBaselineSubcommandModule
{
    public string CommandName => "generate";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return CreateCommand("generate", runtime, console, fileSystem);
    }

    public Command CreateDefaultCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        return CreateCommand(commandName, runtime, console, fileSystem);
    }

    private static Command CreateCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        BaselineGenerateCommandHandler handler = new(runtime, console, fileSystem);
        Command command = new(commandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> outputOption = new("--output");
        Option<string> reasonOption = new("--reason");
        reasonOption.DefaultValueFactory = _ => "generated baseline";
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(outputOption);
        command.Options.Add(reasonOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselineGenerateCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(reasonOption) ?? "generated baseline",
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
