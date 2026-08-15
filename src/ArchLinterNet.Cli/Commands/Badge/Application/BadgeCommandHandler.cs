using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed class BadgeCommandHandler(ICliRuntime runtime, ICliConsole console, CancellationToken cancellationToken)
{
    public int Execute(BadgeCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net badge architecture-policy [--policy <path>] [--ensure-built] [--no-restore] [--configuration <name>]");
            return CliExitCodes.Success;
        }

        try
        {
            ValidationOutcome outcome = runtime.Validate(new ValidationRequest
            {
                PolicyPath = options.PolicyPath,
                Mode = "strict",
                EnforceUnmatchedIgnoredViolationsPolicy = true,
                PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                NoRestore = options.NoRestore,
                RequestedConfiguration = options.Configuration,
                CancellationToken = cancellationToken,
            }, null);
            Write(outcome.Passed ? "passing" : "failing", outcome.Passed ? "brightgreen" : "red");
            return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (Exception)
        {
            Write("unavailable", "red");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void Write(string message, string color) => console.Out.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        label = "architecture policy",
        message,
        color,
    }));
}
