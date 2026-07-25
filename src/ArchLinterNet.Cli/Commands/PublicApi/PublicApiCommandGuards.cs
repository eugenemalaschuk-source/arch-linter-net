using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;

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
        out int exitCode)
    {
        exitCode = CliExitCodes.InvalidArgumentsOrRuntimeError;

        if (string.IsNullOrWhiteSpace(contractId))
        {
            console.Error.WriteLine($"--contract is required for public-api {commandName}.");
            return false;
        }

        if (!PublicApiOptionsFactory.SupportedFormats.Contains(format, StringComparer.Ordinal))
        {
            console.Error.WriteLine(
                $"Invalid format: {format}. Use {string.Join(", ", PublicApiOptionsFactory.SupportedFormats)}.");
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
