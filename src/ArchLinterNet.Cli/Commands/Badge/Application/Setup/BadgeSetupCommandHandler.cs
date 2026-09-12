using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed class BadgeSetupCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    private const string SetupHelp = "arch-linter-net badge architecture-health setup --repository <owner/name> --visibility <public|private> [--mode <none|github-raw|relay>] [--output <directory>] [--dry-run] [--format <json|human>]";
    private const string DoctorHelp = "arch-linter-net badge architecture-health doctor --input <badge-relay-config.json> [--public] [--format <json|human>]";

    internal int ExecuteSetup(BadgeSetupCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(SetupHelp);
            return CliExitCodes.Success;
        }

        BadgeSetupConfiguration? configuration;
        BadgeSetupPlanResult result;
        try
        {
            if (!string.IsNullOrWhiteSpace(options.InputPath))
            {
                BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(fileSystem.ReadAllText(options.InputPath));
                if (!parsed.IsValid || parsed.Configuration is null)
                {
                    result = InvalidResult(parsed.Diagnostics);
                    return WriteResult(result, options.Format);
                }

                configuration = parsed.Configuration;
                BadgeSetupRepositoryContext repository = new(
                    configuration.Repository.Owner,
                    configuration.Repository.Name,
                    configuration.Repository.Visibility,
                    CapabilitiesFor(configuration.Mode, options));
                result = BadgeSetupEngine.BuildPlan(configuration, repository);
            }
            else
            {
                if (!TryParseRepository(options.Repository, out string owner, out string name))
                {
                    return WriteResult(InvalidResult([BadgeSetupDiagnosticCatalog.Create(
                        BadgeSetupDiagnosticCodes.MalformedIdentity,
                        privateDetail: "--repository must use the owner/name form.")]), options.Format);
                }

                string visibility = options.Visibility ?? "private";
                configuration = CreateConfiguration(options, owner, name, visibility);
                result = BadgeSetupEngine.BuildPlan(new BadgeSetupRequest(
                    new(owner, name, visibility, CapabilitiesFor(configuration.Mode, options)),
                    configuration.Mode,
                    configuration.DisclosureProfile,
                    configuration.Destination.Account,
                    configuration.Destination.Alias,
                    configuration.Renewal.Enabled,
                    configuration.Renewal.CadenceMinutes,
                    configuration.Renewal.MaxLeaseMinutes));
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
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
            BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(
                config,
                new(config.Repository.Owner, config.Repository.Name, config.Repository.Visibility, CapabilitiesFor(config.Mode, options)));
            BadgeDoctorObservations observations = config.Mode == BadgeSetupMode.None.ToWireValue()
                ? new()
                : new(FirstEvidenceAvailable: false, ArtifactValid: false);
            return WriteDoctor(BadgeSetupEngine.RunDoctor(plan, observations), options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            console.Error.WriteLine($"Badge doctor failed: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
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
            object output = options.PublicDiagnostics ? report.ToPublic() : new { report.Available, diagnostics = report.Diagnostics.Select(static diagnostic => diagnostic.ToPublic()) };
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

    private static BadgeSetupPlanResult InvalidResult(IReadOnlyList<BadgeSetupDiagnostic> diagnostics) => new(
        new BadgeSetupPlan(false, "unknown", "unknown", false, false, [], BadgeSetupCostEstimate.None, [], diagnostics),
        diagnostics);

    private static BadgeSetupConfiguration CreateConfiguration(BadgeSetupCommandOptions options, string owner, string name, string visibility)
    {
        string mode = options.Mode ?? (visibility == "private" ? "none" : "github-raw");
        return new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            mode,
            options.DisclosureProfile ?? BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new(owner, name, visibility),
            new(mode == "relay" ? options.Alias : null, mode == "relay" ? options.Account : null, mode == "relay" ? options.Endpoint : null),
            new(options.RenewalEnabled, options.CadenceMinutes ?? BadgeSetupContract.DefaultRenewalCadenceMinutes, options.MaxLeaseMinutes ?? BadgeSetupContract.DefaultLeaseMinutes),
            new(
                WorkflowRef: "eugenemalaschuk-source/arch-linter-net/.github/workflows/architecture-health-badge-promotion.yml",
                WorkflowSha: "ff9b19bfe5abcab233d490ea53f55a387dc4a8db",
                ActionRef: "eugenemalaschuk-source/arch-linter-net/.github/actions/architecture-health-badge-promotion"));
    }

    private static BadgeSetupCapabilities CapabilitiesFor(string mode, BadgeSetupCommandOptions options) => new(
        HasRequiredCheck: mode == "none" || mode == "relay" && !string.IsNullOrWhiteSpace(options.ProviderPlan),
        HasRulesApi: mode == "none" || mode == "relay" && !string.IsNullOrWhiteSpace(options.ProviderPlan),
        CanUseOidc: mode != "relay" || !string.IsNullOrWhiteSpace(options.ProviderPlan),
        CanUseGithubRaw: mode == "github-raw",
        CanUseRelay: mode == "relay" && !string.IsNullOrWhiteSpace(options.ProviderPlan),
        ProviderPlan: options.ProviderPlan);

    private static bool TryParseRepository(string? value, out string owner, out string name)
    {
        owner = string.Empty;
        name = string.Empty;
        string[] parts = value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length != 2 || parts.Any(static part => part.Length > 100 || part.Any(static character => !char.IsLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
        {
            return false;
        }

        owner = parts[0];
        name = parts[1];
        return true;
    }
}

internal static class BadgeSetupOutputWriter
{
    private const string ManagedMarker = "<!-- arch-linter-net:managed-badge-setup/v1 -->";

    internal static void Write(string outputDirectory, BadgeSetupConfiguration configuration, BadgeSetupPlan plan)
    {
        string root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        string configJson = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        string workflow = RenderWorkflow(configuration);
        string readme = ManagedMarker + Environment.NewLine + RenderReadme(configuration) + Environment.NewLine;
        string registry = RenderRegistry(configuration);
        List<GeneratedFile> files =
        [
            new("badge-relay-config.json", configJson),
            new(".github/workflows/architecture-health-badge-setup.yml", workflow),
            new("README.badge.md", readme),
            new(".github/badge-promotion/registry.json", registry),
            new("schema/0.8.0/badge-relay-config.schema.json", ReadAsset("schema/0.8.0/badge-relay-config.schema.json")),
        ];
        if (configuration.Mode == "relay")
        {
            files.AddRange(ReadRelayBundle());
        }

        string manifest = JsonSerializer.Serialize(new
        {
            schema_id = "badge-relay-manifest/v1",
            bundle = configuration.Bundle,
            compatibility_plan = configuration.CompatibilityPlan,
            mode = plan.Mode,
            files = files.Select(static file => FileEntry(file.Path, file.Contents)).ToArray(),
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

        foreach (GeneratedFile file in files)
        {
            EnsureNoConflict(root, file.Path, file.Contents);
        }

        EnsureNoConflict(root, "badge-relay-manifest.json", manifest);
        List<GeneratedFile> allFiles = [.. files, new("badge-relay-manifest.json", manifest)];
        OriginalFile[] originals = allFiles.Select(file => CaptureOriginal(root, file.Path)).ToArray();
        try
        {
            foreach (GeneratedFile file in allFiles)
            {
                WriteManaged(root, file.Path, file.Contents);
            }
        }
        catch
        {
            RestoreOriginals(root, originals);
            throw;
        }
    }

    private static string ReadAsset(string relativePath)
    {
        string? path = FindRepositoryPath(relativePath, "architecture-health-badge-setup")
            ?? FindRepositoryPath(relativePath, string.Empty);
        return path is null
            ? throw new IOException($"Shipped setup asset '{relativePath}' is missing.")
            : File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IReadOnlyList<GeneratedFile> ReadRelayBundle()
    {
        string? root = FindRepositoryPath("relay", "architecture-health-badge-setup")
            ?? FindRepositoryPath("relay", string.Empty);
        if (root is null)
        {
            throw new IOException("The shipped badge Relay bundle is missing.");
        }

        List<GeneratedFile> files = [];
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.ts", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.Ordinal))
        {
            files.Add(new("relay/src/" + Path.GetFileName(source), File.ReadAllText(source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))));
        }

        foreach (string fileName in new[] { "package.json", "package-lock.json", "tsconfig.json", "wrangler.jsonc" })
        {
            files.Add(new("relay/" + fileName, ReadText(Path.Combine(root, fileName))));
        }

        return files;
    }

    private static string ReadText(string path) => File.Exists(path)
        ? File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        : throw new IOException($"The shipped badge Relay asset '{Path.GetFileName(path)}' is missing.");

    private static string? FindRepositoryPath(string relativePath, string packagedRoot)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = string.IsNullOrEmpty(packagedRoot)
                ? Path.Combine(directory.FullName, relativePath)
                : Path.Combine(directory.FullName, packagedRoot, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed record GeneratedFile(string Path, string Contents);

    private sealed record OriginalFile(string Path, bool Exists, byte[]? Contents);

    private static OriginalFile CaptureOriginal(string root, string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        return new(relativePath, File.Exists(path), File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    private static void RestoreOriginals(string root, IReadOnlyList<OriginalFile> originals)
    {
        foreach (OriginalFile original in originals.Reverse())
        {
            string path = Path.GetFullPath(Path.Combine(root, original.Path));
            if (original.Exists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, original.Contents!);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static object FileEntry(string path, string contents) => new { path, sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant() };

    private static string RenderWorkflow(BadgeSetupConfiguration configuration)
    {
        if (configuration.Mode == "none")
        {
            return ManagedMarker + Environment.NewLine + "# No public publisher is generated for the none transport." + Environment.NewLine;
        }

        string workflowRef = configuration.Pins?.WorkflowRef ?? "eugenemalaschuk-source/arch-linter-net/.github/workflows/architecture-health-badge-promotion.yml";
        string workflowSha = configuration.Pins?.WorkflowSha ?? "ff9b19bfe5abcab233d490ea53f55a387dc4a8db";
        return $"{ManagedMarker}\nname: Publish Architecture Health badge\n\non:\n  push:\n    branches: [main]\n\npermissions:\n  actions: read\n  checks: read\n  contents: read\n  id-token: write\n  pull-requests: read\n\njobs:\n  publish:\n    uses: {workflowRef}@{workflowSha}\n    with:\n      configuration-id: setup_generated\n      adapter: {configuration.Mode}\n      operation: publish\n";
    }

    private static string RenderReadme(BadgeSetupConfiguration configuration)
    {
        if (configuration.Mode == "none")
        {
            return "Architecture Health badge publication is disabled (`none`). Private reports and checks remain available.";
        }

        string endpoint = configuration.Mode == "github-raw"
            ? $"https://raw.githubusercontent.com/{configuration.Repository.Owner}/{configuration.Repository.Name}/architecture-health-badge/architecture-health.json"
            : $"{configuration.Destination.Endpoint?.TrimEnd('/') ?? "https://<approved-relay-host>"}/badge-relay/v1/{configuration.Destination.Alias}.{(configuration.DisclosureProfile == BadgeSetupContract.HeadlinePlusFreshnessProfile ? "svg" : "json")}";
        return $"![Architecture Health]({endpoint})";
    }

    private static string RenderRegistry(BadgeSetupConfiguration configuration)
    {
        string adapter = configuration.Mode;
        object destination = adapter switch
        {
            "github-raw" => new { adapter, branch = "architecture-health-badge", endpoint_path = "architecture-health.json" },
            "relay" => new { adapter, alias = configuration.Destination.Alias, endpoint = configuration.Destination.Endpoint, audience = "architecture-health-badge-relay" },
            _ => new { adapter },
        };
        return JsonSerializer.Serialize(new
        {
            schema = "architecture-health-badge-promotion/registry/v1",
            configurations = new
            {
                setup_generated = new
                {
                    schema_id = "architecture-health-badge-promotion/v1",
                    repository = $"{configuration.Repository.Owner}/{configuration.Repository.Name}",
                    repository_visibility = configuration.Repository.Visibility,
                    base_ref = "main",
                    producer = new
                    {
                        workflow_path = ".github/workflows/ci.yml",
                        workflow_sha = configuration.Pins?.WorkflowSha ?? "ff9b19bfe5abcab233d490ea53f55a387dc4a8db",
                        job_name = "Architecture Coverage",
                        check_name = "Architecture Coverage",
                        check_app = "github-actions",
                        @event = "pull_request",
                        artifact_name = "architecture-health-badge-v1",
                        evidence_artifact_name = "architecture-health",
                        payload_path = "architecture-health-badge.json",
                    },
                    destination,
                    disclosure_profile = configuration.DisclosureProfile,
                    validity = new { max_lease_seconds = configuration.Renewal.MaxLeaseMinutes * 60 },
                    limits = new { max_archive_bytes = 65536, max_member_bytes = 16384, max_payload_bytes = 16384, max_members = 2 },
                },
            },
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static void WriteManaged(string root, string relativePath, string contents)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new IOException("Managed output path escapes the setup directory.");
        }

        string? directory = Path.GetDirectoryName(path);
        if (directory is not null) Directory.CreateDirectory(directory);
        if (File.Exists(path) && File.ReadAllText(path) != contents)
        {
            throw new IOException($"Managed output conflict at '{relativePath}'. Review or remove the existing managed file before retrying.");
        }

        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    private static void EnsureNoConflict(string root, string relativePath, string contents)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (File.Exists(path) && File.ReadAllText(path) != contents)
        {
            throw new IOException($"Managed output conflict at '{relativePath}'. Review or remove the existing managed file before retrying.");
        }
    }
}
