using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Split out of ValidateCommandHandler.cs (which grew past the file-size lint threshold once
// issue #374's --profile wiring landed) — the single-mode/combined-mode execution paths
// themselves, kept alongside the mode-parsing helper they both depend on. Error-content builders
// (WriteExecutionError et al.) remain in the main file.
internal sealed partial class ValidateCommandHandler
{
    private static bool TryParseModes(string rawMode, out IReadOnlyList<string> modes, out string? error)
    {
        List<string> parsed = rawMode.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parsed.Count == 0 || parsed.Any(mode => mode is not ("strict" or "audit")))
        {
            modes = Array.Empty<string>();
            error = $"Invalid mode: {rawMode}. Use 'strict', 'audit', or a comma-separated combination of both.";
            return false;
        }

        modes = parsed;
        error = null;
        return true;
    }

    private int ExecuteValidation(
        ValidateCommandOptions options, string errorFormat, ValidationProfileExecutionState profileState)
    {
        if (!TryParseModes(options.Mode, out IReadOnlyList<string> modes, out string? modeError))
        {
            _console.Error.WriteLine(modeError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return modes.Count == 1
            ? ExecuteSingleMode(options, modes[0], errorFormat, profileState)
            : ExecuteCombinedModes(options, modes, errorFormat, profileState);
    }

    private int ExecuteSingleMode(
        ValidateCommandOptions options, string mode, string errorFormat, ValidationProfileExecutionState profileState)
    {
        // A profile always needs a real ValidationTiming instance to derive contract-family
        // counts (see AnalysisProfileBuilder) even when --timings' human report was not
        // requested — WriteReport below stays gated on TimingsEnabled specifically, so omitting
        // --timings while using --profile still prints nothing extra to stderr.
        ValidationTiming? timing = options.TimingsEnabled || options.ProfileDestination is not null
            ? new ValidationTiming()
            : null;
        profileState.Timing = timing;
        ValidationRequest request = BuildValidationRequest(options, mode);

        (ValidationOutcome nativeOutcome, ArchitectureAnalysisSnapshotCounters counters) =
            _runtime.ValidateWithCounters(request, timing);
        profileState.Counters = counters;
        profileState.InputPaths = CreateProfileInputPaths(nativeOutcome.PolicyImportPaths
            .Concat(nativeOutcome.ResolvedAssemblyPaths
                .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
            .Concat(nativeOutcome.DiscoveredProjectPaths));

        string? importCollision = FindImportFileCollision(options, nativeOutcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileImportCollision = FindProfileFileCollision(
            options, nativeOutcome.PolicyImportPaths, "imported policy file");
        if (profileImportCollision is not null)
        {
            _console.Error.WriteLine(profileImportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? receiptCollision = FindReceiptFileCollision(options, nativeOutcome.ResolvedAssemblyPaths);
        if (receiptCollision is not null)
        {
            _console.Error.WriteLine(receiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileReceiptCollision = FindProfileFileCollision(
            options, nativeOutcome.ResolvedAssemblyPaths.SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }),
            "a build artifact or receipt loaded during this run");
        if (profileReceiptCollision is not null)
        {
            _console.Error.WriteLine(profileReceiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? projectCollision = FindDiscoveredProjectFileCollision(options, nativeOutcome.DiscoveredProjectPaths);
        if (projectCollision is not null)
        {
            _console.Error.WriteLine(projectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileProjectCollision = FindProfileFileCollision(
            options, nativeOutcome.DiscoveredProjectPaths, "a project file loaded during this run");
        if (profileProjectCollision is not null)
        {
            _console.Error.WriteLine(profileProjectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        // Every --external-evidence artifact is a trust-read input the same way an imported policy
        // file or a discovered project is — a --report/--profile destination must never be allowed
        // to overwrite the SARIF this invocation just read as trusted evidence. Checked before
        // AttachExternalEvidence below (which performs the actual read) so an invalid invocation
        // never reaches output routing at all.
        string? evidenceReportCollision = FindExternalEvidenceReportCollision(options, nativeOutcome.RepositoryRoot);
        if (evidenceReportCollision is not null)
        {
            _console.Error.WriteLine(evidenceReportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? evidenceProfileCollision = FindProfileFileCollision(
            options,
            ResolveExternalEvidencePaths(options, nativeOutcome.RepositoryRoot),
            "an --external-evidence artifact path");
        if (evidenceProfileCollision is not null)
        {
            _console.Error.WriteLine(evidenceProfileCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        // External evidence is bound onto the outcome AFTER the outcome used for cache population
        // is captured (nativeOutcome, below) and NEVER before — see design.md in
        // openspec/changes/cli-external-evidence-binding. This guarantees SARIF bytes are always
        // freshly read regardless of --cache, and that a cache hit's reconstructed applicability
        // records never already contain a previous run's external-evidence entries.
        ValidationOutcome outcome = ValidationExecutionSemantics.AttachExternalEvidence(
            options, nativeOutcome, mode, _cancellationToken);

        RouteResult result = _coordinator.RouteSingleOutcome(
            options.Format, mode, outcome, options.AdditionalSinks, timing, _cancellationToken);
        profileState.Output = CreateOutputProfile(result);
        profileState.RenderedSinkCount = result.RenderedFormats.Count;
        if (options.TimingsEnabled)
        {
            timing?.WriteReport(_console.Error);
        }

        // A completed, non-cancelled run is eligible to populate the cache regardless of
        // Passed/Violations — see openspec/specs/analysis-cache/spec.md; population itself
        // still gates on every discovered project being #406 VerifiedCacheEligible. Populates from
        // nativeOutcome (never the external-evidence-enriched outcome) — see the comment above.
        if (!result.Cancelled)
        {
            TryPopulateCache(options, nativeOutcome, counters, profileState.Cache);
        }

        WriteProfile(
            options,
            profileState,
            ResolveCompletionStatus(outcome, result.Cancelled),
            result.Cancelled);

        if (result.Cancelled)
        {
            WriteCancelledRouting(options, errorFormat, result, isSingleMode: true, new[] { (mode, outcome) });
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options, errorFormat, result, isSingleMode: true, new[] { (mode, outcome) });
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return ResolveValidationExitCode(outcome);
    }

    // One ArchitectureAnalysisSnapshot serves every requested mode: policy composition, project
    // discovery, and assembly loading happen once (inside _runtime.CreateSnapshot), and each
    // requested mode is evaluated against that same snapshot — see issue #363 /
    // openspec/specs/analysis-snapshot/spec.md.
    private int ExecuteCombinedModes(
        ValidateCommandOptions options,
        IReadOnlyList<string> modes,
        string errorFormat,
        ValidationProfileExecutionState profileState)
    {
        // See the identical comment in ExecuteSingleMode: a profile needs a real ValidationTiming
        // instance regardless of whether --timings' human report was also requested.
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
            CacheLocation = ResolveCacheLocationForExecution(options),
            MaxParallelism = options.MaxParallelism,
            WaiverEvaluationDate = ValidationExecutionSemantics.GetWaiverEvaluationDate(options.WaiverEvaluationDate),
            CancellationToken = _cancellationToken,
        };

        using ArchitectureAnalysisSnapshot snapshot = _runtime.CreateSnapshot(snapshotRequest, timing);
        profileState.Counters = snapshot.Counters;
        profileState.InputPaths = CreateProfileInputPaths(snapshot.GetProfileInputPaths());

        List<(string Mode, ValidationOutcome Outcome)> outcomesByMode = new();
        try
        {
            foreach (string mode in modes)
            {
                ValidationOutcome outcome = snapshot.Evaluate(mode, timing);
                outcomesByMode.Add((mode, outcome));
            }
        }
        finally
        {
            // Source/fact-index counters can advance inside Evaluate. Retain the final snapshot
            // view even when cooperative cancellation prevents this method from returning.
            profileState.Counters = snapshot.Counters;
        }

        // All modes share the same policy document and build-state snapshot; check imports and
        // receipts from the first outcome.
        string? importCollision = FindImportFileCollision(options, outcomesByMode[0].Outcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileImportCollision = FindProfileFileCollision(
            options, outcomesByMode[0].Outcome.PolicyImportPaths, "imported policy file");
        if (profileImportCollision is not null)
        {
            _console.Error.WriteLine(profileImportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? receiptCollision = FindReceiptFileCollision(options, outcomesByMode[0].Outcome.ResolvedAssemblyPaths);
        if (receiptCollision is not null)
        {
            _console.Error.WriteLine(receiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileReceiptCollision = FindProfileFileCollision(
            options, outcomesByMode[0].Outcome.ResolvedAssemblyPaths.SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }),
            "a build artifact or receipt loaded during this run");
        if (profileReceiptCollision is not null)
        {
            _console.Error.WriteLine(profileReceiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? projectCollision = FindDiscoveredProjectFileCollision(
            options, outcomesByMode[0].Outcome.DiscoveredProjectPaths);
        if (projectCollision is not null)
        {
            _console.Error.WriteLine(projectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileProjectCollision = FindProfileFileCollision(
            options, outcomesByMode[0].Outcome.DiscoveredProjectPaths, "a project file loaded during this run");
        if (profileProjectCollision is not null)
        {
            _console.Error.WriteLine(profileProjectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        // See the identical comment in ExecuteSingleMode: every --external-evidence artifact is a
        // trust-read input that a --report/--profile destination must never be allowed to overwrite.
        string? evidenceReportCollision = FindExternalEvidenceReportCollision(
            options, outcomesByMode[0].Outcome.RepositoryRoot);
        if (evidenceReportCollision is not null)
        {
            _console.Error.WriteLine(evidenceReportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? evidenceProfileCollision = FindProfileFileCollision(
            options,
            ResolveExternalEvidencePaths(options, outcomesByMode[0].Outcome.RepositoryRoot),
            "an --external-evidence artifact path");
        if (evidenceProfileCollision is not null)
        {
            _console.Error.WriteLine(evidenceProfileCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        // Bound once per invocation (not per mode) and attached to each mode's own outcome — see
        // the identical comment in ExecuteSingleMode. outcomesByMode (native) remains what
        // TryPopulateCache uses below; enrichedOutcomesByMode is used for everything user-facing.
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> enrichedOutcomesByMode =
            AttachExternalEvidenceToAll(options, outcomesByMode);
        bool allPassed = enrichedOutcomesByMode.All(pair => pair.Outcome.Passed);

        RouteResult result = _coordinator.RouteCombinedOutcomes(
            options.Format, enrichedOutcomesByMode, options.AdditionalSinks, timing, _cancellationToken);
        profileState.Output = CreateOutputProfile(result);
        profileState.RenderedSinkCount = result.RenderedFormats.Count;

        if (options.TimingsEnabled)
        {
            timing?.WriteReport(_console.Error);
        }

        // One cache entry per requested mode — see finding #4: a combined "strict,audit" request
        // must never collapse more than one mode's outcome under a single "strict,audit"-shaped
        // key. Each mode's own outcome (and this snapshot's shared discovery/eligibility state) is
        // populated independently. Populates from the native outcomesByMode (never the
        // external-evidence-enriched list) — see the comment in ExecuteSingleMode.
        if (!result.Cancelled)
        {
            foreach ((_, ValidationOutcome modeOutcome) in outcomesByMode)
            {
                TryPopulateCache(options, modeOutcome, snapshot.Counters, profileState.Cache);
            }
        }

        // A blocked preflight blocks every requested mode identically (see
        // openspec/specs/analysis-snapshot/spec.md, "Invalid build state fails the whole
        // snapshot"), so the first outcome's PreflightBlocked reflects every mode's; allPassed
        // reflects every mode's Passed, unlike the single-mode overload's one outcome.
        WriteProfile(
            options,
            profileState,
            ResolveCompletionStatus(
                enrichedOutcomesByMode[0].Outcome.PreflightBlocked, allPassed, result.Cancelled),
            result.Cancelled);

        if (result.Cancelled)
        {
            WriteCancelledRouting(options, errorFormat, result, isSingleMode: false, enrichedOutcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options, errorFormat, result, isSingleMode: false, enrichedOutcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return ResolveCombinedValidationExitCode(enrichedOutcomesByMode, allPassed);
    }

    // See the identical comment in AttachExternalEvidence: id validation runs unconditionally
    // (including for a PreflightBlocked outcome), and only the SARIF read/attach step is skipped
    // when preflight is blocked.
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

    // Moved out of ValidateCommandHandler.cs (which grew past the file-size lint threshold) —
    // kept alongside AttachExternalEvidence/AttachExternalEvidenceToAll above, the only callers.
    //
    // A repository-local SARIF artifact bound via --external-evidence is trust-read as external
    // evidence; if a --report file sink or --profile destination pointed at that same path, this
    // invocation would overwrite the artifact it just read as trusted with its own tool output —
    // especially dangerous with --cache, whose contract requires every run to re-read evidence
    // fresh from disk. Resolved against RepositoryRoot the same way SarifEvidenceReader resolves a
    // relative artifact path (Path.Combine short-circuits to the artifact path itself when it is
    // already rooted, so an absolute/unsafe artifact path still resolves to a comparable full path
    // here even though the Core trust boundary separately rejects it as unsafe).
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
        // A valid unassessable assessment has precedence over trusted failures in any other
        // requested mode, but only after the shared report has routed successfully.
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
            options, mode, ResolveCacheLocationForExecution(options), _cancellationToken);
    }
}
