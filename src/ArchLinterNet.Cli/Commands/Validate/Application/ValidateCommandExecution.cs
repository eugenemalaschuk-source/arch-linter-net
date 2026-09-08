using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Owns mode dispatch and the single/combined validation pipelines. The runtime creates the one
// analysis snapshot; this collaborator only projects outcomes, routes them, and delegates cache
// and profile side effects to their invocation collaborators.
internal sealed class ValidateCommandExecution
{
    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly ReportCoordinator _coordinator;
    private readonly ValidateCacheCoordinator _cache;
    private readonly ValidateProfileWriter _profile;
    private readonly ValidateCommandErrorReporter _errors;
    private readonly CancellationToken _cancellationToken;

    public ValidateCommandExecution(
        ICliRuntime runtime,
        ICliConsole console,
        ReportCoordinator coordinator,
        ValidateCacheCoordinator cache,
        ValidateProfileWriter profile,
        ValidateCommandErrorReporter errors,
        CancellationToken cancellationToken)
    {
        _runtime = runtime;
        _console = console;
        _coordinator = coordinator;
        _cache = cache;
        _profile = profile;
        _errors = errors;
        _cancellationToken = cancellationToken;
    }

    internal int ExecuteValidation(
        ValidateCommandOptions options, string errorFormat, ValidateProfileExecutionState profileState)
    {
        if (!ValidateCommandPreflight.TryParseModes(options.Mode, out IReadOnlyList<string> modes, out string? modeError))
        {
            return WriteCollision(modeError!);
        }

        return modes.Count == 1
            ? ExecuteSingleMode(options, modes[0], errorFormat, profileState)
            : ExecuteCombinedModes(options, modes, errorFormat, profileState);
    }

    private int ExecuteSingleMode(
        ValidateCommandOptions options,
        string mode,
        string errorFormat,
        ValidateProfileExecutionState profileState)
    {
        ValidationTiming? timing = options.TimingsEnabled || options.ProfileDestination is not null
            ? new ValidationTiming()
            : null;
        profileState.Timing = timing;
        ValidationRequest request = BuildValidationRequest(options, mode);

        (ValidationOutcome nativeOutcome, ArchitectureAnalysisSnapshotCounters counters) =
            _runtime.ValidateWithCounters(request, timing);
        profileState.Counters = counters;
        profileState.InputPaths = ValidateProfileWriter.CreateProfileInputPaths(nativeOutcome.PolicyImportPaths
            .Concat(nativeOutcome.ResolvedAssemblyPaths
                .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
            .Concat(nativeOutcome.DiscoveredProjectPaths));

        if (ValidateCommandPreflight.FindImportFileCollision(options, nativeOutcome.PolicyImportPaths) is { } importCollision)
        {
            return WriteCollision(importCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, nativeOutcome.PolicyImportPaths, "imported policy file") is { } profileImportCollision)
        {
            return WriteCollision(profileImportCollision);
        }

        if (ValidateCommandPreflight.FindReceiptFileCollision(options, nativeOutcome.ResolvedAssemblyPaths) is { } receiptCollision)
        {
            return WriteCollision(receiptCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options,
                nativeOutcome.ResolvedAssemblyPaths.SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }),
                "a build artifact or receipt loaded during this run") is { } profileReceiptCollision)
        {
            return WriteCollision(profileReceiptCollision);
        }

        if (ValidateCommandPreflight.FindDiscoveredProjectFileCollision(options, nativeOutcome.DiscoveredProjectPaths) is { } projectCollision)
        {
            return WriteCollision(projectCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, nativeOutcome.DiscoveredProjectPaths, "a project file loaded during this run") is { } profileProjectCollision)
        {
            return WriteCollision(profileProjectCollision);
        }

        if (FindExternalEvidenceReportCollision(options, nativeOutcome.RepositoryRoot) is { } evidenceReportCollision)
        {
            return WriteCollision(evidenceReportCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, ResolveExternalEvidencePaths(options, nativeOutcome.RepositoryRoot),
                "an --external-evidence artifact path") is { } evidenceProfileCollision)
        {
            return WriteCollision(evidenceProfileCollision);
        }

        ValidationOutcome outcome = ValidationExecutionSemantics.AttachExternalEvidence(
            options, nativeOutcome, mode, _cancellationToken);
        RouteResult result = _coordinator.RouteSingleOutcome(
            options.Format, mode, outcome, options.AdditionalSinks, timing, _cancellationToken);
        profileState.Output = ValidateProfileWriter.CreateOutputProfile(result);
        profileState.RenderedSinkCount = result.RenderedFormats.Count;
        if (options.TimingsEnabled)
        {
            if (timing is not null)
            {
                _errors.WriteTimingReport(timing);
            }
        }

