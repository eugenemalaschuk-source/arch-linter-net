using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiCaptureCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    private const string CommandName = "capture";

    public int Execute(PublicApiCaptureCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(PublicApiHelpTexts.CaptureHelpText);
            return CliExitCodes.Success;
        }

        if (!PublicApiCommandGuards.TryValidateCommon(
                console, fileSystem, options.PolicyPath, options.ContractId, options.Format, CommandName, out int exitCode))
        {
            return exitCode;
        }

        if (options.OutputPath == null)
        {
            console.Error.WriteLine("--output is required for public-api capture.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            PublicApiCaptureOutcome outcome = runtime.CapturePublicApi(new PublicApiCaptureRequest
            {
                PolicyPath = options.PolicyPath,
                ContractId = options.ContractId!,
                ConditionSetName = options.ConditionSetName,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(console, CommandName, outcome.Error!, outcome.PreflightDiagnostics);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            // A snapshot is a reviewed artifact, so capture never quietly replaces one that differs:
            // that would turn an unreviewed surface change into a silently updated baseline.
            bool exists = fileSystem.FileExists(options.OutputPath);
            bool identical = exists && string.Equals(
                fileSystem.ReadAllText(options.OutputPath), outcome.Snapshot, StringComparison.Ordinal);

            if (exists && !identical && !options.Force)
            {
                console.Error.WriteLine(
                    $"Public API snapshot '{options.OutputPath}' already exists and differs from the captured " +
                    "surface. Re-run with --force to replace it, or use 'arch-linter-net public-api update " +
                    "--dry-run' to review the change first.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (!identical)
            {
                Write(options.OutputPath, outcome.Snapshot!);
            }

            console.Out.WriteLine(options.Format == "json"
                ? JsonSerializer.Serialize(new
                {
                    contractId = options.ContractId,
                    output = options.OutputPath,
                    entryCount = outcome.EntryCount,
                    alreadyCurrent = identical,
                })
                : identical
                    ? $"Public API snapshot is already current ({outcome.EntryCount} entries): {options.OutputPath}"
                    : $"Captured {outcome.EntryCount} public API entries.{Environment.NewLine}Output: {options.OutputPath}");

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api capture error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private void Write(string path, string content)
    {
        string tempPath = fileSystem.WriteAllTextToTemp(path, content);
        fileSystem.RenameTempToTarget(tempPath, path);
    }
}
