using System.CommandLine;
using ArchLinterNet.Cli;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandDefinition(HealthCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("health", "Project the canonical architecture-health/v1 summary.");
        Command revalidatePublication = new(
            "revalidate-publication",
            "Refresh temporal publication evidence from a serialized Health artifact.");
        Option<string> revalidateInput = RequiredOption("--input", "Health artifact path");
        revalidateInput.Aliases.Add("--health");
        Option<string> revalidateDate = RequiredOption("--evaluation-date", "Explicit UTC evaluation date");
        revalidateDate.Aliases.Add("--date");
        Option<string> sourceHealthSha256 = RequiredOption("--source-health-sha256", "SHA-256 of exact Health input bytes");
        sourceHealthSha256.Aliases.Add("--health-sha256");
        Option<string> badgePayloadSha256 = RequiredOption("--badge-payload-sha256", "SHA-256 of exact badge payload bytes");
        badgePayloadSha256.Aliases.Add("--payload-sha256");
        Option<string> mergedTreeSha = RequiredOption("--merged-tree-sha", "Exact merged-tree SHA");
        mergedTreeSha.Aliases.Add("--tree-sha");
        Option<string> producerIdentitySha256 = RequiredOption("--producer-identity-sha256", "SHA-256 of verified producer identity");
        producerIdentitySha256.Aliases.Add("--producer-sha256");
        Option<string> revalidateOutput = new("--output");
        Option<bool> revalidateHelp = new("--help");
        revalidateHelp.Aliases.Add("-h");
        foreach (Option option in new Option[]
        {
            revalidateInput, revalidateDate, sourceHealthSha256, badgePayloadSha256,
            mergedTreeSha, producerIdentitySha256, revalidateOutput, revalidateHelp,
        })
        {
            revalidatePublication.Options.Add(option);
        }

        revalidatePublication.SetAction(result => handler.ExecuteRevalidatePublication(new HealthRevalidatePublicationCommandOptions(
                result.GetValue(revalidateInput) ?? string.Empty,
                result.GetValue(revalidateDate) ?? string.Empty,
                result.GetValue(sourceHealthSha256) ?? string.Empty,
                result.GetValue(badgePayloadSha256) ?? string.Empty,
                result.GetValue(mergedTreeSha) ?? string.Empty,
                result.GetValue(producerIdentitySha256) ?? string.Empty,
                result.GetValue(revalidateOutput),
                result.GetValue(revalidateHelp))));

        command.Subcommands.Add(revalidatePublication);
        ArchitectureAnalysisCommandOptionSet options = new();
        Option<string> executionContext = new("--execution-context");
        Option<string[]> externalEvidence = new("--external-evidence")
        {
            AllowMultipleArgumentsPerToken = true,
        };
        Option<string> evidenceRepository = new("--evidence-repository");
        Option<string> evidenceRevision = new("--evidence-revision");
        Option<string> evidenceScope = new("--evidence-scope");
        options.AddTo(command);
        command.Options.Add(executionContext);
        command.Options.Add(externalEvidence);
        command.Options.Add(evidenceRepository);
        command.Options.Add(evidenceRevision);
        command.Options.Add(evidenceScope);
        command.SetAction(result =>
        {
            IReadOnlyList<SarifEvidenceArtifactReference> artifacts =
                Array.Empty<SarifEvidenceArtifactReference>();
            string? parseError = null;
            try
            {
                artifacts = ExternalEvidenceCommandSupport.ParseBindings(result.GetValue(externalEvidence));
            }
            catch (InvalidOperationException exception)
            {
                parseError = exception.Message;
            }

            SarifEvidenceAssessmentContext? assessmentContext =
                ExternalEvidenceCommandSupport.ResolveAssessmentContext(
                    result.GetValue(evidenceRepository),
                    result.GetValue(evidenceRevision),
                    result.GetValue(evidenceScope));
            return handler.Execute(
                options.Read(result),
                result.GetValue(executionContext),
                artifacts,
                assessmentContext,
                parseError);
        });
        return command;
    }

    private static Option<string> RequiredOption(string name, string description)
    {
        Option<string> option = new(name)
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = description,
        };
        return option;
    }
}
