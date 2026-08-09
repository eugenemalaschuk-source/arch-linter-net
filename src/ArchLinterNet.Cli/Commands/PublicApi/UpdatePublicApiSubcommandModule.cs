using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class UpdatePublicApiSubcommandModule : IPublicApiSubcommandModule
{
    public string CommandName => "update";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        PublicApiUpdateCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName);
        Option<string> policyOption = PublicApiOptionsFactory.CreatePolicyOption();
        Option<string> contractOption = PublicApiOptionsFactory.CreateContractOption();
        Option<string> snapshotOption = new("--snapshot");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> formatOption = PublicApiOptionsFactory.CreateFormatOption();
        Option<bool> dryRunOption = new("--dry-run");
        dryRunOption.Aliases.Add("--check");
        Option<bool> ensureBuiltOption = PublicApiOptionsFactory.CreateEnsureBuiltOption();
        Option<bool> noRestoreOption = PublicApiOptionsFactory.CreateNoRestoreOption();
        Option<bool> helpOption = PublicApiOptionsFactory.CreateHelpOption();

        command.Options.Add(policyOption);
        command.Options.Add(contractOption);
        command.Options.Add(snapshotOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(formatOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(ensureBuiltOption);
        command.Options.Add(noRestoreOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new PublicApiUpdateCommandOptions(
            PublicApiOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(contractOption),
            parseResult.GetValue(snapshotOption),
            parseResult.GetValue(conditionSetOption),
            PublicApiOptionsFactory.GetFormat(parseResult, formatOption),
            parseResult.GetValue(dryRunOption),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(ensureBuiltOption),
            parseResult.GetValue(noRestoreOption))));

        return command;
    }
}
