using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineCommandModule : ITopLevelCliSubcommandModule
{
    public string CommandName => "baseline";

    public Command CreateCommand(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        IReadOnlyList<IBaselineSubcommandModule> subcommandModules = BaselineSubcommandCatalog.CreateModules();
        IDefaultBaselineSubcommandModule defaultModule = subcommandModules.OfType<IDefaultBaselineSubcommandModule>().Single();
        Command baselineCommand = defaultModule.CreateDefaultCommand("baseline", runtime, console, fileSystem);

        foreach (IBaselineSubcommandModule module in subcommandModules.OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            baselineCommand.Subcommands.Add(module.CreateCommand(runtime, console, fileSystem));
        }

        return baselineCommand;
    }
}
