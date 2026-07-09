using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Graph;

internal sealed class GraphCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(GraphCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(GraphCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!runtime.TryParseGraphLevel(options.Level, out ArchitectureGraphLevel graphLevel))
        {
            console.Error.WriteLine($"Invalid level: {options.Level}. Use 'namespace', 'type', or 'assembly'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("json" or "dot" or "mermaid"))
        {
            console.Error.WriteLine($"Invalid format: {options.Format}. Use 'json', 'dot', or 'mermaid'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            console.Error.WriteLine($"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitectureGraphRequest request = new()
            {
                PolicyPath = options.PolicyPath,
                Mode = options.Mode,
                Level = graphLevel,
                ConditionSetName = options.ConditionSetName,
                ContractIds = options.ContractIds.ToList(),
            };

            ArchitectureGraphOutcome outcome = runtime.BuildGraph(request);
            console.Out.WriteLine(options.Format switch
            {
                "dot" => runtime.FormatGraphAsDot(outcome.Graph),
                "mermaid" => runtime.FormatGraphAsMermaid(outcome.Graph),
                _ => runtime.FormatGraphAsJson(outcome.Graph),
            });

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Graph export error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
