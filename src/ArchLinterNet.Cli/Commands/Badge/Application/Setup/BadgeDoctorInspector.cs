using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed record BadgeDoctorInspectionResult(
    BadgeSetupPlanResult Plan,
    BadgeDoctorObservations Observations);

internal static class BadgeDoctorInspector
{
    private const string GithubRawOrigin = "https://raw.githubusercontent.com";
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);

    internal static BadgeDoctorInspectionResult Inspect(
        BadgeSetupConfiguration configuration,
        BadgeSetupCommandOptions options,
        string configurationPath,
        IFileSystem fileSystem,
        Func<string, string?, HttpClient>? clientFactory = null)
    {
        BadgeSetupCapabilityInspectionResult capability = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            options,
            fileSystem,
            clientFactory);
        BadgeSetupConfiguration enriched = configuration with
        {
            Repository = configuration.Repository with
            {
                RepositoryId = configuration.Repository.RepositoryId ?? capability.Repository.Capabilities.RepositoryId,
                RepositoryOwnerId = configuration.Repository.RepositoryOwnerId ?? capability.Repository.Capabilities.RepositoryOwnerId,
            },
        };
        BadgeSetupRepositoryContext repository = capability.Repository with
        {
            Owner = enriched.Repository.Owner,
            Name = enriched.Repository.Name,
            Visibility = enriched.Repository.Visibility,
        };
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(enriched, repository);
        plan = AddDiagnostics(plan, capability.Diagnostics);

        BadgeDoctorObservations observations = BuildObservations(
            enriched,
            plan,
            capability.Repository.Capabilities,
            configurationPath,
            fileSystem,
            clientFactory);
        return new(plan, observations);
    }

    private static BadgeDoctorObservations BuildObservations(
        BadgeSetupConfiguration configuration,
        BadgeSetupPlanResult plan,
        BadgeSetupCapabilities capabilities,
        string configurationPath,
        IFileSystem fileSystem,
        Func<string, string?, HttpClient>? clientFactory)
    {
        bool none = configuration.Mode == BadgeSetupMode.None.ToWireValue();
        bool capabilityEvidenceValid = !plan.Diagnostics.Any(static diagnostic => diagnostic.Code == BadgeSetupDiagnosticCodes.InvalidObservation);
        bool identityValid = !HasDiagnostic(plan, BadgeSetupDiagnosticCodes.MalformedIdentity) && capabilityEvidenceValid;
        bool pinsValid = none || (!HasDiagnostic(plan, BadgeSetupDiagnosticCodes.InvalidPin) && HasGeneratedProducerPin(configuration, configurationPath, fileSystem));
        bool relay = configuration.Mode == BadgeSetupMode.Relay.ToWireValue();
        bool oidcValid = !relay || capabilityEvidenceValid && capabilities.CanUseOidc;
        bool requiredCheckAvailable = !relay || capabilityEvidenceValid && capabilities.HasRequiredCheck;
        bool rulesApiAvailable = !relay || capabilityEvidenceValid && capabilities.HasRulesApi;
        bool providerQuotaAvailable = !relay || capabilityEvidenceValid && capabilities.ProviderQuotaAvailable;

        BadgeDoctorArtifactInspection artifact = none || !plan.IsValid
            ? BadgeDoctorArtifactInspection.None
            : ProbeArtifact(configuration, clientFactory);

        return new(
            DestinationReachable: none || artifact.DestinationReachable,
            FirstEvidenceAvailable: none || artifact.FirstEvidenceAvailable,
            ArtifactValid: none || artifact.ArtifactValid,
            ValidityCurrent: none || artifact.ValidityCurrent,
            DestinationRevoked: artifact.DestinationRevoked,
            ProviderQuotaAvailable: providerQuotaAvailable,
            IdentityValid: identityValid,
            PinsValid: pinsValid,
            OidcValid: oidcValid,
            RequiredCheckAvailable: requiredCheckAvailable,
            RulesApiAvailable: rulesApiAvailable,
            CacheFresh: none || artifact.CacheFresh);
    }

    private static bool HasDiagnostic(BadgeSetupPlanResult plan, string code) =>
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == code);

    private static bool HasGeneratedProducerPin(
        BadgeSetupConfiguration configuration,
        string configurationPath,
        IFileSystem fileSystem)
    {
        BadgeSetupProducer? producer = configuration.Producer;
        if (producer is null)
        {
            return false;
        }

        try
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(configurationPath)) ?? Directory.GetCurrentDirectory();
            string path = Path.Combine(root, producer.WorkflowPath);
            if (!fileSystem.FileExists(path))
            {
                return false;
            }

            byte[] bytes = fileSystem.ReadAllBytes(path);
            string contents = Encoding.UTF8.GetString(bytes);
            return BadgeSetupOutputWriter.ComputeGitBlobSha(contents) == producer.WorkflowSha;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static BadgeDoctorArtifactInspection ProbeArtifact(
        BadgeSetupConfiguration configuration,
        Func<string, string?, HttpClient>? clientFactory)
    {
        string? artifactUrl = ArtifactUrl(configuration);
        if (artifactUrl is null || !Uri.TryCreate(artifactUrl, UriKind.Absolute, out Uri? uri))
        {
            return BadgeDoctorArtifactInspection.None;
        }

        try
        {
            using HttpClient client = (clientFactory ?? CreateClient)(uri.GetLeftPart(UriPartial.Authority), null);
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            bool cacheFresh = IsCacheFresh(response);
            if (response.StatusCode == HttpStatusCode.Gone)
            {
                return new(true, false, false, false, true, cacheFresh);
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                bool reachable = (int)response.StatusCode < 500;
                return new(reachable, false, false, false, false, cacheFresh);
            }

            byte[]? bytes = ReadBounded(response.Content);
            if (bytes is null)
            {
                return new(true, false, false, false, false, cacheFresh);
            }

            bool valid = ArchitectureHealthBadgeDisclosureValidator.TryValidate(configuration.DisclosureProfile, bytes, out _);
            bool current = valid && IsValidityCurrent(configuration.DisclosureProfile, bytes);
            return new(true, valid, valid, current, false, cacheFresh);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            return BadgeDoctorArtifactInspection.None;
        }
    }

    private static byte[]? ReadBounded(HttpContent content)
    {
        const int BufferSize = 4 * 1024;
        int maximum = ArchitectureHealthBadgeDisclosureValidator.MaximumPayloadBytes;
        if (content.Headers.ContentLength is long length && length > maximum)
        {
            return null;
        }

        using Stream stream = content.ReadAsStream();
        using MemoryStream buffer = new();
        byte[] chunk = new byte[BufferSize];
        int total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (read > maximum - total)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
            total += read;
        }

        return buffer.ToArray();
    }

    private static string? ArtifactUrl(BadgeSetupConfiguration configuration)
    {
        if (configuration.Mode == BadgeSetupMode.GithubRaw.ToWireValue())
        {
            return $"{GithubRawOrigin}/{configuration.Repository.Owner}/{configuration.Repository.Name}/{configuration.BaseRef}/architecture-health-badge/architecture-health.json";
        }

        if (configuration.Mode != BadgeSetupMode.Relay.ToWireValue()
            || configuration.Destination.Endpoint is not { } endpoint
            || configuration.Destination.Alias is not { } alias)
        {
            return null;
        }

        return $"{endpoint.TrimEnd('/')}/badge-relay/v1/{alias}.json";
    }

    private static bool IsValidityCurrent(string profile, byte[] bytes)
    {
        if (profile == BadgeSetupContract.HeadlineOnlyProfile)
        {
            return true;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            string? validUntil = document.RootElement.TryGetProperty("valid_until", out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
            return DateTimeOffset.TryParseExact(
                    validUntil,
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset timestamp)
                && timestamp > DateTimeOffset.UtcNow;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsCacheFresh(HttpResponseMessage response)
    {
        TimeSpan? maxAge = response.Headers.CacheControl?.MaxAge;
        TimeSpan? age = response.Headers.Age;
        return !maxAge.HasValue || !age.HasValue || age.Value <= maxAge.Value;
    }

    private static HttpClient CreateClient(string baseAddress, string? token) => new()
    {
        BaseAddress = new Uri(baseAddress),
        Timeout = _requestTimeout,
    };

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

    private sealed record BadgeDoctorArtifactInspection(
        bool DestinationReachable,
        bool FirstEvidenceAvailable,
        bool ArtifactValid,
        bool ValidityCurrent,
        bool DestinationRevoked,
        bool CacheFresh)
    {
        internal static BadgeDoctorArtifactInspection None { get; } = new(false, false, false, false, false, false);
    }
}
