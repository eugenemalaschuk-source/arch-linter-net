using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class DiffPublicApiSubcommandModule : IPublicApiSubcommandModule
{
    public string CommandName => "diff";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        PublicApiDiffCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName);
        Option<string> policyOption = PublicApiOptionsFactory.CreatePolicyOption();
        Option<string> contractOption = PublicApiOptionsFactory.CreateContractOption();
        Option<string> snapshotOption = new("--snapshot");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> formatOption = PublicApiOptionsFactory.CreateFormatOption();
        Option<bool> helpOption = PublicApiOptionsFactory.CreateHelpOption();

        command.Options.Add(policyOption);
        command.Options.Add(contractOption);
        command.Options.Add(snapshotOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(formatOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new PublicApiDiffCommandOptions(
            PublicApiOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(contractOption),
            parseResult.GetValue(snapshotOption),
            parseResult.GetValue(conditionSetOption),
            PublicApiOptionsFactory.GetFormat(parseResult, formatOption),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
