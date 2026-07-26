using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

// Shared precondition checks for the four public-api subcommands. Every subcommand needs the same
// policy/contract/format validation before it reaches the Core seam, and a preflight-blocked
// outcome has to read the same way regardless of which subcommand hit it.
internal static class PublicApiCommandGuards
{
    public static bool TryValidateCommon(
        ICliConsole console,
        IFileSystem fileSystem,
        string policyPath,
        string? contractId,
        string format,
        string commandName,
        IReadOnlyList<string> supportedFormats,
        out int exitCode)
    {
        exitCode = CliExitCodes.InvalidArgumentsOrRuntimeError;

        if (string.IsNullOrWhiteSpace(contractId))
        {
            console.Error.WriteLine($"--contract is required for public-api {commandName}.");
            return false;
        }

        if (!supportedFormats.Contains(format, StringComparer.Ordinal))
        {
            console.Error.WriteLine(
                $"Invalid format for public-api {commandName}: {format}. Use {string.Join(", ", supportedFormats)}.");
            return false;
        }

        if (!fileSystem.FileExists(policyPath))
        {
            console.Error.WriteLine($"Policy file not found: {policyPath}");
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
        string commandName,
        string error,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics)
    {
        console.Error.WriteLine($"public-api {commandName} error: {error}");
        foreach (BuildStatePreflightDiagnostic diagnostic in preflightDiagnostics.Where(d => d.IsBlocking))
        {
            console.Error.WriteLine($"  {diagnostic.State}: {diagnostic.ContractName}");
        }
    }
}
