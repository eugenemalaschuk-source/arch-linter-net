using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// The command façade owns the unified invocation lifecycle and top-level exception routing.
// Analysis, cache, profile, preflight, and output details live in purpose-named collaborators;
// this type remains the sole command entry point and outcome authority.
internal sealed class ValidateCommandHandler
{
    // Capture before any invocation collaborators are constructed so --profile keeps measuring
    // allocations made by the complete command invocation, including its composition boundary.
    private readonly long _allocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
    private readonly ValidateCommandPreflight _preflight;
    private readonly ValidateCommandExecution _execution;
    private readonly ValidateProfileWriter _profile;
    private readonly ValidateCommandErrorReporter _errors;

    public ValidateCommandHandler(
        ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        ReportCoordinator coordinator = new(runtime, console, fileSystem);
        ValidateCacheCoordinator cache = new(console, cancellationToken);
        _profile = new ValidateProfileWriter(console, fileSystem, _allocatedBytesAtStart);
        _errors = new ValidateCommandErrorReporter(console, coordinator, cancellationToken);
        _preflight = new ValidateCommandPreflight(runtime, console, fileSystem, cache);
        _execution = new ValidateCommandExecution(
            runtime, console, coordinator, cache, _profile, _errors, cancellationToken);
    }

    public int Execute(ValidateCommandOptions options)
    {
        int? immediateResult = _preflight.TryWriteImmediateResponse(options);
        if (immediateResult is not null)
        {
            return immediateResult.Value;
        }

        string errorFormat = ValidateCommandPreflight.ResolveEffectiveFormat(options);
        ValidateProfileExecutionState profileState = new();

        try
        {
            return _execution.ExecuteValidation(options, errorFormat, profileState);
        }
        // Must precede the general OperationCanceledException catch below (it is a subtype).
        catch (BuildStateProcessCleanupTimedOutException ex)
        {
            _profile.CaptureCancelledProfileState(profileState, ex);
            _profile.WriteCancelledProfile(options, profileState);
            _errors.WriteCancellation(options, errorFormat, ex);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (OperationCanceledException ex)
        {
            _profile.CaptureCancelledProfileState(profileState, ex);
            _profile.WriteCancelledProfile(options, profileState);
            _errors.WriteCancellation(options, errorFormat);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex) when (TryGetPolicyDiagnostic(ex, out ArchitecturePolicyDiagnostic? diagnostic))
        {
            _errors.WritePolicyDiagnostic(options, errorFormat, ex, diagnostic!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception ex)
        {
            _errors.WriteExecutionError(options, errorFormat, ex);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static bool TryGetPolicyDiagnostic(Exception exception, out ArchitecturePolicyDiagnostic? diagnostic)
    {
        diagnostic = exception switch
        {
            ArchitecturePolicyLoadException loadException => loadException.Diagnostic as ArchitecturePolicyDiagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic as ArchitecturePolicyDiagnostic,
            _ => null,
        };
        return diagnostic is not null;
    }

    internal static int ResolveValidationExitCode(ValidationOutcome outcome) =>
        ValidateCommandExecution.ResolveValidationExitCode(outcome);

    internal static int ResolveCombinedValidationExitCode(
        IReadOnlyList<(string Mode, ValidationOutcome Outcome)> outcomesByMode,
        bool allPassed) =>
        ValidateCommandExecution.ResolveCombinedValidationExitCode(outcomesByMode, allPassed);
}
