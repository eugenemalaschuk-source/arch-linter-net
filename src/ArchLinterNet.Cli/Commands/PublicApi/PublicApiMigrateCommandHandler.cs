using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiMigrateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem, CancellationToken cancellationToken = default)
{
    private const string CommandName = "migrate";

    public int Execute(PublicApiMigrateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(PublicApiHelpTexts.MigrateHelpText);
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
            console.Error.WriteLine("--output is required for public-api migrate.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            PublicApiMigrateOutcome outcome = runtime.MigratePublicApi(new PublicApiMigrateRequest
            {
                PolicyPath = options.PolicyPath,
                ContractId = options.ContractId!,
                OutputPath = options.OutputPath,
                AcceptDrift = options.AcceptDrift,
                ConditionSetName = options.ConditionSetName,
                CancellationToken = cancellationToken,
            });

            if (!outcome.Succeeded)
            {
                PublicApiCommandGuards.WriteError(console, CommandName, outcome.Error!, outcome.PreflightDiagnostics);
                WriteDrift(outcome);

                // Only refused migration drift is a completed gate reporting a finding; an unknown
                // contract, an unsafe path, or blocked preflight never completed at all.
                return PublicApiCommandGuards.ExitCodeFor(outcome.FailureKind);
            }

            string destination = outcome.ResolvedOutputPath!;

            // migrate writes a brand-new reviewed artifact, same as capture: it must not silently
            // destroy an existing file — another contract's snapshot, or anything else repository-
            // local except the policy itself (TryResolveDestination already refuses that one).
            bool exists = fileSystem.FileExists(destination);
            bool identical = exists && string.Equals(
                fileSystem.ReadAllText(destination), outcome.Snapshot, StringComparison.Ordinal);

            if (!options.DryRun && exists && !identical && !options.Force)
            {
                console.Error.WriteLine(
                    $"'{destination}' already exists and differs from the migrated snapshot. Re-run with " +
                    "--force to replace it, or point --output at a new file.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            // Re-checked immediately before publication, whether that means writing the
            // migrated snapshot or just reporting a dry-run preview.
            cancellationToken.ThrowIfCancellationRequested();

            if (!options.DryRun && !identical)
            {
                string tempPath = fileSystem.WriteAllTextToTemp(destination, outcome.Snapshot!);
                fileSystem.RenameTempToTarget(tempPath, destination);
            }

            console.Out.WriteLine(options.Format == PublicApiOptionsFactory.JsonFormat
                ? FormatAsJson(outcome, options, destination)
                : FormatForHumans(outcome, options, destination));

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return PublicApiCancellationOutput.Write(console, "migrate", options.Format == PublicApiOptionsFactory.JsonFormat);
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api migrate error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static string FormatAsJson(
        PublicApiMigrateOutcome outcome, PublicApiMigrateCommandOptions options, string destination)
    {
        string status = options.DryRun ? "dry-run" : "migrated";

        // `output` always names the destination, dry-run or not — matching capture/update's JSON —
        // since a dry-run still has to tell the caller what it *would* have written. `dryRun`, not a
        // null-vs-non-null `output`, is what signals whether anything was actually written.
        return JsonSerializer.Serialize(new
        {
            status,
            contractId = options.ContractId,
            output = destination,
            dryRun = options.DryRun,
            acceptedDrift = outcome.HasDrift,
            staleDeclarations = outcome.StaleDeclarations,
            undeclaredSurface = outcome.UndeclaredSurface,
        });
    }

    private static string FormatForHumans(
        PublicApiMigrateOutcome outcome, PublicApiMigrateCommandOptions options, string destination)
    {
        List<string> lines = new()
        {
            $"Stale inline declarations: {outcome.StaleDeclarations.Count}",
            $"Undeclared exported members: {outcome.UndeclaredSurface.Count}",
        };

        lines.AddRange(outcome.StaleDeclarations.Select(signature => $"  [stale] {signature}"));
        lines.AddRange(outcome.UndeclaredSurface.Select(signature => $"  [undeclared] {signature}"));

        lines.Add(options.DryRun
            ? $"Dry run: '{destination}' was not written."
            : $"Output: {destination}");

        if (!options.DryRun)
        {
            lines.Add(
                $"Next: set 'api_snapshot: {options.OutputPath}' on contract '{options.ContractId}' and remove " +
                "its inline 'declared_api' entries.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void WriteDrift(PublicApiMigrateOutcome outcome)
    {
        foreach (string signature in outcome.StaleDeclarations)
        {
            console.Error.WriteLine($"  [stale] {signature}");
        }

        foreach (string signature in outcome.UndeclaredSurface)
        {
            console.Error.WriteLine($"  [undeclared] {signature}");
        }
    }
}
