using ArchLinterNet.Cli.Commands.Topology.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Application;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class DiffTopologySubcommandModule : TopologyValidationSubcommandModule, ITopologySubcommandModule
{
    public string CommandName => "diff";

    protected override string SubcommandName => CommandName;

    protected override string Description => "Project declared topology evidence for review.";

    protected override int Execute(TopologyCommandHandler handler, TopologyValidationCommandOptions options) =>
        handler.Diff(options);
}
