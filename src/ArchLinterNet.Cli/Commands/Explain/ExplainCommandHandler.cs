using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed class ExplainCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(ExplainCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(ExplainCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            console.Error.WriteLine($"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!runtime.TryParseGraphLevel(options.Level, out ArchitectureGraphLevel graphLevel))
        {
            console.Error.WriteLine($"Invalid level: {options.Level}. Use 'namespace' or 'type'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json"))
        {
            console.Error.WriteLine($"Invalid format: {options.Format}. Use 'human' or 'json'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (string.IsNullOrEmpty(options.Source) || string.IsNullOrEmpty(options.Target))
        {
            console.Error.WriteLine("--source and --target are required.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!fileSystem.FileExists(options.PolicyPath))
        {
            console.Error.WriteLine($"Policy file not found: {options.PolicyPath}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            ArchitectureExplainRequest request = new()
            {
                PolicyPath = options.PolicyPath,
                Source = options.Source,
                Target = options.Target,
                Mode = options.Mode,
                Level = graphLevel,
                ConditionSetName = options.ConditionSetName,
            };

            ArchitectureExplainOutcome outcome = runtime.Explain(request);
            if (options.Format == "json")
            {
                var jsonObj = new Dictionary<string, object?>
                {
                    ["source"] = outcome.Source,
                    ["target"] = outcome.Target,
                    ["path"] = outcome.Path,
                    ["contractIds"] = outcome.ContractIds,
                };

                if (outcome.ExpressionParticipation.Count > 0)
                {
                    jsonObj["expressionParticipation"] = outcome.ExpressionParticipation.Select(p => new Dictionary<string, object?>
                    {
                        ["contractId"] = p.ContractId,
                        ["hopSource"] = p.HopSource,
                        ["hopTarget"] = p.HopTarget,
                        ["source"] = p.Source,
                        ["yamlPath"] = p.YamlPath,
                        ["result"] = p.Result switch
                        {
                            ExpressionParticipationResult.Matched => "matched",
                            ExpressionParticipationResult.NotMatched => "not_matched",
                            _ => "evaluation_failed",
                        },
                    }).ToArray();
                }

                console.Out.WriteLine(JsonSerializer.Serialize(jsonObj));
            }
            else if (outcome.Path == null)
            {
                console.Out.WriteLine($"No dependency path found from '{outcome.Source}' to '{outcome.Target}'.");
            }
            else
            {
                console.Out.WriteLine(string.Join(" -> ", outcome.Path));
                if (outcome.ContractIds.Count > 0)
                {
                    console.Out.WriteLine($"Contract IDs: {string.Join(", ", outcome.ContractIds)}");
                }

                foreach (ExplainExpressionParticipation participation in outcome.ExpressionParticipation)
                {
                    string result = participation.Result switch
                    {
                        ExpressionParticipationResult.Matched => "matched",
                        ExpressionParticipationResult.NotMatched => "not matched",
                        _ => "evaluation failed",
                    };
                    string hop = participation.HopSource != null && participation.HopTarget != null
                        ? $"{participation.HopSource} -> {participation.HopTarget}: "
                        : string.Empty;
                    console.Out.WriteLine(
                        $"  [{participation.ContractId}] {hop}when: {participation.Source} ({result})");
                }
            }

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Explain error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
