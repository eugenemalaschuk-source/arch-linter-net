using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

/// <summary>
/// Thin authenticated client for the private Relay lifecycle API.
///
/// The handler deliberately owns no Relay state and accepts an HttpClient
/// factory so command tests never need a network connection. Tokens are read
/// only immediately before a real request and are never included in output.
/// </summary>
internal sealed class BadgeLifecycleCommandHandler
{
    internal const string AdminTokenEnvironmentVariable = "ARCHLINTERNET_BADGE_ADMIN_TOKEN";
    internal const string AdminOriginEnvironmentVariable = "ARCHLINTERNET_BADGE_ADMIN_ORIGIN";
    private const int MaximumJsonBytes = 64 * 1024;
    private const string StatusOperation = "status";
    private const string InvalidConfigurationReason = "invalid-configuration";
    private const string HumanFormat = "human";
    private const string Help =
        "arch-linter-net badge architecture-health lifecycle --operation <status|invalidate|revoke|rename|transfer|rotate|remove|recover|upgrade|activate|rollback> "
        + "--input <badge-relay-config.json> [--alias <a.......>] [--expected-generation <n>] [--expected-epoch <n>] "
        + "[--workflow-ref <ref>] [--to <bundle-digest>] [--approve-withdrawal|--approve-recovery] [--dry-run] [--format <json|human>]";

    private static readonly HashSet<string> _supportedOperations = new(StringComparer.Ordinal)
    {
        StatusOperation, "invalidate", "revoke", "rename", "transfer", "rotate", "remove", "recover", "upgrade", "activate", "rollback",
    };

    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly Func<string?> _adminTokenProvider;
    private readonly Func<string?> _adminOriginProvider;

