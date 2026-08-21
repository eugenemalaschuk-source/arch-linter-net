using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed class PolicyContextCommandHandler(ICliRuntime runtime, ICliConsole console)
{
    private const string HelpText =
        """
        arch-linter-net policy context — export effective policy facts for coding agents

        Usage:
          arch-linter-net policy context --policy <path> [options]

        This command does not build projects, load target assemblies, or validate architecture results.

        Options:
          -p, --policy <path>   Path to YAML contract file
                                (default: architecture/dependencies.arch.yml)
          -f, --format <fmt>    Output format: json or markdown (default: markdown)
          -h, --help            Show this help message

        Exit codes:
          0   Effective policy context exported
          2   Invalid arguments or policy-loading error
        """;

    public int Execute(PolicyContextCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (options.Format is not ("json" or "markdown"))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "invalid-format",
                $"Invalid format: {options.Format}. Use 'json' or 'markdown'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitecturePolicyContextExport context = runtime.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = options.PolicyPath });
            console.Out.WriteLine(options.Format == "json"
                ? runtime.FormatPolicyContextAsJson(context)
                : runtime.FormatPolicyContextAsMarkdown(context));
            return CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            if (options.Format == "json" && PolicyDiagnosticOutputWriter.TryWriteJson(console, exception))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (PolicyDiagnosticOutputWriter.TryWriteHuman(console, "Policy context export error", exception))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "unexpected-tool-failure",
                $"Policy context export error: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
