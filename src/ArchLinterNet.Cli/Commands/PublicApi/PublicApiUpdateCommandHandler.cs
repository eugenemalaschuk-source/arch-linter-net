using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiUpdateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    private const string CommandName = "update";

    public int Execute(PublicApiUpdateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(PublicApiHelpTexts.UpdateHelpText);
            return CliExitCodes.Success;
        }

        if (!PublicApiCommandGuards.TryValidateCommon(
                console, fileSystem, options.PolicyPath, options.ContractId, options.Format, CommandName, out int exitCode))
        {
            return exitCode;
        }

        if (options.SnapshotPath == null)
        {
            console.Error.WriteLine("--snapshot is required for public-api update.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            PublicApiUpdateOutcome outcome = runtime.UpdatePublicApi(new PublicApiUpdateRequest
            {
                PolicyPath = options.PolicyPath,
                ContractId = options.ContractId!,
                SnapshotPath = options.SnapshotPath,
                DryRun = options.DryRun,
                ConditionSetName = options.ConditionSetName,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(console, CommandName, outcome.Error!, outcome.PreflightDiagnostics);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            console.Out.WriteLine(PublicApiDeltaFormatter.Format(
                runtime, options.Format, options.ContractId!, outcome.Delta));

            if (options.DryRun)
            {
                console.Out.WriteLine($"Dry run: '{options.SnapshotPath}' was not modified. Proposed content:");
                console.Out.WriteLine(outcome.Snapshot!);
                return CliExitCodes.Success;
            }

            string tempPath = fileSystem.WriteAllTextToTemp(options.SnapshotPath, outcome.Snapshot!);
            fileSystem.RenameTempToTarget(tempPath, options.SnapshotPath);
            console.Out.WriteLine($"Updated: {options.SnapshotPath}");
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api update error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
