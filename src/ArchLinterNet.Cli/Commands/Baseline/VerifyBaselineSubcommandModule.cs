using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class VerifyBaselineSubcommandModule : IBaselineSubcommandModule
{
    private readonly BaselineVerifyCommandHandler _handler;

    public VerifyBaselineSubcommandModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _handler = new BaselineVerifyCommandHandler(runtime, console, fileSystem);
    }

    public string CommandName => "verify";

    public Command CreateCommand()
    {
        Command command = new(CommandName);
        Option<string> policyOption = BaselineOptionsFactory.CreatePolicyOption();
        Option<string> baselineOption = new("--baseline");
        Option<string> modeOption = BaselineOptionsFactory.CreateModeOption();
        Option<string> conditionSetOption = new("--condition-set");
        Option<string[]> contractOption = new("--contract");
        Option<bool> jsonOption = new("--json");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");

        command.Options.Add(policyOption);
        command.Options.Add(baselineOption);
        command.Options.Add(modeOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(contractOption);
        command.Options.Add(jsonOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => _handler.Execute(new BaselineVerifyCommandOptions(
            BaselineOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(modeOption) ?? "all",
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(jsonOption) ? "json" : "human",
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
