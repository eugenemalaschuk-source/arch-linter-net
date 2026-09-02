using System.CommandLine;
using ArchLinterNet.Cli;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthCommandDefinition(HealthCommandHandler handler)
{
    public Command Create()
    {
        Command command = new("health", "Project the canonical architecture-health/v1 summary.");
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
}
