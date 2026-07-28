using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Policy;

internal sealed class PolicyCheckCommandHandler(ICliConsole console)
{
    public int Execute(PolicyCheckCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(PolicyCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            console.Error.WriteLine($"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            PolicyCheckOutcome outcome = ArchitectureValidator.CheckPolicy(options.PolicyPath);
            console.Out.WriteLine(options.Format switch
            {
                "json" => JsonSerializer.Serialize(new
                {
                    status = "valid-with-deferred-checks",
                    completed_checks = outcome.CompletedChecks,
                    deferred_checks = outcome.DeferredChecks.Select(check => new { kind = check.Kind, reason = check.Reason }),
                }),
                "sarif" => JsonSerializer.Serialize(new
                {
                    version = "2.1.0",
                    runs = new[]
                    {
                        new
                        {
                            tool = new { driver = new { name = "ArchLinterNet" } },
                            invocations = new[] { new { executionSuccessful = true } },
                            properties = new
                            {
                                status = "valid-with-deferred-checks",
                                deferredChecks = outcome.DeferredChecks,
                            },
                        },
                    },
                }),
                _ => FormatHuman(outcome),
            });
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            if (options.Format == "json" && PolicyDiagnosticOutputWriter.TryWriteJson(console, ex))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (PolicyDiagnosticOutputWriter.TryWriteHuman(console, "Policy check error", ex))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            console.Error.WriteLine($"Policy check error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static string FormatHuman(PolicyCheckOutcome outcome)
    {
        return $"Policy and static configuration are valid. Completed checks: {string.Join(", ", outcome.CompletedChecks)}.\n" +
            $"Deferred checks: {string.Join("; ", outcome.DeferredChecks.Select(check => check.Reason))}\n" +
            "Architecture compliance was not evaluated.";
    }
}
