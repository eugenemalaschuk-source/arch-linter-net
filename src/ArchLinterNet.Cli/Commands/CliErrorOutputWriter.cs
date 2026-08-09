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
                    state = diagnostic.State.ToString(),
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
}
