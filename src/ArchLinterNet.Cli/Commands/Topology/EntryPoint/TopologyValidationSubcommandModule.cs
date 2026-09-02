using System.CommandLine;
using System.CommandLine.Parsing;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Abstractions;
using ArchLinterNet.Cli.Commands.Topology.Application;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Topology.EntryPoint;

/// <summary>Defines the shared ordinary-validation inputs for topology diff and verify.</summary>
internal abstract class TopologyValidationSubcommandModule
{
    protected abstract string SubcommandName { get; }

    protected abstract string Description { get; }

    public Command CreateCommand(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        TopologyCommandHandler handler = new(runtime, console, fileSystem, cancellationToken);
        Command command = new(SubcommandName, Description);

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

        foreach (Option option in new Option[]
        {
            policy, mode, strict, audit, format, json, output, conditionSet, baseline, contract, help,
            ensureBuilt, noRestore, configuration, framework, platform, runtimeOption, maxParallelism,
            waiverEvaluationDate, externalEvidence, evidenceRepository, evidenceRevision, evidenceScope,
        })
        {
            command.Options.Add(option);
        }

        command.SetAction(result => Execute(handler, CreateOptions(
            result, policy, format, json, output, conditionSet, baseline, contract, help,
            ensureBuilt, noRestore, configuration, framework, platform, runtimeOption, maxParallelism,
            waiverEvaluationDate, externalEvidence, evidenceRepository, evidenceRevision, evidenceScope)));
        return command;
    }

    protected abstract int Execute(TopologyCommandHandler handler, TopologyValidationCommandOptions options);

    private static string ResolveMode(ParseResult result)
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

    private static TopologyValidationCommandOptions CreateOptions( // NOSONAR: System.CommandLine requires individual typed option handles.
        ParseResult result,
        Option<string> policy,
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
            externalEvidenceArtifacts = ExternalEvidenceCommandSupport.ParseBindings(
                result.GetValue(externalEvidence));
        }
        catch (InvalidOperationException exception)
        {
            externalEvidenceParseError = exception.Message;
        }

        return new TopologyValidationCommandOptions(
            result.GetValue(policy) ?? "architecture/dependencies.arch.yml",
            ResolveMode(result),
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
            ExternalEvidenceAssessmentContext = ExternalEvidenceCommandSupport.ResolveAssessmentContext(
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
