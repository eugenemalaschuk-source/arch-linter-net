using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Abstractions;

// The ordinary validate command and topology review commands must hand the Core the same
// validation inputs and bind external evidence at the same point in the lifecycle. This neutral
// CLI abstraction prevents topology review from becoming a partial copy of validation orchestration
// or creating a command-to-command dependency.
internal interface IValidationExecutionOptions
{
    string PolicyPath { get; }

    string? ConditionSetName { get; }

    string? BaselinePath { get; }

    IReadOnlyList<string> ContractIds { get; }

    bool EnsureBuilt { get; }

    bool NoRestore { get; }

    string? Configuration { get; }

    string? TargetFramework { get; }

    string? Platform { get; }

    string? RuntimeIdentifier { get; }

    int? MaxParallelism { get; }

    string? WaiverEvaluationDate { get; }

    IReadOnlyList<SarifEvidenceArtifactReference> ExternalEvidenceArtifacts { get; }

    SarifEvidenceAssessmentContext? ExternalEvidenceAssessmentContext { get; }
}

internal abstract class ValidationExecutionSemantics
{
    internal static ValidationRequest CreateRequest(
        IValidationExecutionOptions options,
        string mode,
        AnalysisCacheLocation? cacheLocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryGetWaiverEvaluationDate(options.WaiverEvaluationDate, out DateOnly? waiverEvaluationDate, out _))
        {
            throw new InvalidOperationException("The waiver evaluation date was not validated.");
        }

        return new ValidationRequest
        {
            PolicyPath = options.PolicyPath,
            Mode = mode,
            ConditionSetName = options.ConditionSetName,
            ContractIds = options.ContractIds.ToList(),
            BaselinePath = options.BaselinePath,
            EnforceUnmatchedIgnoredViolationsPolicy = true,
            PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
            NoRestore = options.NoRestore,
            RequestedConfiguration = options.Configuration,
            RequestedTargetFramework = options.TargetFramework,
            RequestedPlatform = options.Platform,
            RequestedRuntimeIdentifier = options.RuntimeIdentifier,
            CacheLocation = cacheLocation,
            MaxParallelism = options.MaxParallelism,
            WaiverEvaluationDate = waiverEvaluationDate,
            CancellationToken = cancellationToken,
        };
    }

    internal static bool TryGetWaiverEvaluationDate(
        string? rawValue,
        out DateOnly? evaluationDate,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            evaluationDate = null;
            error = null;
            return true;
        }

        if (DateOnly.TryParseExact(
                rawValue,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateOnly parsed))
        {
            evaluationDate = parsed;
            error = null;
            return true;
        }

        evaluationDate = null;
        error = "Invalid --waiver-evaluation-date value. Use an ISO calendar date in yyyy-MM-dd format.";
        return false;
    }

    internal static DateOnly? GetWaiverEvaluationDate(string? rawValue)
    {
        return TryGetWaiverEvaluationDate(rawValue, out DateOnly? evaluationDate, out _)
            ? evaluationDate
            : throw new InvalidOperationException("The waiver evaluation date was not validated.");
    }

    internal static ValidationOutcome AttachExternalEvidence(
        IValidationExecutionOptions options,
        ValidationOutcome outcome,
        string mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(outcome);
        ArchitectureExternalEvidenceBinder.ValidateBindingIds(
            outcome.ExternalEvidenceRequirements, options.ExternalEvidenceArtifacts);

        if (outcome.PreflightBlocked)
        {
            return outcome;
        }

        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            outcome.ExternalEvidenceRequirements,
            outcome.RepositoryRoot,
            options.ExternalEvidenceArtifacts,
            options.ExternalEvidenceAssessmentContext,
            cancellationToken);
        return ArchitectureExternalEvidenceBinder.Attach(outcome, binding, mode);
    }

    internal static IReadOnlyList<string> ResolveExternalEvidencePaths(
        IValidationExecutionOptions options,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ExternalEvidenceArtifacts
            .Select(artifact => Path.GetFullPath(Path.Combine(repositoryRoot, artifact.Path)))
            .ToArray();
    }
}
