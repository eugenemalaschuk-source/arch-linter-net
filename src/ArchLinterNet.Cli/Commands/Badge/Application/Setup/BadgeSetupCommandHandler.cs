using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed class BadgeSetupCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string SetupHelp = "arch-linter-net badge architecture-health setup --repository <owner/name> --visibility <public|private> [--mode <none|github-raw|relay>] [--account <provider-account>] [--alias <a.......>] [--endpoint <https-origin>] [--audience <oidc-audience>] [--provider-plan <plan>] [--base-ref <branch>] [--producer-workflow <path>] [--check-name <name>] [--output <directory>] [--dry-run] [--format <json|human>]";
    private const string DoctorHelp = "arch-linter-net badge architecture-health doctor --input <badge-relay-config.json> [--capability-evidence <path>] [--observation <path>] [--public] [--format <json|human>]";

    internal int ExecuteSetup(BadgeSetupCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(SetupHelp);
            return CliExitCodes.Success;
        }

        try
        {
            BadgeSetupConfiguration? configuration = ReadOrCreateConfiguration(options, out BadgeSetupPlanResult? parseFailure);
            if (configuration is null)
            {
                return WriteResult(parseFailure!, options.Format);
            }

            (configuration, BadgeSetupPlanResult result) = Prepare(configuration, options);
            if (!options.DryRun && configuration.Mode == BadgeSetupMode.Relay.ToWireValue() && !configuration.DisclosureApproved)
            {
                result = AddDiagnostics(result, [BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.DisclosureApprovalRequired)]);
            }

            int resultCode = WriteResult(result, options.Format);
            if (!result.IsValid || options.DryRun)
            {
                return resultCode;
            }

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                console.Error.WriteLine("A non-dry-run setup requires --output <directory>.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            BadgeSetupOutputWriter.Write(options.OutputDirectory, configuration, result.Plan);
            console.Out.WriteLine($"Setup output written to {Path.GetFullPath(options.OutputDirectory)}.");
            return resultCode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            console.Error.WriteLine($"Badge setup failed: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    internal int ExecuteDoctor(BadgeSetupCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(DoctorHelp);
            return CliExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            console.Error.WriteLine("Doctor requires --input <badge-relay-config.json>.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(fileSystem.ReadAllText(options.InputPath));
            if (!parsed.IsValid || parsed.Configuration is null)
            {
                return WriteDoctor(new BadgeDoctorReport(false, parsed.Diagnostics), options);
            }

            BadgeSetupConfiguration config = parsed.Configuration;
            BadgeDoctorObservationParseResult? parsedObservation = null;
            if (!string.IsNullOrWhiteSpace(options.ObservationPath))
            {
                parsedObservation = BadgeDoctorObservationParser.Parse(fileSystem.ReadAllText(options.ObservationPath!), config);
                if (!parsedObservation.IsValid || parsedObservation.Observations is null)
                {
                    return WriteDoctor(new BadgeDoctorReport(false, parsedObservation.Diagnostics), options);
                }
            }

            BadgeSetupPlanResult plan;
            if (parsedObservation?.Observations is BadgeDoctorObservations observed)
            {
                BadgeSetupCapabilities capabilities = CapabilitiesForObservation(config, observed);
                plan = BadgeSetupEngine.BuildPlan(config, new(config.Repository.Owner, config.Repository.Name, config.Repository.Visibility, capabilities));
            }
            else
            {
                (config, plan) = Prepare(config, options);
            }

            BadgeDoctorObservations observations = parsedObservation?.Observations
                ?? (config.Mode == BadgeSetupMode.None.ToWireValue()
                    ? new()
                    : new(FirstEvidenceAvailable: false, ArtifactValid: false));
            return WriteDoctor(BadgeSetupEngine.RunDoctor(plan, observations), options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            console.Error.WriteLine($"Badge doctor failed: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private (BadgeSetupConfiguration Configuration, BadgeSetupPlanResult Plan) Prepare(
        BadgeSetupConfiguration configuration,
        BadgeSetupCommandOptions options)
    {
        BadgeSetupCapabilityInspectionResult inspection = BadgeSetupCapabilityInspector.Inspect(configuration, options, fileSystem);
        BadgeSetupConfiguration enriched = configuration with
        {
            Repository = configuration.Repository with
            {
                RepositoryId = configuration.Repository.RepositoryId ?? inspection.Repository.Capabilities.RepositoryId,
                RepositoryOwnerId = configuration.Repository.RepositoryOwnerId ?? inspection.Repository.Capabilities.RepositoryOwnerId,
            },
        };
        BadgeSetupRepositoryContext context = inspection.Repository with
        {
            Owner = enriched.Repository.Owner,
            Name = enriched.Repository.Name,
            Visibility = enriched.Repository.Visibility,
        };
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(enriched, context);
        return (enriched, AddDiagnostics(plan, inspection.Diagnostics));
    }

    private BadgeSetupConfiguration? ReadOrCreateConfiguration(
        BadgeSetupCommandOptions options,
        out BadgeSetupPlanResult? parseFailure)
    {
        parseFailure = null;
        if (!string.IsNullOrWhiteSpace(options.InputPath))
        {
            BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(fileSystem.ReadAllText(options.InputPath));
            if (!parsed.IsValid || parsed.Configuration is null)
            {
                parseFailure = InvalidResult(parsed.Diagnostics);
                return null;
            }

            return parsed.Configuration;
        }

        if (!TryParseRepository(options.Repository, out string owner, out string name))
        {
            parseFailure = InvalidResult([BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "--repository must use the owner/name form.")]);
            return null;
        }

        string visibility = options.Visibility ?? "private";
        return CreateConfiguration(options, owner, name, visibility);
    }

    private static BadgeSetupConfiguration CreateConfiguration(
        BadgeSetupCommandOptions options,
        string owner,
        string name,
        string visibility)
    {
        string mode = options.Mode ?? (visibility == "private" ? "none" : "github-raw");
        string? alias = mode == BadgeSetupMode.Relay.ToWireValue() ? options.Alias : null;
        return new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            mode,
            options.DisclosureProfile ?? BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new(owner, name, visibility, options.RepositoryId, options.RepositoryOwnerId),
            new(
                alias,
                mode == BadgeSetupMode.Relay.ToWireValue() ? options.Account : null,
                mode == BadgeSetupMode.Relay.ToWireValue() ? options.Endpoint : null,
                mode == BadgeSetupMode.Relay.ToWireValue() && alias is not null
                    ? options.Audience ?? $"architecture-health-badge-relay/{alias}"
                    : null),
            new(options.RenewalEnabled, options.CadenceMinutes ?? BadgeSetupContract.DefaultRenewalCadenceMinutes, options.MaxLeaseMinutes ?? BadgeSetupContract.DefaultLeaseMinutes),
            new(
                WorkflowRef: BadgeSetupContract.DefaultPublisherWorkflowRef,
                WorkflowSha: BadgeSetupContract.DefaultPublisherWorkflowSha,
                ActionRef: BadgeSetupContract.DefaultActionRef),
            BaseRef: options.BaseRef ?? "main",
            Project: new(options.PolicyPath ?? "architecture/dependencies.arch.yml", options.SolutionPath ?? "ArchLinterNet.slnx"),
            Producer: new(
                options.ProducerWorkflowPath ?? BadgeSetupContract.DefaultProducerWorkflowPath,
                new string('0', 40),
                options.ProducerJobName ?? BadgeSetupContract.DefaultCheckName,
                options.CheckName ?? BadgeSetupContract.DefaultCheckName,
                options.CheckApp ?? BadgeSetupContract.DefaultCheckApp,
                "pull_request",
                options.ArtifactName ?? BadgeSetupContract.DefaultArtifactName,
                options.EvidenceArtifactName ?? BadgeSetupContract.DefaultEvidenceArtifactName,
                BadgeSetupContract.DefaultPayloadPath),
            DisclosureApproved: options.ApproveDisclosure,
            ProviderPlan: options.ProviderPlan);
    }

    private int WriteDoctor(BadgeDoctorReport report, BadgeSetupCommandOptions options)
    {
        if (options.Format.Equals("human", StringComparison.OrdinalIgnoreCase))
        {
            console.Out.WriteLine(report.Available ? "available" : "unavailable");
            foreach (BadgeSetupDiagnostic diagnostic in report.Diagnostics)
            {
                BadgeSetupPublicDiagnostic safe = diagnostic.ToPublic();
                console.Out.WriteLine($"{safe.Code}: {safe.Message} Fix: {safe.Remediation}");
            }
        }
        else
        {
            object output = options.PublicDiagnostics
                ? report.ToPublic()
                : new { report.Available, diagnostics = report.Diagnostics.Select(static diagnostic => diagnostic.ToPublic()) };
            console.Out.WriteLine(JsonSerializer.Serialize(output));
        }

        return report.Available ? CliExitCodes.Success : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private int WriteResult(BadgeSetupPlanResult result, string format)
    {
        if (format.Equals("human", StringComparison.OrdinalIgnoreCase))
        {
            console.Out.WriteLine(result.Plan.IsValid ? $"ready: {result.Plan.Mode} / {result.Plan.DisclosureProfile}" : "unavailable");
            foreach (BadgeSetupDiagnostic diagnostic in result.Diagnostics)
            {
                BadgeSetupPublicDiagnostic safe = diagnostic.ToPublic();
                console.Out.WriteLine($"{safe.Code}: {safe.Message} Fix: {safe.Remediation}");
            }
        }
        else
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                result.Plan.IsValid,
                result.Plan.Mode,
                result.Plan.DisclosureProfile,
                result.Plan.ExternalCallsExpected,
                result.Plan.PublicEndpointExpected,
                result.Plan.Prerequisites,
                result.Plan.Cost,
                result.Plan.PlannedChanges,
                diagnostics = result.Diagnostics.Select(static diagnostic => diagnostic.ToPublic()),
            }));
        }

        return result.IsValid ? CliExitCodes.Success : CliExitCodes.InvalidArgumentsOrRuntimeError;
    }

    private static BadgeSetupPlanResult AddDiagnostics(
        BadgeSetupPlanResult result,
        IReadOnlyList<BadgeSetupDiagnostic> additional)
    {
        if (additional.Count == 0)
        {
            return result;
        }

        List<BadgeSetupDiagnostic> diagnostics = [.. result.Diagnostics, .. additional];
        BadgeSetupPlan plan = result.Plan with
        {
            IsValid = false,
            PlannedChanges = [],
            Diagnostics = diagnostics,
        };
        return new(plan, diagnostics);
    }

    private static BadgeSetupPlanResult InvalidResult(IReadOnlyList<BadgeSetupDiagnostic> diagnostics) => new(
        new BadgeSetupPlan(false, "unknown", "unknown", false, false, [], BadgeSetupCostEstimate.None, [], diagnostics),
        diagnostics);

    private static BadgeSetupCapabilities CapabilitiesForObservation(
        BadgeSetupConfiguration configuration,
        BadgeDoctorObservations observations) => new(
        HasRequiredCheck: observations.RequiredCheckAvailable,
        HasRulesApi: observations.RulesApiAvailable,
        CanUseGithubRaw: configuration.Mode == BadgeSetupMode.GithubRaw.ToWireValue()
            && observations.DestinationReachable
            && observations.IdentityValid
            && observations.PinsValid,
        CanUseOidc: observations.OidcValid,
        CanUseRelay: observations.IdentityValid && observations.PinsValid && observations.OidcValid,
        ProviderPlan: configuration.ProviderPlan,
        RepositoryId: configuration.Repository.RepositoryId,
        RepositoryOwnerId: configuration.Repository.RepositoryOwnerId,
        ProviderQuotaAvailable: observations.ProviderQuotaAvailable,
        CapabilitySource: "doctor-observation");

    private static bool TryParseRepository(string? value, out string owner, out string name)
    {
        owner = string.Empty;
        name = string.Empty;
        string[] parts = value?.Split('/', StringSplitOptions.None) ?? [];
        if (parts.Length != 2 || parts.Any(static part => part.Length > 100 || part.Length == 0 || part.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
        {
            return false;
        }

        owner = parts[0];
        name = parts[1];
        return true;
    }
}
