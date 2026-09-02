using ArchLinterNet.Cli.Commands.Topology.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Application;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class VerifyTopologySubcommandModule : TopologyValidationSubcommandModule, ITopologySubcommandModule
{
    public string CommandName => "verify";

    protected override string SubcommandName => CommandName;

    protected override string Description => "Verify declared topology using ordinary validation.";

    protected override int Execute(TopologyCommandHandler handler, TopologyValidationCommandOptions options) =>
        handler.Verify(options);
}