    internal BadgeLifecycleCommandHandler(
        ICliConsole console,
        IFileSystem fileSystem,
        Func<HttpClient>? httpClientFactory = null,
        Func<string?>? adminTokenProvider = null,
        Func<string?>? adminOriginProvider = null)
    {
        _console = console;
        _fileSystem = fileSystem;
        _httpClientFactory = httpClientFactory ?? (() => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        _adminTokenProvider = adminTokenProvider ?? (() => Environment.GetEnvironmentVariable(AdminTokenEnvironmentVariable));
        _adminOriginProvider = adminOriginProvider ?? (() => Environment.GetEnvironmentVariable(AdminOriginEnvironmentVariable));
    }

    internal int Execute(BadgeLifecycleCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            _console.Out.WriteLine(Help);
            return CliExitCodes.Success;
        }

        if (!TryValidateOptions(options, out string validationError))
        {
            WriteFailure(options.Format, "invalid-arguments", validationError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        BadgeSetupConfiguration? configuration;
        try
        {
            BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(
                _fileSystem.ReadAllText(options.InputPath!));
            if (!parsed.IsValid || parsed.Configuration is null)
            {
                WriteFailure(options.Format, InvalidConfigurationReason, "The setup configuration is malformed.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            configuration = parsed.Configuration;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            WriteFailure(options.Format, InvalidConfigurationReason, "The setup configuration could not be read.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!TryValidateRelayConfiguration(configuration, options.Alias, out string relayError))
        {
            WriteFailure(options.Format, InvalidConfigurationReason, relayError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string alias = options.Alias ?? configuration.Destination.Alias!;
        if (options.DryRun)
        {
            // This branch must stay before token lookup and HttpClient creation.
            WriteDryRun(options, configuration, alias);
            return CliExitCodes.Success;
        }

        string? token = _adminTokenProvider();
        if (string.IsNullOrWhiteSpace(token))
        {
            WriteFailure(options.Format, "missing-admin-token", $"Set {AdminTokenEnvironmentVariable} for a non-dry-run lifecycle operation.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? operatorOrigin = _adminOriginProvider();
        if (!TryValidateOperatorOrigin(configuration, operatorOrigin, out string originError, out string relayOrigin))
        {
            WriteFailure(options.Format, InvalidConfigurationReason, originError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            HttpClient client = _httpClientFactory();
            using HttpRequestMessage request = BuildRequest(configuration, relayOrigin, alias, options, token);
            using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            string responseJson = ReadBoundedResponse(response);
            return WriteResponse(options, alias, response.StatusCode, responseJson);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException or InvalidOperationException or JsonException)
        {
            WriteFailure(options.Format, "relay-unavailable", "The badge Relay lifecycle request could not be completed.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static bool TryValidateOptions(BadgeLifecycleCommandOptions options, out string error)
    {
        string? validationError = ValidateOperation(options)
            ?? ValidateApproval(options)
            ?? ValidateInputPath(options)
            ?? ValidateFormat(options)
            ?? ValidateAliases(options)
            ?? ValidateIdentity(options)
            ?? ValidateDigests(options)
            ?? ValidateAudience(options)
            ?? ValidateFieldSizes(options)
            ?? ValidateExpectedValues(options);
        error = validationError ?? string.Empty;
        return validationError is null;
    }

    private static string? ValidateOperation(BadgeLifecycleCommandOptions options) =>
        _supportedOperations.Contains(options.Operation)
            ? null
            : "The lifecycle operation is not supported.";

    private static string? ValidateApproval(BadgeLifecycleCommandOptions options)
    {
        if (!options.DryRun && options.Operation is "revoke" or "transfer" or "remove" && !options.ApproveWithdrawal)
        {
            return "This destructive operation requires --approve-withdrawal.";
        }

        return !options.DryRun && options.Operation is "recover" && !options.ApproveRecovery
            ? "Recovery requires --approve-recovery."
            : null;
    }

    private static string? ValidateInputPath(BadgeLifecycleCommandOptions options) =>
        string.IsNullOrWhiteSpace(options.InputPath)
            ? "Lifecycle requires --input <badge-relay-config.json>."
            : null;

    private static string? ValidateFormat(BadgeLifecycleCommandOptions options) =>
        options.Format.Equals("json", StringComparison.OrdinalIgnoreCase)
            || options.Format.Equals(HumanFormat, StringComparison.OrdinalIgnoreCase)
            ? null
            : "Invalid format. Use 'json' or 'human'.";

    private static string? ValidateAliases(BadgeLifecycleCommandOptions options)
    {
        if (options.Alias is not null && !BadgeSetupValidationHelpers.IsOpaqueAlias(options.Alias))
        {
            return "The lifecycle alias must be an approved opaque alias.";
        }

        return options.NewAlias is not null && !BadgeSetupValidationHelpers.IsOpaqueAlias(options.NewAlias)
            ? "The new lifecycle alias must be an approved opaque alias."
            : null;
    }

    private static string? ValidateIdentity(BadgeLifecycleCommandOptions options) =>
        options.NewOwner is not null && !BadgeSetupValidationHelpers.IsIdentity(options.NewOwner)
            || options.NewRepository is not null && !BadgeSetupValidationHelpers.IsIdentity(options.NewRepository)
            ? "The lifecycle display identity contains unsupported characters."
            : null;

    private static string? ValidateDigests(BadgeLifecycleCommandOptions options)
    {
        if (options.WorkflowSha is not null && !BadgeSetupValidationHelpers.IsSha(options.WorkflowSha, 40))
        {
            return "The workflow SHA must be a lowercase 40-character hexadecimal digest.";
        }

        return options.TargetDigest is not null && !BadgeSetupValidationHelpers.IsSha(options.TargetDigest, 64)
            ? "The lifecycle bundle digest must be a lowercase 64-character hexadecimal digest."
            : null;
    }

    private static string? ValidateAudience(BadgeLifecycleCommandOptions options) =>
        options.Audience is not null && !BadgeSetupValidationHelpers.IsSafeAudience(options.Audience)
            ? "The lifecycle audience contains unsupported characters."
            : null;

    private static string? ValidateFieldSizes(BadgeLifecycleCommandOptions options) =>
        new[] { options.NewOwner, options.NewRepository, options.Bundle, options.ContractVersion, options.CompatibilityPlan, options.Manifest, options.WorkflowRef }
            .Any(static value => value is not null && Encoding.UTF8.GetByteCount(value) > 16 * 1024)
            ? "Lifecycle request fields must be at most 16 KiB."
            : null;

    private static string? ValidateExpectedValues(BadgeLifecycleCommandOptions options)
    {
        long? epoch = options.ExpectedRevocationEpoch ?? options.ExpectedEpoch;
        return options.ExpectedGeneration is <= 0 || epoch is <= 0
            || options.ExpectedRegistryRevision is <= 0 || options.ExpectedBarrierEpoch is <= 0
            ? "Expected generation and epoch values must be positive integers."
            : null;
    }

    private static bool TryValidateRelayConfiguration(
        BadgeSetupConfiguration configuration,
        string? requestedAlias,
        out string error)
    {
        if (!string.Equals(configuration.Mode, BadgeSetupMode.Relay.ToWireValue(), StringComparison.Ordinal))
        {
            error = "Lifecycle operations require a relay-mode setup configuration.";
            return false;
        }

        if (!string.Equals(configuration.SchemaId, BadgeSetupContract.SchemaId, StringComparison.Ordinal)
            || !string.Equals(configuration.ContractVersion, BadgeSetupContract.ContractVersion, StringComparison.Ordinal)
            || !string.Equals(configuration.Bundle, BadgeSetupContract.Bundle, StringComparison.Ordinal)
            || !string.Equals(configuration.CompatibilityPlan, BadgeSetupContract.CompatibilityPlan, StringComparison.Ordinal)
            || configuration.DisclosureProfile is not (BadgeSetupContract.HeadlineOnlyProfile or BadgeSetupContract.HeadlinePlusFreshnessProfile)
            || configuration.Repository.RepositoryId is not > 0
            || configuration.Repository.RepositoryOwnerId is not > 0
            || configuration.Destination.Alias is null
            || !BadgeSetupValidationHelpers.IsSafeAudience(configuration.Destination.Audience)
            || !BadgeSetupValidationHelpers.IsSafeRef(configuration.BaseRef)
            || !BadgeSetupValidationHelpers.AreTrustedPublisherPins(configuration.Pins)
            || configuration.Pins?.BundleDigest is not null && !BadgeSetupValidationHelpers.IsSha(configuration.Pins.BundleDigest, 64))
        {
            error = "The Relay setup configuration does not satisfy the v1 semantic contract.";
            return false;
        }

        if (!BadgeSetupValidationHelpers.IsHttpsOrigin(configuration.Destination.Endpoint))
        {
            error = "The relay endpoint must be an HTTPS origin without query or fragment components.";
            return false;
        }

        string? alias = requestedAlias ?? configuration.Destination.Alias;
        if (!BadgeSetupValidationHelpers.IsOpaqueAlias(alias))
        {
            error = "The relay alias must be an approved opaque alias.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateOperatorOrigin(
        BadgeSetupConfiguration configuration,
        string? configuredOrigin,
        out string error,
        out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;
        if (!BadgeSetupValidationHelpers.IsHttpsOrigin(configuredOrigin))
        {
            error = $"Set {AdminOriginEnvironmentVariable} to the operator-controlled HTTPS Relay origin.";
            return false;
        }

        Uri configured = new(configuredOrigin!.TrimEnd('/'), UriKind.Absolute);
        Uri repositoryEndpoint = new(configuration.Destination.Endpoint!.TrimEnd('/'), UriKind.Absolute);
        string configuredAuthority = configured.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        string repositoryAuthority = repositoryEndpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        if (!string.Equals(configuredAuthority, repositoryAuthority, StringComparison.OrdinalIgnoreCase))
        {
            error = $"{AdminOriginEnvironmentVariable} must exactly match the checked-in Relay destination origin.";
            return false;
        }

        normalizedOrigin = configuredAuthority;
        error = string.Empty;
        return true;
    }

    private static HttpRequestMessage BuildRequest(
        BadgeSetupConfiguration configuration,
        string relayOrigin,
        string alias,
        BadgeLifecycleCommandOptions options,
        string token)
    {
        string route = options.Operation switch
        {
            StatusOperation => $"{alias}/{StatusOperation}",
            "rename" => $"{alias}/reconcile-identity",
            "remove" => $"{alias}/uninstall",
            "recover" => $"{alias}/recover/open",
            "activate" => $"{alias}/upgrade/activate",
            _ => $"{alias}/{options.Operation}",
        };
        Uri uri = new(relayOrigin.TrimEnd('/') + "/badge-relay/v1/admin/" + route, UriKind.Absolute);
        bool isStatus = options.Operation.Equals(StatusOperation, StringComparison.Ordinal);
        Dictionary<string, object?> fields = new(StringComparer.Ordinal)
        {
            ["operation"] = options.Operation,
            ["operation_id"] = $"cli-{Guid.NewGuid():N}",
            ["confirm"] = options.ApproveWithdrawal || options.ApproveRecovery,
            ["expected_generation"] = options.ExpectedGeneration,
            ["expected_revocation_epoch"] = options.ExpectedRevocationEpoch ?? options.ExpectedEpoch,
            ["expected_registry_revision"] = options.ExpectedRegistryRevision,
            ["expected_barrier_epoch"] = options.ExpectedBarrierEpoch,
            ["repository_id"] = configuration.Repository.RepositoryId,
            ["repository_owner_id"] = configuration.Repository.RepositoryOwnerId,
            ["new_owner"] = options.NewOwner,
            ["new_repository"] = options.NewRepository,
            ["new_alias"] = options.NewAlias,
            ["owner"] = options.NewOwner,
            ["repository"] = options.NewRepository,
            ["workflow_sha"] = options.WorkflowSha,
            ["job_workflow_sha"] = options.WorkflowSha,
            ["job_workflow_ref"] = options.WorkflowRef ?? options.Manifest,
            ["audience"] = options.Audience,
            ["bundle"] = options.Bundle ?? configuration.Bundle,
            ["contract_version"] = options.ContractVersion ?? configuration.ContractVersion,
            ["compatibility_plan"] = options.CompatibilityPlan ?? configuration.CompatibilityPlan,
            ["bundle_digest"] = options.TargetDigest ?? configuration.Pins?.BundleDigest,
            ["to_bundle"] = options.Bundle ?? configuration.Bundle,
            ["manifest"] = options.Manifest,
        };

        // Nulls are omitted, both to keep the protocol bounded and to avoid
        // turning an omitted conditional into an explicit state reset.
        fields = fields.Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        string json = JsonSerializer.Serialize(fields);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
        {
            throw new InvalidOperationException("The lifecycle request is too large.");
        }

        HttpRequestMessage request = new(isStatus ? HttpMethod.Get : HttpMethod.Post, uri)
        {
            Content = isStatus ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        return request;
    }

    private static string ReadBoundedResponse(HttpResponseMessage response)
    {
        using Stream stream = response.Content.ReadAsStream();
        using MemoryStream buffer = new();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
        byte[] chunk = new byte[8192];
        int total = 0;
        while (true)
        {
            int read = stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, MaximumJsonBytes + 1 - total)), deadline.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaximumJsonBytes)
            {
                throw new InvalidOperationException("The Relay response is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private int WriteResponse(BadgeLifecycleCommandOptions options, string alias, HttpStatusCode statusCode, string responseJson)
    {
        bool success = (int)statusCode is >= 200 and < 300;
        if (!success)
        {
            string reason = ReadAllowedReason(responseJson) ?? "relay-request-failed";
            WriteFailure(options.Format, reason, "The Relay rejected the lifecycle operation.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        JsonElement? fixedStatus = ReadFixedStatus(responseJson);
        if (options.Format.Equals(HumanFormat, StringComparison.OrdinalIgnoreCase))
        {
            string state = fixedStatus is JsonElement status && status.TryGetProperty("state", out JsonElement stateValue)
                ? stateValue.GetString() ?? "ok"
                : "ok";
            _console.Out.WriteLine($"{options.Operation}: {state}");
            return CliExitCodes.Success;
        }

        Dictionary<string, object?> output = new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["operation"] = options.Operation,
            ["alias"] = alias,
        };
        if (fixedStatus is JsonElement statusElement)
        {
            foreach (JsonProperty property in statusElement.EnumerateObject())
            {
                output[property.Name] = property.Value.Clone();
            }
        }

        _console.Out.WriteLine(JsonSerializer.Serialize(output));
        return CliExitCodes.Success;
    }

    private void WriteDryRun(BadgeLifecycleCommandOptions options, BadgeSetupConfiguration configuration, string alias)
    {
        if (options.Format.Equals(HumanFormat, StringComparison.OrdinalIgnoreCase))
        {
            _console.Out.WriteLine($"dry-run: {options.Operation} {alias} ({configuration.Destination.Endpoint})");
            return;
        }

        _console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            dry_run = true,
            operation = options.Operation,
            alias,
            endpoint = configuration.Destination.Endpoint,
            expected_generation = options.ExpectedGeneration,
            expected_revocation_epoch = options.ExpectedRevocationEpoch ?? options.ExpectedEpoch,
            expected_registry_revision = options.ExpectedRegistryRevision,
            expected_barrier_epoch = options.ExpectedBarrierEpoch,
            new_owner = options.NewOwner,
            new_repository = options.NewRepository,
            new_alias = options.NewAlias,
            workflow_sha = options.WorkflowSha,
            audience = options.Audience,
            bundle = options.Bundle,
            contract_version = options.ContractVersion,
            compatibility_plan = options.CompatibilityPlan,
            target_digest = options.TargetDigest,
            workflow_ref = options.WorkflowRef,
            manifest = options.Manifest,
        }));
    }

    private void WriteFailure(string format, string reason, string message)
    {
        if (format.Equals(HumanFormat, StringComparison.OrdinalIgnoreCase))
        {
            _console.Error.WriteLine(message);
        }
        else
        {
            _console.Out.WriteLine(JsonSerializer.Serialize(new { ok = false, reason, message }));
        }
    }

    private static string? ReadAllowedReason(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                string? reason = value.GetString();
                return reason is not null && reason.Length <= 64 && reason.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
                    ? reason
                    : null;
            }
        }
        catch (JsonException)
        {
            // Generic failure below; provider response bodies never reach output.
        }

        return null;
    }

    private static JsonElement? ReadFixedStatus(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement candidate = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(StatusOperation, out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
        {
            candidate = nested;
        }

        if (candidate.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string[] names =
        [
            "state", "generation", "revocation_epoch", "registry_revision", "barrier_epoch", "profile",
            "bundle", "contract_version", "compatibility_plan", "verified_at", "valid_until", "tombstoned",
            "active_digest", "staged_digest", "previous_verified_digest", "display_owner", "display_repository",
            "last_operation", "last_reason", "updated_at",
        ];
        Dictionary<string, JsonElement> safe = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (candidate.TryGetProperty(name, out JsonElement value))
            {
                safe[name] = value.Clone();
            }
        }

        return JsonSerializer.SerializeToElement(safe);
    }
}
