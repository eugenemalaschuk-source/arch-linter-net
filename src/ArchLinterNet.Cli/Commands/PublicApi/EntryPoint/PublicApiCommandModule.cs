using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.PublicApi.Abstractions;

namespace ArchLinterNet.Cli.Commands.PublicApi.EntryPoint;

internal sealed class PublicApiCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "public-api";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        Command publicApiCommand = new(CommandName);
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");
        publicApiCommand.Options.Add(helpOption);
        publicApiCommand.SetAction(_ =>
        {
            console.Out.WriteLine(PublicApiHelpTexts.HelpText);
            return CliExitCodes.Success;
        });

        foreach (IPublicApiSubcommandModule module in PublicApiSubcommandCatalog.CreateModules()
                     .OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            publicApiCommand.Subcommands.Add(module.CreateCommand(runtime, console, fileSystem, cancellationToken));
        }

        return publicApiCommand;
    }
}
