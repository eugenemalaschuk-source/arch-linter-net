using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiCaptureCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
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
                console,
                fileSystem,
                new PublicApiCommandGuards.Invocation(
                    options.PolicyPath, options.ContractId, options.Format, CommandName, PublicApiOptionsFactory.OperationFormats),
                out int exitCode))
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
                OutputPath = options.OutputPath,
                ConditionSetName = options.ConditionSetName,
                CancellationToken = cancellationToken,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(console, CommandName, outcome.Error!, outcome.PreflightDiagnostics);
                return PublicApiCommandGuards.ExitCodeFor(outcome.FailureKind);
            }

            // Every probe and write targets the destination Core resolved against the policy
            // boundary, never the raw user string: that is what stops an absolute path, a `../`
            // escape, or the policy file itself from being read or replaced.
            string destination = outcome.ResolvedOutputPath!;

            // A snapshot is a reviewed artifact, so capture never quietly replaces one that differs:
            // that would turn an unreviewed surface change into a silently updated baseline.
            bool exists = fileSystem.FileExists(destination);
            bool identical = exists && string.Equals(
                fileSystem.ReadAllText(destination), outcome.Snapshot, StringComparison.Ordinal);

            if (exists && !identical && !options.Force)
            {
                console.Error.WriteLine(
                    $"Public API snapshot '{destination}' already exists and differs from the captured " +
                    "surface. Re-run with --force to replace it, or use 'arch-linter-net public-api update " +
                    "--dry-run' to review the change first.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            // Re-checked immediately before publication, whether that means writing a new
            // snapshot or just reporting the existing one as already current.
            cancellationToken.ThrowIfCancellationRequested();

            if (!identical)
            {
                string tempPath = fileSystem.WriteAllTextToTemp(destination, outcome.Snapshot!);
                fileSystem.RenameTempToTarget(tempPath, destination);
            }

            console.Out.WriteLine(options.Format == PublicApiOptionsFactory.JsonFormat
                ? JsonSerializer.Serialize(new
                {
                    status = "captured",
                    contractId = options.ContractId,
                    output = destination,
                    entryCount = outcome.EntryCount,
                    alreadyCurrent = identical,
                })
                : FormatForHumans(outcome.EntryCount, destination, identical));

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return PublicApiCancellationOutput.Write(console, "capture", options.Format == PublicApiOptionsFactory.JsonFormat);
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api capture error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static string FormatForHumans(int entryCount, string destination, bool identical)
    {
        return identical
            ? $"Public API snapshot is already current ({entryCount} entries): {destination}"
            : $"Captured {entryCount} public API entries.{Environment.NewLine}Output: {destination}";
    }
}
