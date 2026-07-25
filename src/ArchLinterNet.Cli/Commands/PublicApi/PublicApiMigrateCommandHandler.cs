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
                console, fileSystem, options.PolicyPath, options.ContractId, options.Format, CommandName, out int exitCode))
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
                return CliExitCodes.ValidationFailure;
            }

            if (!options.DryRun)
            {
                string tempPath = fileSystem.WriteAllTextToTemp(options.OutputPath, outcome.Snapshot!);
                fileSystem.RenameTempToTarget(tempPath, options.OutputPath);
            }

            console.Out.WriteLine(options.Format == "json"
                ? JsonSerializer.Serialize(new
                {
                    contractId = options.ContractId,
                    output = options.DryRun ? null : options.OutputPath,
                    dryRun = options.DryRun,
                    acceptedDrift = outcome.HasDrift,
                    staleDeclarations = outcome.StaleDeclarations,
                    undeclaredSurface = outcome.UndeclaredSurface,
                })
                : FormatForHumans(outcome, options));

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"public-api migrate error: {ex.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static string FormatForHumans(PublicApiMigrateOutcome outcome, PublicApiMigrateCommandOptions options)
    {
        List<string> lines = new()
        {
            $"Stale inline declarations: {outcome.StaleDeclarations.Count}",
            $"Undeclared exported members: {outcome.UndeclaredSurface.Count}",
        };

        lines.AddRange(outcome.StaleDeclarations.Select(signature => $"  [stale] {signature}"));
        lines.AddRange(outcome.UndeclaredSurface.Select(signature => $"  [undeclared] {signature}"));

        lines.Add(options.DryRun
            ? $"Dry run: '{options.OutputPath}' was not written."
            : $"Output: {options.OutputPath}");

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
