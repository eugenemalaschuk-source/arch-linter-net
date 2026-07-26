using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.PublicApi;

internal sealed class PublicApiMigrateCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
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

            if (!options.DryRun)
            {
                string tempPath = fileSystem.WriteAllTextToTemp(destination, outcome.Snapshot!);
                fileSystem.RenameTempToTarget(tempPath, destination);
            }

            console.Out.WriteLine(options.Format == PublicApiOptionsFactory.JsonFormat
                ? FormatAsJson(outcome, options, destination)
                : FormatForHumans(outcome, options, destination));

            return CliExitCodes.Success;
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
        string? output = options.DryRun ? null : destination;
        string status = options.DryRun ? "dry-run" : "migrated";

        return JsonSerializer.Serialize(new
        {
            status,
            contractId = options.ContractId,
            output,
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
