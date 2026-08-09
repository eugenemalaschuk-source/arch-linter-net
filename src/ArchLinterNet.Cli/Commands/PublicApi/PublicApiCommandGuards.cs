using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

// Shared precondition checks for the four public-api subcommands. Every subcommand needs the same
// policy/contract/format validation before it reaches the Core seam, and a preflight-blocked
// outcome has to read the same way regardless of which subcommand hit it.
internal static class PublicApiCommandGuards
{
    // The five values describing "which invocation is being validated" travel together, so they are
    // one parameter object rather than five positional arguments at every call site.
    internal readonly record struct Invocation(
        string PolicyPath,
        string? ContractId,
        string Format,
        string CommandName,
        IReadOnlyList<string> SupportedFormats);

    public static bool TryValidateCommon(
        ICliConsole console,
        IFileSystem fileSystem,
        Invocation invocation,
        out int exitCode)
    {
        (string policyPath, string? contractId, string format, string commandName, IReadOnlyList<string> supportedFormats) =
            invocation;

        exitCode = CliExitCodes.InvalidArgumentsOrRuntimeError;

        if (string.IsNullOrWhiteSpace(contractId))
        {
            CliErrorOutputWriter.Write(
                console, format, "invalid-arguments", $"--contract is required for public-api {commandName}.");
            return false;
        }

        if (!supportedFormats.Contains(format, StringComparer.Ordinal))
        {
            CliErrorOutputWriter.Write(
                console, format, "invalid-format",
                $"Invalid format for public-api {commandName}: {format}. Use {string.Join(", ", supportedFormats)}.");
            return false;
        }

        if (!fileSystem.FileExists(policyPath))
        {
            CliErrorOutputWriter.Write(console, format, "configuration-error", $"Policy file not found: {policyPath}");
            return false;
        }

        exitCode = CliExitCodes.Success;
        return true;
    }

    // The 0.5.1 contract reserves exit code 1 for a completed gate that found drift and 2 for any
    // operation that did not complete (invalid input, unusable snapshot, blocked preflight).
    public static int ExitCodeFor(PublicApiFailureKind failureKind)
    {
        return failureKind == PublicApiFailureKind.Drift
            ? CliExitCodes.ValidationFailure
            : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    public static void WriteError(
        ICliConsole console,
        string format,
        string commandName,
        string error,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        PublicApiFailureKind failureKind,
        IReadOnlyCollection<string>? staleDeclarations = null,
        IReadOnlyCollection<string>? undeclaredSurface = null)
    {
        string message = $"public-api {commandName} error: {error}";
        if (preflightDiagnostics.Any(diagnostic => diagnostic.IsBlocking))
        {
            CliErrorOutputWriter.WritePreflightFailure(console, format, message, preflightDiagnostics);
            return;
        }

        string category = failureKind switch
        {
            PublicApiFailureKind.Drift => "public-api-drift",
            PublicApiFailureKind.InvalidInput => "public-api-invalid-input",
            _ => "configuration-error",
        };
        CliErrorOutputWriter.Write(console, format, category, message, new
        {
            failure_kind = FailureKindToken(failureKind),
            stale_declarations = staleDeclarations ?? Array.Empty<string>(),
            undeclared_surface = undeclaredSurface ?? Array.Empty<string>(),
        });
    }

    private static string FailureKindToken(PublicApiFailureKind failureKind) => failureKind switch
    {
        PublicApiFailureKind.Drift => "drift",
        PublicApiFailureKind.InvalidInput => "invalid-input",
        _ => "none",
    };
}
