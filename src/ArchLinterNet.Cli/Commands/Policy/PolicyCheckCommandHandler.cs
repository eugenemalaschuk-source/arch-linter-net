using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
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
            if (!outcome.IsValid)
            {
                WriteFailure(options.Format, outcome.Failure!);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            console.Out.WriteLine(options.Format switch
            {
                "json" => FormatJson(outcome),
                "sarif" => FormatSarif(outcome, failure: null),
                _ => FormatHuman(outcome),
            });
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            if (options.Format == "sarif")
            {
                console.Out.WriteLine(FormatSarif(
                    new PolicyCheckOutcome(Array.Empty<string>(), Array.Empty<PolicyCheckDeferredCheck>()),
                    new PolicyCheckFailure(ex.Message, "unexpected-tool-failure", null)));
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

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
            $"Deferred checks: {string.Join("; ", outcome.DeferredChecks.Select(FormatDeferredForHuman))}\n" +
            "Architecture compliance was not evaluated.";
    }

    private void WriteFailure(string format, PolicyCheckFailure failure)
    {
        if (format == "human")
        {
            console.Error.WriteLine(FormatFailureForHuman(failure));
            return;
        }

        PolicyCheckOutcome outcome = PolicyCheckOutcome.Invalid(failure);
        console.Out.WriteLine(format == "json" ? FormatJson(outcome) : FormatSarif(outcome, failure));
    }

    private static string FormatJson(PolicyCheckOutcome outcome)
    {
        return JsonSerializer.Serialize(new
        {
            status = outcome.IsValid
                ? outcome.DeferredChecks.Count == 0 ? "valid" : "valid-with-deferred-checks"
                : "invalid-policy",
            completed_checks = outcome.CompletedChecks,
            deferred_checks = outcome.DeferredChecks.Select(FormatDeferred),
            failure = outcome.Failure is null ? null : FormatFailure(outcome.Failure),
        });
    }

    private static string FormatSarif(PolicyCheckOutcome outcome, PolicyCheckFailure? failure)
    {
        object[] results = failure is null
            ? Array.Empty<object>()
            :
            [
                new Dictionary<string, object?>
                {
                    ["ruleId"] = "architecture-policy",
                    ["level"] = "error",
                    ["message"] = new { text = failure.Message },
                    ["properties"] = FormatFailure(failure),
                    ["relatedLocations"] = ArchitectureSarifFormatter.FormatPolicyLocationsForSarif(
                        failure.Diagnostic?.Location,
                        failure.Diagnostic?.RelatedLocations ?? Array.Empty<ArchitecturePolicySourceLocation>()),
                },
            ];

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$schema"] = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new[]
            {
                new
                {
                    tool = new { driver = new { name = "ArchLinterNet" } },
                    invocations = new[] { new { executionSuccessful = failure is null } },
                    results,
                    properties = new
                    {
                        status = failure is null
                            ? outcome.DeferredChecks.Count == 0 ? "valid" : "valid-with-deferred-checks"
                            : "invalid-policy",
                        completedChecks = outcome.CompletedChecks,
                        deferredChecks = outcome.DeferredChecks.Select(FormatDeferred),
                    },
                },
            },
        });
    }

    private static object FormatDeferred(PolicyCheckDeferredCheck check)
    {
        return new
        {
            kind = check.Kind,
            reason = check.Reason,
            contract_family = check.ContractFamily,
            contract_id = check.ContractId,
            policy_locations = check.PolicyLocations.Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
        };
    }

    private static object FormatFailure(PolicyCheckFailure failure)
    {
        return new
        {
            message = failure.Message,
            category = failure.Category,
            diagnostic_kind = failure.Diagnostic?.Kind.ToString(),
            policy_location = failure.Diagnostic?.Location is null
                ? null
                : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(failure.Diagnostic.Location),
            related_policy_locations = failure.Diagnostic?.RelatedLocations
                .Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
            import_chain = failure.Diagnostic?.ImportChain,
        };
    }

    private static string FormatDeferredForHuman(PolicyCheckDeferredCheck check)
    {
        string contract = check.ContractId is null ? string.Empty : $" (contract: {check.ContractId})";
        string locations = check.PolicyLocations.Count == 0
            ? string.Empty
            : $" (policy: {string.Join(", ", check.PolicyLocations.Select(location => $"{location.SourcePath}:{location.YamlPath}"))})";
        return check.Reason + contract + locations;
    }

    private static string FormatFailureForHuman(PolicyCheckFailure failure)
    {
        ArchitecturePolicyDiagnostic? diagnostic = failure.Diagnostic;
        string location = diagnostic?.Location is null
            ? string.Empty
            : $" (policy: {diagnostic.Location.SourcePath}:{diagnostic.Location.YamlPath}; root: {diagnostic.Location.RootPath})";
        string importChain = diagnostic is { ImportChain.Count: > 0 }
            ? $"\nImport chain: {string.Join(" -> ", diagnostic.ImportChain)}"
            : string.Empty;
        return $"Policy check error: {failure.Message}{location}{importChain}";
    }
}
