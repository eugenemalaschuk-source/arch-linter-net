using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands;

/// <summary>Writes owned command failures without making JSON callers parse a human fallback.</summary>
internal static class CliErrorOutputWriter
{
    public const string JsonFormat = "json";

    public static void Write(
        ICliConsole console,
        string format,
        string category,
        string message,
        object? details = null)
    {
        if (format == JsonFormat)
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                schema_version = 1,
                status = "error",
                kind = "command_error",
                error = new
                {
                    category,
                    message,
                    details,
                },
            }));
            return;
        }

        console.Error.WriteLine(message);
    }

    public static void WriteConfigurationViolations(
        ICliConsole console,
        string format,
        string operation,
        IReadOnlyCollection<ArchitectureViolation> violations)
    {
        string message = $"Configuration violations detected — baseline cannot be {operation}:";
        if (format == JsonFormat)
        {
            Write(console, format, "configuration-error", message, new
            {
                violations = violations.Select(violation => new
                {
                    source_type = violation.SourceType,
                    forbidden_namespace = violation.ForbiddenNamespace,
                }),
            });
            return;
        }

        console.Error.WriteLine(message);
        foreach (ArchitectureViolation violation in violations)
        {
            console.Error.WriteLine($"  {violation.SourceType}: {violation.ForbiddenNamespace}");
        }
    }

    public static void WritePreflightFailure(
        ICliConsole console,
        string format,
        string message,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> diagnostics)
    {
        if (format == JsonFormat)
        {
            Write(console, format, "build-state-preflight-failed", message, new
            {
                diagnostics = diagnostics.Where(diagnostic => diagnostic.IsBlocking).Select(diagnostic => new
                {
                    contract_name = diagnostic.ContractName,
                    contract_id = diagnostic.ContractId,
                    state = PreflightStateToken(diagnostic.State),
                    project_path = diagnostic.Evidence.ProjectPath,
                    assembly_name = diagnostic.Evidence.AssemblyName,
                    requested_configuration = diagnostic.Evidence.RequestedConfiguration,
                    observed_configuration = diagnostic.Evidence.ObservedConfiguration,
                    requested_target_framework = diagnostic.Evidence.RequestedTargetFramework,
                    observed_target_framework = diagnostic.Evidence.ObservedTargetFramework,
                    expected_output_path = diagnostic.Evidence.ExpectedOutputPath,
                    searched_paths = diagnostic.Evidence.SearchedPaths,
                    build_command = diagnostic.Evidence.BuildCommand,
                    detail = diagnostic.Evidence.Detail,
                    cache_eligibility = diagnostic.Evidence.CacheEligibility,
                    cache_ineligibility_reasons = diagnostic.Evidence.CacheIneligibilityReasons,
                }),
            });
            return;
        }

        console.Error.WriteLine(message);
        foreach (BuildStatePreflightDiagnostic diagnostic in diagnostics.Where(diagnostic => diagnostic.IsBlocking))
        {
            console.Error.WriteLine($"  {diagnostic.State}: {diagnostic.ContractName}");
        }
    }

    private static string PreflightStateToken(BuildStatePreflightState state) => state switch
    {
        BuildStatePreflightState.Cancelled => "cancelled",
        BuildStatePreflightState.RestoreRequired => "restore-required",
        BuildStatePreflightState.MissingArtifact => "missing-artifact",
        BuildStatePreflightState.WrongConfiguration => "wrong-configuration",
        BuildStatePreflightState.WrongTargetFramework => "wrong-target-framework",
        BuildStatePreflightState.WrongProjectOutput => "wrong-project-output",
        BuildStatePreflightState.InconsistentDependencyArtifact => "inconsistent-dependency-artifact",
        BuildStatePreflightState.StaleArtifact => "stale-artifact",
        BuildStatePreflightState.UnverifiableArtifact => "unverifiable-artifact",
        BuildStatePreflightState.Current => "current",
        BuildStatePreflightState.BuildFailed => "build-failed",
        BuildStatePreflightState.RestoreFailed => "restore-failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
