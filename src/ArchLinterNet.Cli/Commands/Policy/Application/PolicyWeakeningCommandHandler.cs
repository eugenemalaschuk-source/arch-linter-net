using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.PolicyWeakening;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed class PolicyWeakeningCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    private const string HelpText =
        """
        arch-linter-net policy weakening — compare separately exported effective policy contexts

        Usage:
          arch-linter-net policy weakening --base-context <path> --current-context <path> [options]

        Produce each JSON context in its own repository/policy state with:
          arch-linter-net policy context --policy <path> --format json

        This command reads only the supplied artifacts. It does not load policy YAML, build projects,
        analyze assemblies, or simulate a candidate policy.

        Options:
          --base-context <path>     JSON policy context from the base state
          --current-context <path>  JSON policy context from the current state
          -f, --format <fmt>        Output format: human, json, or sarif (default: human)
          -h, --help                Show this help message

        Exit codes:
          0   Comparison completed with no error-severity weakening
          1   Comparison completed with error-severity weakening
          2   Invalid arguments, unreadable artifact, or invalid comparison input
        """;

    public int Execute(PolicyWeakeningCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "invalid-format",
                $"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (string.IsNullOrWhiteSpace(options.BaseContextPath) || string.IsNullOrWhiteSpace(options.CurrentContextPath))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "missing-policy-context",
                "Both --base-context and --current-context are required.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitecturePolicyWeakeningResult result = runtime.ComparePolicyWeakening(new ArchitecturePolicyWeakeningRequest(
                ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.BaseContextPath)),
                ArchitecturePolicyWeakeningFormatter.DeserializeContext(fileSystem.ReadAllText(options.CurrentContextPath))));
            console.Out.WriteLine(options.Format switch
            {
                "json" => runtime.FormatPolicyWeakeningAsJson(result),
                "sarif" => runtime.FormatPolicyWeakeningAsSarif(result),
                _ => runtime.FormatPolicyWeakeningAsHuman(result),
            });
            return result.HasErrors ? CliExitCodes.ValidationFailure : CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "policy-weakening-comparison-error",
                $"Policy weakening comparison error: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
