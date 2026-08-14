using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate;

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

        (ValidationOutcome outcome, ArchitectureAnalysisSnapshotCounters counters) =
            _runtime.ValidateWithCounters(request, timing);
        profileState.Counters = counters;
        profileState.InputPaths = CreateProfileInputPaths(outcome.PolicyImportPaths
            .Concat(outcome.ResolvedAssemblyPaths
                .SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }))
            .Concat(outcome.DiscoveredProjectPaths));

        string? importCollision = FindImportFileCollision(options, outcome.PolicyImportPaths);
        if (importCollision is not null)
        {
            _console.Error.WriteLine(importCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileImportCollision = FindProfileFileCollision(
            options, outcome.PolicyImportPaths, "imported policy file");
        if (profileImportCollision is not null)
        {
            _console.Error.WriteLine(profileImportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? receiptCollision = FindReceiptFileCollision(options, outcome.ResolvedAssemblyPaths);
        if (receiptCollision is not null)
        {
            _console.Error.WriteLine(receiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileReceiptCollision = FindProfileFileCollision(
            options, outcome.ResolvedAssemblyPaths.SelectMany(path => new[] { path, BuildReceiptStore.ReceiptPathFor(path) }),
            "a build artifact or receipt loaded during this run");
        if (profileReceiptCollision is not null)
        {
            _console.Error.WriteLine(profileReceiptCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? projectCollision = FindDiscoveredProjectFileCollision(options, outcome.DiscoveredProjectPaths);
        if (projectCollision is not null)
        {
            _console.Error.WriteLine(projectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileProjectCollision = FindProfileFileCollision(
            options, outcome.DiscoveredProjectPaths, "a project file loaded during this run");
        if (profileProjectCollision is not null)
        {
            _console.Error.WriteLine(profileProjectCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

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
        // still gates on every discovered project being #406 VerifiedCacheEligible.
        if (!result.Cancelled)
        {
            TryPopulateCache(options, outcome, counters, profileState.Cache);
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

        return outcome.Passed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
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
            CancellationToken = _cancellationToken,
        };

        using ArchitectureAnalysisSnapshot snapshot = _runtime.CreateSnapshot(snapshotRequest, timing);
        profileState.Counters = snapshot.Counters;
        profileState.InputPaths = CreateProfileInputPaths(snapshot.GetProfileInputPaths());

        bool allPassed = true;
        List<(string Mode, ValidationOutcome Outcome)> outcomesByMode = new();
        try
        {
            foreach (string mode in modes)
            {
                ValidationOutcome outcome = snapshot.Evaluate(mode, timing);
                outcomesByMode.Add((mode, outcome));
                allPassed &= outcome.Passed;
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

        RouteResult result = _coordinator.RouteCombinedOutcomes(
            options.Format, outcomesByMode, options.AdditionalSinks, timing, _cancellationToken);
        profileState.Output = CreateOutputProfile(result);
        profileState.RenderedSinkCount = result.RenderedFormats.Count;

        if (options.TimingsEnabled)
        {
            timing?.WriteReport(_console.Error);
        }

        // One cache entry per requested mode — see finding #4: a combined "strict,audit" request
        // must never collapse more than one mode's outcome under a single "strict,audit"-shaped
        // key. Each mode's own outcome (and this snapshot's shared discovery/eligibility state) is
        // populated independently.
        if (!result.Cancelled)
        {
            foreach ((string mode, ValidationOutcome modeOutcome) in outcomesByMode)
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
                outcomesByMode[0].Outcome.PreflightBlocked, allPassed, result.Cancelled),
            result.Cancelled);

        if (result.Cancelled)
        {
            WriteCancelledRouting(options, errorFormat, result, isSingleMode: false, outcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (result.Status != ReportRouteStatus.AllSucceeded)
        {
            WriteOutputError(options, errorFormat, result, isSingleMode: false, outcomesByMode);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return allPassed ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
    }

    private ValidationRequest BuildValidationRequest(ValidateCommandOptions options, string mode)
    {
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
            CacheLocation = ResolveCacheLocationForExecution(options),
            MaxParallelism = options.MaxParallelism,
            CancellationToken = _cancellationToken,
        };
    }
}
