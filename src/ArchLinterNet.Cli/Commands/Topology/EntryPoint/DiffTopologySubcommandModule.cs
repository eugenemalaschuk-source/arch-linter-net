using System.CommandLine;
using System.CommandLine.Parsing;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Application;
using ArchLinterNet.Cli.Commands.Validate.Application;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

internal sealed class DiffTopologySubcommandModule : ITopologySubcommandModule
{
    public string CommandName => "diff";

    public Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        TopologyCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(CommandName, "Project declared topology evidence for review.");

        Option<string> policy = OptionWithDefault("--policy", "architecture/dependencies.arch.yml");
        policy.Aliases.Add("-p");
        Option<string> mode = OptionWithDefault("--mode", "strict");
        mode.Aliases.Add("-m");
        Option<bool> strict = new("--strict");
        Option<bool> audit = new("--audit");
        Option<string> format = new("--format");
        format.Aliases.Add("-f");
        Option<bool> json = new("--json");
        Option<string> output = new("--output");
        Option<string> conditionSet = new("--condition-set");
        Option<string> baseline = new("--baseline");
        Option<string[]> contract = new("--contract");
        Option<bool> ensureBuilt = new("--ensure-built");
        Option<bool> noRestore = new("--no-restore");
        Option<string> configuration = new("--configuration");
        Option<string> framework = new("--framework");
        Option<string> platform = new("--platform");
        Option<string> runtimeOption = new("--runtime");
        Option<int?> maxParallelism = new("--max-parallelism");
        Option<string> waiverEvaluationDate = new("--waiver-evaluation-date");
        Option<string[]> externalEvidence = new("--external-evidence") { AllowMultipleArgumentsPerToken = true };
        Option<string> evidenceRepository = new("--evidence-repository");
        Option<string> evidenceRevision = new("--evidence-revision");
        Option<string> evidenceScope = new("--evidence-scope");
        Option<bool> help = new("--help");
        help.Aliases.Add("-h");

        command.Options.Add(policy);
        command.Options.Add(mode);
        command.Options.Add(strict);
        command.Options.Add(audit);
        command.Options.Add(format);
        command.Options.Add(json);
        command.Options.Add(output);
        command.Options.Add(conditionSet);
        command.Options.Add(baseline);
        command.Options.Add(contract);
        command.Options.Add(ensureBuilt);
        command.Options.Add(noRestore);
        command.Options.Add(configuration);
        command.Options.Add(framework);
        command.Options.Add(platform);
        command.Options.Add(runtimeOption);
        command.Options.Add(maxParallelism);
        command.Options.Add(waiverEvaluationDate);
        command.Options.Add(externalEvidence);
        command.Options.Add(evidenceRepository);
        command.Options.Add(evidenceRevision);
        command.Options.Add(evidenceScope);
        command.Options.Add(help);
        command.SetAction(result => handler.Diff(CreateOptions(
            result, policy, mode, strict, audit, format, json, output, conditionSet, baseline, contract, help,
            ensureBuilt, noRestore, configuration, framework, platform, runtimeOption, maxParallelism,
            waiverEvaluationDate, externalEvidence, evidenceRepository, evidenceRevision, evidenceScope)));

        return command;
    }

    private static string ResolveMode(ParseResult result, Option<string> mode, Option<bool> strict, Option<bool> audit)
    {
        string selected = "strict";
        bool expectModeValue = false;
        foreach (string token in result.Tokens.Select(token => token.Value))
        {
            if (expectModeValue)
            {
                selected = token;
                expectModeValue = false;
            }
            else if (token is "--mode" or "-m")
            {
                expectModeValue = true;
            }
            else if (token is "--strict")
            {
                selected = "strict";
            }
            else if (token is "--audit")
            {
                selected = "audit";
            }
        }

        return selected;
    }

    private static TopologyDiffCommandOptions CreateOptions( // NOSONAR: System.CommandLine requires individual typed option handles.
        ParseResult result,
        Option<string> policy,
        Option<string> mode,
        Option<bool> strict,
        Option<bool> audit,
        Option<string> format,
        Option<bool> json,
        Option<string> output,
        Option<string> conditionSet,
        Option<string> baseline,
        Option<string[]> contract,
        Option<bool> help,
        Option<bool> ensureBuilt,
        Option<bool> noRestore,
        Option<string> configuration,
        Option<string> framework,
        Option<string> platform,
        Option<string> runtimeOption,
        Option<int?> maxParallelism,
        Option<string> waiverEvaluationDate,
        Option<string[]> externalEvidence,
        Option<string> evidenceRepository,
        Option<string> evidenceRevision,
        Option<string> evidenceScope)
    {
        IReadOnlyList<SarifEvidenceArtifactReference> externalEvidenceArtifacts =
            Array.Empty<SarifEvidenceArtifactReference>();
        string? externalEvidenceParseError = null;
        try
        {
            externalEvidenceArtifacts = ValidateCommandDefinition.ParseExternalEvidenceBindings(
                result.GetValue(externalEvidence));
        }
        catch (InvalidOperationException exception)
        {
            externalEvidenceParseError = exception.Message;
        }

        return new TopologyDiffCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            ResolveMode(result, mode, strict, audit),
            result.GetValue(json) ? "json" : result.GetValue(format) ?? "human",
            result.GetValue(output),
            result.GetValue(conditionSet),
            result.GetValue(baseline),
            result.GetValue(contract) ?? Array.Empty<string>(),
            result.GetValue(help),
            result.GetValue(ensureBuilt),
            result.GetValue(noRestore),
            result.GetValue(configuration),
            result.GetValue(framework),
            result.GetValue(platform),
            result.GetValue(runtimeOption),
            result.GetValue(maxParallelism))
        {
            HasFormatConflict = result.GetValue(json) && result.GetValue(format) is not null,
            WaiverEvaluationDate = result.GetValue(waiverEvaluationDate),
            ExternalEvidenceArtifacts = externalEvidenceArtifacts,
            ExternalEvidenceAssessmentContext = ValidateCommandDefinition.ResolveExternalEvidenceAssessmentContext(
                result.GetValue(evidenceRepository), result.GetValue(evidenceRevision), result.GetValue(evidenceScope)),
            ExternalEvidenceParseError = externalEvidenceParseError,
        };
    }

    private static Option<string> OptionWithDefault(string name, string value)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => value;
        return option;
    }
}
