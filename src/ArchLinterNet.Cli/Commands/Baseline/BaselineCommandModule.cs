using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Baseline;

internal sealed class BaselineCommandModule : ICliSubcommandModule
{
    private readonly IReadOnlyList<IBaselineSubcommandModule> _subcommandModules;

    public BaselineCommandModule(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
    {
        _subcommandModules = BaselineSubcommandCatalog.CreateModules(runtime, console, fileSystem);
    }

    public string CommandName => "baseline";

    public Command CreateCommand()
    {
        IDefaultBaselineSubcommandModule defaultModule = _subcommandModules.OfType<IDefaultBaselineSubcommandModule>().Single();
        Command baselineCommand = defaultModule.CreateDefaultCommand("baseline");

        foreach (IBaselineSubcommandModule module in _subcommandModules.OrderBy(static module => module.CommandName, StringComparer.Ordinal))
        {
            baselineCommand.Subcommands.Add(module.CreateCommand());
        }

        return baselineCommand;
    }
}
