using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiDiffCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    private const string CommandName = "diff";

    public int Execute(PublicApiDiffCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(PublicApiHelpTexts.DiffHelpText);
            return CliExitCodes.Success;
        }

        if (!PublicApiCommandGuards.TryValidateCommon(
                console,
                fileSystem,
                new PublicApiCommandGuards.Invocation(
                    options.PolicyPath, options.ContractId, options.Format, CommandName, PublicApiOptionsFactory.SupportedFormats),
                out int exitCode))
        {
            return exitCode;
        }

        if (options.SnapshotPath == null)
        {
            console.Error.WriteLine("--snapshot is required for public-api diff.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            PublicApiDiffOutcome outcome = runtime.DiffPublicApi(new PublicApiDiffRequest
            {
                PolicyPath = options.PolicyPath,
                ContractId = options.ContractId!,
                SnapshotPath = options.SnapshotPath,
                ConditionSetName = options.ConditionSetName,
                PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                NoRestore = options.NoRestore,
                CancellationToken = cancellationToken,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(console, CommandName, outcome.Error!, outcome.PreflightDiagnostics);
                return PublicApiCommandGuards.ExitCodeFor(outcome.FailureKind);
            }

            cancellationToken.ThrowIfCancellationRequested();

            console.Out.WriteLine(PublicApiDeltaFormatter.Format(
                runtime, options.Format, options.ContractId!, outcome.Delta));

            // Drift is a validation failure, not a runtime error: CI can gate on exit code 1 the
            // same way it gates on a failing strict run.
            return outcome.InSync ? CliExitCodes.Success : CliExitCodes.ValidationFailure;
        }
        catch (OperationCanceledException)
        {
            return PublicApiCancellationOutput.Write(console, "diff", options.Format == PublicApiOptionsFactory.JsonFormat);
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api diff error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
