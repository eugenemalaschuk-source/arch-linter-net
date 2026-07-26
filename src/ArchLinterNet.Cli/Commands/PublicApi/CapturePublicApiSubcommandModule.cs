using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class CapturePublicApiSubcommandModule : IPublicApiSubcommandModule
{
    public string CommandName => "capture";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        PublicApiCaptureCommandHandler handler = new(runtime, console, fileSystem);
        Command command = new(CommandName);
        Option<string> policyOption = PublicApiOptionsFactory.CreatePolicyOption();
        Option<string> contractOption = PublicApiOptionsFactory.CreateContractOption();
        Option<string> outputOption = new("--output");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> formatOption = PublicApiOptionsFactory.CreateFormatOption();
        Option<bool> forceOption = new("--force");
        Option<bool> helpOption = PublicApiOptionsFactory.CreateHelpOption();

        command.Options.Add(policyOption);
        command.Options.Add(contractOption);
        command.Options.Add(outputOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(formatOption);
        command.Options.Add(forceOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new PublicApiCaptureCommandOptions(
            PublicApiOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(contractOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(conditionSetOption),
            PublicApiOptionsFactory.GetFormat(parseResult, formatOption),
            parseResult.GetValue(forceOption),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
