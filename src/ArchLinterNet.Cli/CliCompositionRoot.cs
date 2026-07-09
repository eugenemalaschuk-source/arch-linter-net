using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Cli.Commands.Explain;
using ArchLinterNet.Cli.Commands.Graph;
using ArchLinterNet.Cli.Commands.Validate;

namespace ArchLinterNet.Cli;

internal sealed class CliCompositionRoot
{
    public CliComposition Compose()
    {
        ICliConsole console = new SystemCliConsole();
        IFileSystem fileSystem = new FileSystem();
        ICliRuntime runtime = new CliRuntime();

        ValidateCommandHandler validateHandler = new(runtime, console, fileSystem);
        ValidateCommandDefinition validateDefinition = new(validateHandler);

        BaselineGenerateCommandHandler baselineGenerateHandler = new(runtime, console, fileSystem);
        BaselineUpdateCommandHandler baselineUpdateHandler = new(runtime, console, fileSystem);
        BaselinePruneCommandHandler baselinePruneHandler = new(runtime, console, fileSystem);
        BaselineDiffCommandHandler baselineDiffHandler = new(runtime, console, fileSystem);
        BaselineVerifyCommandHandler baselineVerifyHandler = new(runtime, console, fileSystem);
        BaselineCommandDefinition baselineDefinition = new(
            baselineGenerateHandler,
            baselineUpdateHandler,
            baselinePruneHandler,
            baselineDiffHandler,
            baselineVerifyHandler);

        GraphCommandHandler graphHandler = new(runtime, console, fileSystem);
        GraphCommandDefinition graphDefinition = new(graphHandler);

        ExplainCommandHandler explainHandler = new(runtime, console, fileSystem);
        ExplainCommandDefinition explainDefinition = new(explainHandler);

        ICliRootCommandFactory rootCommandFactory = new CliRootCommandFactory(
            validateDefinition,
            baselineDefinition,
            graphDefinition,
            explainDefinition);

        CliHost host = new(rootCommandFactory, console);

        return new CliComposition(
            host,
            rootCommandFactory,
            runtime,
            validateHandler,
            validateDefinition);
    }

    public CliHost CreateHost()
    {
        return Compose().Host;
    }
}

internal sealed record CliComposition(
    CliHost Host,
    ICliRootCommandFactory RootCommandFactory,
    ICliRuntime Runtime,
    ValidateCommandHandler ValidateHandler,
    ValidateCommandDefinition ValidateDefinition);
