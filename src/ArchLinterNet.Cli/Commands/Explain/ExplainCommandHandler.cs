using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Explain;

internal sealed class ExplainCommandHandler(ICliRuntime runtime, ICliConsole console)
{
    private const string PolicyLocationKey = "policyLocation";
    private const string MatchedKey = "matched";
    private const string SourceKey = "source";
    private const string ContractIdKey = "contractId";

    public int Execute(ExplainCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(ExplainCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.Mode is not ("strict" or "audit" or "all"))
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments",
                $"Invalid mode: {options.Mode}. Use 'strict', 'audit', or 'all'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!runtime.TryParseGraphLevel(options.Level, out ArchitectureGraphLevel graphLevel))
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments",
                $"Invalid level: {options.Level}. Use 'namespace' or 'type'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not ("human" or "json"))
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-format",
                $"Invalid format: {options.Format}. Use 'human' or 'json'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (string.IsNullOrEmpty(options.Source) || string.IsNullOrEmpty(options.Target))
        {
            CliErrorOutputWriter.Write(console, options.Format, "invalid-arguments", "--source and --target are required.");
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
                    [SourceKey] = outcome.Source,
                    ["target"] = outcome.Target,
                    ["path"] = outcome.Path,
                    ["contractIds"] = outcome.ContractIds,
                    ["coverageSummary"] = outcome.CoverageSummaries.Select(summary => new Dictionary<string, object?>
                    {
                        [ContractIdKey] = summary.ContractId,
                        ["optionalEmptyItems"] = summary.OptionalEmptyItems.Select(item => new Dictionary<string, object?>
                        {
                            ["item"] = item.Item,
                            [ContractIdKey] = item.ContractId,
                            ["input"] = item.Input,
                            ["layer"] = item.Layer,
                            ["reason"] = item.Reason,
                            ["evidence"] = item.Evidence,
                            [PolicyLocationKey] = item.PolicyLocation is null ? null : new Dictionary<string, object?>
                            {
                                ["sourcePath"] = item.PolicyLocation.SourcePath,
                                ["yamlPath"] = item.PolicyLocation.YamlPath
                            }
                        }).ToArray()
                    }).ToArray(),
                    ["sourceSetExpansion"] = new Dictionary<string, object?>
                    {
                        ["sets"] = outcome.SourceExpansion.Sets.Select(set => new Dictionary<string, object?>
                        {
                            ["name"] = set.Name,
                            ["kind"] = set.Kind.ToString().ToLowerInvariant(),
                            ["resolvedSources"] = set.ResolvedSources,
                            ["optional"] = set.Optional,
                            ["reason"] = set.Reason,
                            [PolicyLocationKey] = FormatPolicyLocation(set.PolicyLocation)
                        }).ToArray(),
                        ["contracts"] = outcome.SourceExpansion.Contracts.Select(expansion => new Dictionary<string, object?>
                        {
                            ["group"] = expansion.Group,
                            ["authoredContractId"] = expansion.AuthoredContractId,
                            ["authoredContractName"] = expansion.AuthoredContractName,
                            ["kind"] = expansion.Kind switch
                            {
                                ArchitectureContractExpansionKind.FanOut => "fan_out",
                                ArchitectureContractExpansionKind.ContainerSet => "container_set",
                                _ => "inline_union",
                            },
                            ["selectorField"] = expansion.SelectorField,
                            ["sourceSets"] = expansion.SetNames,
                            ["optionalEmpty"] = expansion.OptionalEmpty,
                            ["optionalReason"] = expansion.OptionalReason,
                            [PolicyLocationKey] = FormatPolicyLocation(expansion.PolicyLocation),
                            ["exclusions"] = expansion.Exclusions.Select(exclusion => new Dictionary<string, object?>
                            {
                                [SourceKey] = exclusion.Source,
                                ["sourceSet"] = exclusion.SetName,
                                ["selector"] = exclusion.Selector,
                                [MatchedKey] = exclusion.Matched,
                                ["optionalEmpty"] = exclusion.OptionalEmpty,
                                ["optionalReason"] = exclusion.OptionalReason,
                                [PolicyLocationKey] = FormatPolicyLocation(exclusion.PolicyLocation)
                            }).ToArray(),
                            ["inclusions"] = expansion.Inclusions.Select(instance => new Dictionary<string, object?>
                            {
                                [ContractIdKey] = instance.ContractId,
                                [SourceKey] = instance.Source,
                                ["sourceSet"] = instance.SetName,
                                ["selector"] = instance.Selector,
                                ["optionalEmpty"] = instance.OptionalEmpty,
                                ["optionalReason"] = instance.OptionalReason,
                                [PolicyLocationKey] = FormatPolicyLocation(instance.PolicyLocation),
                                ["authoredContractPolicyLocation"] = FormatPolicyLocation(instance.AuthoredContractPolicyLocation),
                                ["sourceSetReferencePolicyLocation"] = FormatPolicyLocation(instance.SourceSetReferencePolicyLocation)
                            }).ToArray(),
                            ["instances"] = expansion.Instances.Select(instance => new Dictionary<string, object?>
                            {
                                [ContractIdKey] = instance.ContractId,
                                [SourceKey] = instance.Source,
                                ["sourceSet"] = instance.SetName,
                                ["selector"] = instance.Selector,
                                [PolicyLocationKey] = FormatPolicyLocation(instance.PolicyLocation),
                                ["authoredContractPolicyLocation"] = FormatPolicyLocation(instance.AuthoredContractPolicyLocation),
                                ["sourceSetReferencePolicyLocation"] = FormatPolicyLocation(instance.SourceSetReferencePolicyLocation)
                            }).ToArray()
                        }).ToArray()
                    },
                    ["selectorParticipation"] = outcome.SelectorParticipation.Select(participation => new Dictionary<string, object?>
                    {
                        [ContractIdKey] = participation.ContractId,
                        ["contractName"] = participation.ContractName,
                        ["mode"] = participation.Mode.ToString().ToLowerInvariant(),
                        ["kind"] = participation.Kind == ArchitectureSelectorParticipationKind.Inclusion
                            ? "inclusion"
                            : "exclusion",
                        ["field"] = participation.Field,
                        ["index"] = participation.Index,
                        [MatchedKey] = participation.Matched,
                        ["staleExclusion"] = participation.IsStaleExclusion,
                        ["evaluationFailed"] = participation.EvaluationFailed,
                        [PolicyLocationKey] = FormatPolicyLocation(participation.PolicyLocation)
                    }).ToArray()
                };

                if (outcome.ExpressionParticipation.Count > 0)
                {
                    jsonObj["expressionParticipation"] = outcome.ExpressionParticipation.Select(p => new Dictionary<string, object?>
                    {
                        [ContractIdKey] = p.ContractId,
                        ["hopSource"] = p.HopSource,
                        ["hopTarget"] = p.HopTarget,
                        [SourceKey] = p.Source,
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

            if (options.Format == "human")
            {
                foreach (var optionalInput in outcome.CoverageSummaries.SelectMany(summary => summary.OptionalEmptyItems))
                {
                    string policy = optionalInput.PolicyLocation is null
                        ? string.Empty
                        : $" (policy: {optionalInput.PolicyLocation.SourcePath}:{optionalInput.PolicyLocation.YamlPath})";
                    console.Out.WriteLine($"Optional empty input: {optionalInput.Item} ({optionalInput.Reason}){policy}");
                }

                foreach (ArchitectureContractExpansion expansion in outcome.SourceExpansion.Contracts)
                {
                    string policy = FormatPolicySuffix(expansion.PolicyLocation);

                    if (expansion.OptionalEmpty)
                    {
                        console.Out.WriteLine(
                            $"Source expansion: [{expansion.AuthoredContractId}] optional-empty ({expansion.OptionalReason}){policy}");
                    }
                    else if (expansion.Instances.Count == 0)
                    {
                        console.Out.WriteLine(
                            $"Source expansion: [{expansion.AuthoredContractId}] fully excluded{policy}");
                    }
                    else
                    {
                        foreach (ArchitectureExpandedContractInstance instance in expansion.Instances)
                        {
                            string set = instance.SetName is null ? "sources" : $"set '{instance.SetName}'";
                            string selectorField = expansion.SelectorField is null ? string.Empty :
                                $" ({expansion.SelectorField})";
                            string instancePolicy = FormatInstancePolicySuffix(instance);
                            console.Out.WriteLine(
                                $"Source expansion: [{expansion.AuthoredContractId}]{selectorField} {set} -> {instance.Source} " +
                                $"(selector: {instance.Selector}; id: {instance.ContractId}){instancePolicy}");
                        }
                    }

                    foreach (ArchitectureExpandedContractInstance inclusion in expansion.Inclusions)
                    {
                        string set = inclusion.SetName is null ? "sources" : $"set '{inclusion.SetName}'";
                        if (inclusion.OptionalEmpty)
                        {
                            console.Out.WriteLine(
                                $"Source expansion inclusion: [{expansion.AuthoredContractId}] optional-empty {set} " +
                                $"({inclusion.OptionalReason}){FormatInstancePolicySuffix(inclusion)}");
                            continue;
                        }

                        console.Out.WriteLine(
                            $"Source expansion inclusion: [{expansion.AuthoredContractId}] {set} -> {inclusion.Source} " +
                            $"(selector: {inclusion.Selector}; id: {inclusion.ContractId}){FormatInstancePolicySuffix(inclusion)}");
                    }

                    foreach (ArchitectureExpandedContractExclusion exclusion in expansion.Exclusions)
                    {
                        string set = exclusion.SetName is null ? "sources" : $"set '{exclusion.SetName}'";
                        string exclusionPolicy = FormatPolicySuffix(exclusion.PolicyLocation);

                        if (exclusion.OptionalEmpty)
                        {
                            console.Out.WriteLine(
                                $"Source expansion exclusion: [{expansion.AuthoredContractId}] optional-empty {set} " +
                                $"({exclusion.OptionalReason}){exclusionPolicy}");
                            continue;
                        }

                        string state = exclusion.Matched ? "matched" : "stale";
                        console.Out.WriteLine(
                            $"Source expansion exclusion: [{expansion.AuthoredContractId}] {state} {set} -> {exclusion.Source} " +
                            $"(selector: {exclusion.Selector}){exclusionPolicy}");
                    }
                }

                foreach (ArchitectureSubtractiveMatcherParticipation participation in outcome.SelectorParticipation)
                {
                    string state = DescribeParticipationState(participation);
                    string kind = participation.Kind == ArchitectureSelectorParticipationKind.Inclusion
                        ? "inclusion"
                        : "exclusion";
                    console.Out.WriteLine(
                        $"Selector participation: [{participation.ContractId}] {participation.Mode.ToString().ToLowerInvariant()} " +
                        $"{kind} {participation.Field}{FormatSelectorIndex(participation.Index)} " +
                        $"{state}{FormatPolicySuffix(participation.PolicyLocation)}");
                }
            }

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            if (options.Format == "json" && PolicyDiagnosticOutputWriter.TryWriteJson(console, ex))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (PolicyDiagnosticOutputWriter.TryWriteHuman(console, "Explain error", ex))
            {
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"Explain error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static Dictionary<string, object?>? FormatPolicyLocation(ArchitecturePolicySourceLocation? location) =>
        location is null
            ? null
            : new Dictionary<string, object?>
            {
                ["sourcePath"] = location.SourcePath,
                ["yamlPath"] = location.YamlPath
            };

    private static string FormatPolicySuffix(ArchitecturePolicySourceLocation? location) =>
        location is null ? string.Empty : $" (policy: {location.SourcePath}:{location.YamlPath})";

    private static string FormatSelectorIndex(int? index) => index is int value ? $"[{value}]" : string.Empty;

    private static string DescribeParticipationState(ArchitectureSubtractiveMatcherParticipation participation)
    {
        if (participation.Matched && participation.EvaluationFailed)
        {
            return "matched; evaluation failed";
        }

        if (participation.EvaluationFailed)
        {
            return "evaluation failed";
        }

        if (participation.Matched)
        {
            return "matched";
        }

        return participation.IsStaleExclusion ? "stale" : "not matched";
    }

    private static string FormatInstancePolicySuffix(ArchitectureExpandedContractInstance instance)
    {
        List<string> locations = new();
        if (instance.AuthoredContractPolicyLocation is not null)
        {
            locations.Add($"contract: {instance.AuthoredContractPolicyLocation.SourcePath}:{instance.AuthoredContractPolicyLocation.YamlPath}");
        }

        if (instance.SourceSetReferencePolicyLocation is not null)
        {
            locations.Add($"source set reference: {instance.SourceSetReferencePolicyLocation.SourcePath}:{instance.SourceSetReferencePolicyLocation.YamlPath}");
        }

        if (instance.PolicyLocation is not null)
        {
            locations.Add($"selector: {instance.PolicyLocation.SourcePath}:{instance.PolicyLocation.YamlPath}");
        }

        return locations.Count == 0 ? string.Empty : $" (policy: {string.Join("; ", locations)})";
    }
}
