using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Baseline.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class GenerateBaselineSubcommandModule : IDefaultBaselineSubcommandModule
{
    public string CommandName => "generate";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return CreateCommand("generate", runtime, console, fileSystem, cancellationToken);
    }

    public Command CreateDefaultCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        return CreateCommand(commandName, runtime, console, fileSystem, cancellationToken);
    }

    private static Command CreateCommand(string commandName, ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken)
    {
        BaselineGenerateCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(commandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
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
        command.Options.Add(outputOption);
        BaselineOptionsFactory.AddTo(command, reasonOptions);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        BaselineOptionsFactory.AddTo(command, writeOptions);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new BaselineGenerateCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
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
