using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class MigratePublicApiSubcommandModule : IPublicApiSubcommandModule
{
    public string CommandName => "migrate";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        PublicApiMigrateCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName);
        Option<string> policyOption = PublicApiOptionsFactory.CreatePolicyOption();
        Option<string> contractOption = PublicApiOptionsFactory.CreateContractOption();
        Option<string> outputOption = new("--output");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> formatOption = PublicApiOptionsFactory.CreateFormatOption();
        Option<bool> acceptDriftOption = new("--accept-drift");
        Option<bool> forceOption = new("--force");
        Option<bool> dryRunOption = new("--dry-run");
        dryRunOption.Aliases.Add("--check");
        Option<bool> helpOption = PublicApiOptionsFactory.CreateHelpOption();

        command.Options.Add(policyOption);
        command.Options.Add(contractOption);
        command.Options.Add(outputOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(formatOption);
        command.Options.Add(acceptDriftOption);
        command.Options.Add(forceOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(helpOption);

        command.SetAction(parseResult => handler.Execute(new PublicApiMigrateCommandOptions(
            PublicApiOptionsFactory.GetPolicyPath(parseResult, policyOption),
            parseResult.GetValue(contractOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(conditionSetOption),
            PublicApiOptionsFactory.GetFormat(parseResult, formatOption),
            parseResult.GetValue(acceptDriftOption),
            parseResult.GetValue(forceOption),
            parseResult.GetValue(dryRunOption),
            parseResult.GetValue(helpOption))));

        return command;
    }
}