        if (!result.Cancelled)
        {
            _cache.TryPopulateCache(options, nativeOutcome, counters, profileState.Cache);
        }

        _profile.WriteProfile(
            options,
            profileState,
            ValidateProfileWriter.ResolveCompletionStatus(outcome, result.Cancelled),
            result.Cancelled);

        if (result.Cancelled)
        {
            _errors.WriteCancelledRouting(options, errorFormat, result, isSingleMode: true, new[] { (mode, outcome) });
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            _errors.WriteOutputError(options, errorFormat, result, isSingleMode: true, new[] { (mode, outcome) });
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return ResolveValidationExitCode(outcome);
    }

    private int ExecuteCombinedModes(
        ValidateCommandOptions options,
        IReadOnlyList<string> modes,
        string errorFormat,
        ValidateProfileExecutionState profileState)
    {
        ValidationTiming? timing = options.TimingsEnabled || options.ProfileDestination is not null
            ? new ValidationTiming()
            : null;
        profileState.Timing = timing;
        AnalysisSnapshotRequest snapshotRequest = new()
        {
            PolicyPath = options.PolicyPath,
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
            CacheLocation = ValidateCacheCoordinator.ResolveCacheLocationForExecution(options),
            MaxParallelism = options.MaxParallelism,
            WaiverEvaluationDate = ValidationExecutionSemantics.GetWaiverEvaluationDate(options.WaiverEvaluationDate),
            CancellationToken = _cancellationToken,
        };

        using ArchitectureAnalysisSnapshot snapshot = _runtime.CreateSnapshot(snapshotRequest, timing);
        profileState.Counters = snapshot.Counters;
        profileState.InputPaths = ValidateProfileWriter.CreateProfileInputPaths(snapshot.GetProfileInputPaths());

        List<(string Mode, ValidationOutcome Outcome)> outcomesByMode = new();
        try
        {
            foreach (string mode in modes)
            {
                outcomesByMode.Add((mode, snapshot.Evaluate(mode, timing)));
            }
        }
        finally
        {
            profileState.Counters = snapshot.Counters;
        }

        ValidationOutcome firstOutcome = outcomesByMode[0].Outcome;
        if (ValidateCommandPreflight.FindImportFileCollision(options, firstOutcome.PolicyImportPaths) is { } importCollision)
        {
            return WriteCollision(importCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, firstOutcome.PolicyImportPaths, "imported policy file") is { } profileImportCollision)
        {
            return WriteCollision(profileImportCollision);
        }

        if (ValidateCommandPreflight.FindReceiptFileCollision(options, firstOutcome.ResolvedAssemblyPaths) is { } receiptCollision)
        {
            return WriteCollision(receiptCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options,
                firstOutcome.ResolvedAssemblyPaths.SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }),
                "a build artifact or receipt loaded during this run") is { } profileReceiptCollision)
        {
            return WriteCollision(profileReceiptCollision);
        }

        if (ValidateCommandPreflight.FindDiscoveredProjectFileCollision(options, firstOutcome.DiscoveredProjectPaths) is { } projectCollision)
        {
            return WriteCollision(projectCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, firstOutcome.DiscoveredProjectPaths, "a project file loaded during this run") is { } profileProjectCollision)
        {
            return WriteCollision(profileProjectCollision);
        }

        if (FindExternalEvidenceReportCollision(options, firstOutcome.RepositoryRoot) is { } evidenceReportCollision)
        {
            return WriteCollision(evidenceReportCollision);
        }

        if (ValidateCommandPreflight.FindProfileFileCollision(
                options, ResolveExternalEvidencePaths(options, firstOutcome.RepositoryRoot),
                "an --external-evidence artifact path") is { } evidenceProfileCollision)
        {
            return WriteCollision(evidenceProfileCollision);
        }

        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> enrichedOutcomesByMode =
            AttachExternalEvidenceToAll(options, outcomesByMode);
        bool allPassed = enrichedOutcomesByMode.All(pair => pair.Outcome.Passed);
        RouteResult result = _coordinator.RouteCombinedOutcomes(
            options.Format, enrichedOutcomesByMode, options.AdditionalSinks, timing, _cancellationToken);
        profileState.Output = ValidateProfileWriter.CreateOutputProfile(result);
        profileState.RenderedSinkCount = result.RenderedFormats.Count;
        if (options.TimingsEnabled)
        {
            if (timing is not null)
            {
                _errors.WriteTimingReport(timing);
            }
        }

        if (!result.Cancelled)
        {
            foreach ((_, ValidationOutcome modeOutcome) in outcomesByMode)
            {
                _cache.TryPopulateCache(options, modeOutcome, snapshot.Counters, profileState.Cache);
            }
        }

        _profile.WriteProfile(
            options,
            profileState,
            ValidateProfileWriter.ResolveCompletionStatus(
                enrichedOutcomesByMode[0].Outcome.PreflightBlocked, allPassed, result.Cancelled),
            result.Cancelled);

        if (result.Cancelled)
        {
            _errors.WriteCancelledRouting(options, errorFormat, result, isSingleMode: false, enrichedOutcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            _errors.WriteOutputError(options, errorFormat, result, isSingleMode: false, enrichedOutcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return ResolveCombinedValidationExitCode(enrichedOutcomesByMode, allPassed);
    }

    private IReadOnlyList<(string Mode, ValidationOutcome Outcome)> AttachExternalEvidenceToAll(
        ValidateCommandOptions options, IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode)
    {
        if (outcomesByMode.Count == 0)
        {
            return outcomesByMode;
        }

        ValidationOutcome first = outcomesByMode[0].Outcome;
        ArchitectureExternalEvidenceBinder.ValidateBindingIds(
            first.ExternalEvidenceRequirements, options.ExternalEvidenceArtifacts);
        if (first.PreflightBlocked)
        {
            return outcomesByMode;
        }

        ArchitectureExternalEvidenceBindingResult binding = ArchitectureExternalEvidenceBinder.Evaluate(
            first.ExternalEvidenceRequirements,
            first.RepositoryRoot,
            options.ExternalEvidenceArtifacts,
            options.ExternalEvidenceAssessmentContext,
            _cancellationToken);
        return outcomesByMode
            .Select(pair => (pair.Mode, ArchitectureExternalEvidenceBinder.Attach(pair.Outcome, binding, pair.Mode)))
            .ToList();
    }

    private static IReadOnlyList<string> ResolveExternalEvidencePaths(
        ValidateCommandOptions options, string repositoryRoot)
    {
        return ValidationExecutionSemantics.ResolveExternalEvidencePaths(options, repositoryRoot);
    }

    private static string? FindExternalEvidenceReportCollision(
        ValidateCommandOptions options, string repositoryRoot)
    {
        if (options.ExternalEvidenceArtifacts.Count == 0)
        {
            return null;
        }

        HashSet<string> evidencePaths = new(
            ResolveExternalEvidencePaths(options, repositoryRoot), StringComparer.OrdinalIgnoreCase);
        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (evidencePaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches an --external-evidence artifact path";
            }
        }

        return null;
    }

    internal static int ResolveValidationExitCode(ValidationOutcome outcome)
    {
        return outcome.AssessmentCompletionEvidence?.State switch
        {
            ArchitectureAssessmentCompletionState.Pass => outcome.Passed
                ? CliExitCodes.Success
                : CliExitCodes.ValidationFailure,
            ArchitectureAssessmentCompletionState.Fail => CliExitCodes.ValidationFailure,
            ArchitectureAssessmentCompletionState.Unassessable => CliExitCodes.InvalidArgumentsOrRuntimeError,
            _ => outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure,
        };
    }

    internal static int ResolveCombinedValidationExitCode(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        bool allPassed)
    {
        if (outcomesByMode.Any(pair =>
                pair.Outcome.AssessmentCompletionEvidence?.State == ArchitectureAssessmentCompletionState.Unassessable))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (outcomesByMode.Any(pair =>
                pair.Outcome.AssessmentCompletionEvidence?.State == ArchitectureAssessmentCompletionState.Fail))
        {
            return CliExitCodes.ValidationFailure;
        }

        return allPassed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    private ValidationRequest BuildValidationRequest(ValidateCommandOptions options, string mode)
    {
        return ValidationExecutionSemantics.CreateRequest(
            options,
            mode,
            ValidateCacheCoordinator.ResolveCacheLocationForExecution(options),
            _cancellationToken);
    }

    private int WriteCollision(string message)
    {
        _console.Error.WriteLine(message);
        return CliExitCodes.InvalidArgumentsOrRuntimeError;
    }
}
