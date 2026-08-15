using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi.Application;

internal sealed class PublicApiUpdateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
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
                console,
                fileSystem,
                new PublicApiCommandGuards.Invocation(
                    options.PolicyPath, options.ContractId, options.Format, CommandName, PublicApiOptionsFactory.OperationFormats),
                out int exitCode))
        {
            return exitCode;
        }

        if (options.SnapshotPath == null)
        {
            CliErrorOutputWriter.Write(
                console, options.Format, "invalid-arguments", "--snapshot is required for public-api update.");
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
                PreparationMode = options.EnsureBuilt ? BuildPreparationMode.EnsureBuilt : BuildPreparationMode.Ordinary,
                NoRestore = options.NoRestore,
                CancellationToken = cancellationToken,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(
                    console, options.Format, CommandName, outcome.Error!, outcome.PreflightDiagnostics, outcome.FailureKind);
                return PublicApiCommandGuards.ExitCodeFor(outcome.FailureKind);
            }

            // Core validated and read the boundary-resolved destination; writing the raw
            // cwd-relative string here would update a different file and still report success.
            string destination = outcome.ResolvedSnapshotPath!;

            // Re-checked immediately before publication: outcome above may have taken long
            // enough that cancellation arrived after Core's own last check but before this
            // handler commits anything or prints a completion document — including a dry-run
            // preview, which reports a proposed-content document as if the operation had
            // completed normally.
            cancellationToken.ThrowIfCancellationRequested();

            if (!options.DryRun)
            {
                PublicApiTwoPhaseWriter.WriteAndCommit(fileSystem, destination, outcome.Snapshot!, cancellationToken);
            }

            // One document per invocation: for `json` the delta, status, destination, and proposed
            // content are all fields of a single object, so stdout stays parsable end to end.
            console.Out.WriteLine(options.Format == PublicApiOptionsFactory.JsonFormat
                ? FormatAsJson(outcome, destination, options.DryRun)
                : FormatForHumans(outcome, destination, options.DryRun));

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return PublicApiCancellationOutput.Write(console, "update", options.Format == PublicApiOptionsFactory.JsonFormat);
        }
        catch (Exception ex)
        {
            CliErrorOutputWriter.Write(console, options.Format, "unexpected-tool-failure", $"public-api update error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private string FormatForHumans(PublicApiUpdateOutcome outcome, string destination, bool dryRun)
    {
        List<string> lines = new()
        {
            PublicApiDeltaFormatter.Format(runtime, PublicApiOptionsFactory.HumanFormat, "update", outcome.Delta),
        };

        if (dryRun)
        {
            lines.Add($"Dry run: '{destination}' was not modified. Proposed content:");
            lines.Add(outcome.Snapshot!);
        }
        else
        {
            lines.Add($"Updated: {destination}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatAsJson(PublicApiUpdateOutcome outcome, string destination, bool dryRun)
    {
        return JsonSerializer.Serialize(new
        {
            status = dryRun ? "dry-run" : "updated",
            dryRun,
            snapshotPath = destination,
            delta = new
            {
                added = outcome.Delta.Added.Select(Describe),
                removed = outcome.Delta.Removed.Select(Describe),
                changed = outcome.Delta.Changed.Select(Describe),
            },
            proposedSnapshot = outcome.Snapshot,
        });
    }

    private static object Describe(PublicApiDeltaEntry entry)
    {
        return new
        {
            api_delta_kind = entry.Kind.ToString().ToLowerInvariant(),
            assembly = entry.AssemblyName,
            api_signature = entry.Signature,
            previous_api_signature = entry.PreviousSignature,
        };
    }
}
