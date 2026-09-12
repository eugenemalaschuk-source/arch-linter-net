using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed record BadgeSetupCapabilityInspectionResult(
    BadgeSetupRepositoryContext Repository,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics);

internal static class BadgeSetupCapabilityInspector
{
    private const string EvidenceSource = "live-github-provider-inspector/v1";
    private const string GitHubApi = "https://api.github.com";
    private const string CloudflareApi = "https://api.cloudflare.com/client/v4";
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _evidenceLifetime = TimeSpan.FromMinutes(15);

    internal static BadgeSetupCapabilityInspectionResult Inspect(
        BadgeSetupConfiguration configuration,
        BadgeSetupCommandOptions options,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (configuration.Mode == BadgeSetupMode.None.ToWireValue())
        {
            return Success(configuration, new(CapabilitySource: "local"));
        }

        if (configuration.Mode == BadgeSetupMode.GithubRaw.ToWireValue())
        {
            return Success(configuration, new(CanUseGithubRaw: configuration.Repository.Visibility == "public", CapabilitySource: "repository-visibility"));
        }

        if (configuration.Mode != BadgeSetupMode.Relay.ToWireValue())
        {
            return Success(configuration, new());
        }

        if (!string.IsNullOrWhiteSpace(options.CapabilityEvidencePath))
        {
            return ReadEvidence(configuration, fileSystem.ReadAllText(options.CapabilityEvidencePath!));
        }

        return InspectLive(configuration);
    }

    private static BadgeSetupCapabilityInspectionResult ReadEvidence(
        BadgeSetupConfiguration configuration,
        string source)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(source);
            JsonElement root = document.RootElement;
            HashSet<string> allowed = new(StringComparer.Ordinal)
            {
                "schema_id", "source", "observed_at", "repository", "repository_id", "repository_owner_id",
                "visibility", "base_ref", "provider_plan", "provider_account", "required_check", "rules_api",
                "oidc", "provider_quota", "relay",
            };
            HashSet<string> seen = new(StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Any(property => !allowed.Contains(property.Name) || !seen.Add(property.Name)))
            {
                return InvalidEvidence(configuration, "Capability evidence has an unsupported or duplicate shape.");
            }

            string schema = RequiredString(root, "schema_id");
            string evidenceSource = RequiredString(root, "source");
            string observedAtText = RequiredString(root, "observed_at");
            string repository = RequiredString(root, "repository");
            long repositoryId = RequiredPositiveLong(root, "repository_id");
            long repositoryOwnerId = RequiredPositiveLong(root, "repository_owner_id");
            string visibility = RequiredString(root, "visibility");
            string baseRef = RequiredString(root, "base_ref");
            string providerPlan = RequiredString(root, "provider_plan");
            string providerAccount = RequiredString(root, "provider_account");
            DateTimeOffset observedAt = DateTimeOffset.Parse(observedAtText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (schema != BadgeSetupContract.CapabilityEvidenceSchemaId
                || evidenceSource != EvidenceSource
                || observedAt < now - _evidenceLifetime
                || observedAt > now.AddMinutes(5)
                || repository != $"{configuration.Repository.Owner}/{configuration.Repository.Name}"
                || visibility != configuration.Repository.Visibility
                || baseRef != configuration.BaseRef
                || providerPlan != configuration.ProviderPlan
                || providerAccount != configuration.Destination.Account)
            {
                return InvalidEvidence(configuration, "Capability evidence is stale or contradicts the requested repository, plan, or account.");
            }

            BadgeSetupCapabilities capabilities = new(
                HasRequiredCheck: RequiredBoolean(root, "required_check"),
                HasRulesApi: RequiredBoolean(root, "rules_api"),
                CanUseOidc: RequiredBoolean(root, "oidc"),
                CanUseRelay: RequiredBoolean(root, "relay"),
                ProviderPlan: providerPlan,
                RepositoryId: repositoryId,
                RepositoryOwnerId: repositoryOwnerId,
                ProviderQuotaAvailable: RequiredBoolean(root, "provider_quota"),
                CapabilitySource: EvidenceSource,
                ObservedAt: observedAt);
            return Success(configuration, capabilities);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or OverflowException)
        {
            return InvalidEvidence(configuration, $"Capability evidence could not be parsed ({exception.GetType().Name}).");
        }
    }

    private static BadgeSetupCapabilityInspectionResult InspectLive(BadgeSetupConfiguration configuration)
    {
        string? githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GH_TOKEN");
        string? providerToken = Environment.GetEnvironmentVariable("CF_API_TOKEN")
            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN");
        if (string.IsNullOrWhiteSpace(githubToken) || string.IsNullOrWhiteSpace(providerToken))
        {
            return Success(configuration, new(
                RepositoryId: configuration.Repository.RepositoryId,
                RepositoryOwnerId: configuration.Repository.RepositoryOwnerId,
                CapabilitySource: "missing-live-credentials"));
        }

        try
        {
            using HttpClient github = CreateClient(GitHubApi, githubToken);
            using HttpClient cloudflare = CreateClient(CloudflareApi, providerToken);
            bool repositoryIdentity = TryInspectGitHub(
                github,
                configuration,
                out long repositoryId,
                out long repositoryOwnerId,
                out bool requiredCheck,
                out bool rulesApi);
            bool oidc = TryInspectOidc(configuration);
            bool provider = TryInspectProvider(
                cloudflare,
                configuration,
                out string? observedPlan,
                out bool providerQuota);
            BadgeSetupCapabilities capabilities = new(
                HasRequiredCheck: repositoryIdentity && requiredCheck,
                HasRulesApi: repositoryIdentity && rulesApi,
                CanUseOidc: oidc,
                CanUseRelay: provider && string.Equals(observedPlan, configuration.ProviderPlan, StringComparison.Ordinal),
                ProviderPlan: observedPlan,
                RepositoryId: repositoryId > 0 ? repositoryId : configuration.Repository.RepositoryId,
                RepositoryOwnerId: repositoryOwnerId > 0 ? repositoryOwnerId : configuration.Repository.RepositoryOwnerId,
                ProviderQuotaAvailable: providerQuota,
                CapabilitySource: EvidenceSource,
                ObservedAt: DateTimeOffset.UtcNow);
            return Success(configuration, capabilities);
        }
        catch (HttpRequestException)
        {
            return Success(configuration, new(
                RepositoryId: configuration.Repository.RepositoryId,
                RepositoryOwnerId: configuration.Repository.RepositoryOwnerId,
                CapabilitySource: "live-inspection-failed"));
        }
        catch (TaskCanceledException)
        {
            return Success(configuration, new(
                RepositoryId: configuration.Repository.RepositoryId,
                RepositoryOwnerId: configuration.Repository.RepositoryOwnerId,
                CapabilitySource: "live-inspection-timeout"));
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or OverflowException)
        {
            return InvalidEvidence(configuration, $"Live capability inspection returned an invalid response ({exception.GetType().Name}).");
        }
    }

    private static bool TryInspectGitHub(
        HttpClient client,
        BadgeSetupConfiguration configuration,
        out long repositoryId,
        out long repositoryOwnerId,
        out bool requiredCheck,
        out bool rulesApi)
    {
        repositoryId = 0;
        repositoryOwnerId = 0;
        requiredCheck = false;
        rulesApi = false;
        string repositoryPath = $"/repos/{Uri.EscapeDataString(configuration.Repository.Owner)}/{Uri.EscapeDataString(configuration.Repository.Name)}";
        using JsonDocument? repository = GetJson(client, repositoryPath);
        if (repository is null)
        {
            return false;
        }

        JsonElement root = repository.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        repositoryId = ReadPositiveLong(root, "id");
        if (root.TryGetProperty("owner", out JsonElement owner) && owner.ValueKind == JsonValueKind.Object)
        {
            repositoryOwnerId = ReadPositiveLong(owner, "id");
        }

        string visibility = root.TryGetProperty("visibility", out JsonElement visibilityElement)
            && visibilityElement.ValueKind == JsonValueKind.String
            ? visibilityElement.GetString() ?? string.Empty
            : root.TryGetProperty("private", out JsonElement privateElement) && privateElement.ValueKind == JsonValueKind.True
                ? "private"
                : "public";
        bool identity = repositoryId > 0
            && repositoryOwnerId > 0
            && visibility == configuration.Repository.Visibility;
        if (!identity)
        {
            return false;
        }

        using JsonDocument? branch = GetJson(client, $"{repositoryPath}/branches/{Uri.EscapeDataString(configuration.BaseRef)}");
        bool branchMatches = branch is not null
            && branch.RootElement.ValueKind == JsonValueKind.Object
            && branch.RootElement.TryGetProperty("name", out JsonElement branchName)
            && branchName.ValueKind == JsonValueKind.String
            && branchName.GetString() == configuration.BaseRef;
        if (!branchMatches)
        {
            return false;
        }

        using JsonDocument? rules = GetJson(client, $"{repositoryPath}/rules/branches/{Uri.EscapeDataString(configuration.BaseRef)}");
        if (rules is not null)
        {
            rulesApi = true;
            requiredCheck = ContainsRequiredCheck(rules.RootElement, configuration.Producer?.CheckName ?? BadgeSetupContract.DefaultCheckName);
        }
        else
        {
            using JsonDocument? rulesets = GetJson(client, $"{repositoryPath}/rulesets?includes_parents=true&includes_inherited=true&per_page=100");
            if (rulesets is not null && rulesets.RootElement.ValueKind == JsonValueKind.Array)
            {
                rulesApi = true;
                foreach (JsonElement summary in rulesets.RootElement.EnumerateArray())
                {
                    if (!summary.TryGetProperty("id", out JsonElement id)
                        || !id.TryGetInt64(out long rulesetId)
                        || rulesetId <= 0)
                    {
                        continue;
                    }

                    using JsonDocument? detail = GetJson(client, $"{repositoryPath}/rulesets/{rulesetId}");
                    if (detail is not null
                        && RulesetAppliesToBaseRef(detail.RootElement, configuration.BaseRef, root)
                        && ContainsRequiredCheck(detail.RootElement, configuration.Producer?.CheckName ?? BadgeSetupContract.DefaultCheckName))
                    {
                        requiredCheck = true;
                        break;
                    }
                }
            }
        }

        return true;
    }

    private static bool TryInspectOidc(BadgeSetupConfiguration configuration)
    {
        string? requestUrl = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_URL");
        string? requestToken = Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_TOKEN");
        if (string.IsNullOrWhiteSpace(requestUrl) || string.IsNullOrWhiteSpace(requestToken) || string.IsNullOrWhiteSpace(configuration.Destination.Audience))
        {
            return false;
        }

        try
        {
            using HttpClient client = new() { Timeout = _requestTimeout };
            using HttpRequestMessage request = new(HttpMethod.Get, AddQuery(requestUrl, "audience", configuration.Destination.Audience!));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jwt"));
            using HttpResponseMessage response = client.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string token = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return token.Length <= 16 * 1024 && HasExpectedOidcClaims(token, configuration);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private static bool TryInspectProvider(
        HttpClient client,
        BadgeSetupConfiguration configuration,
        out string? observedPlan,
        out bool quotaAvailable)
    {
        observedPlan = null;
        quotaAvailable = false;
        if (string.IsNullOrWhiteSpace(configuration.Destination.Account))
        {
            return false;
        }

        using JsonDocument? account = GetJson(client, $"/accounts/{Uri.EscapeDataString(configuration.Destination.Account)}");
        if (account is null)
        {
            return false;
        }

        JsonElement result = account.RootElement.TryGetProperty("result", out JsonElement resultElement)
            ? resultElement
            : default;
        string accountId = result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("id", out JsonElement id)
            && id.ValueKind == JsonValueKind.String
            ? id.GetString() ?? string.Empty
            : string.Empty;
        if (!string.Equals(accountId, configuration.Destination.Account, StringComparison.Ordinal))
        {
            return false;
        }

        if (result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("plan", out JsonElement plan)
            && plan.ValueKind == JsonValueKind.Object
            && plan.TryGetProperty("slug", out JsonElement slug)
            && slug.ValueKind == JsonValueKind.String)
        {
            observedPlan = slug.GetString();
        }

        using JsonDocument? workers = GetJson(client, $"/accounts/{Uri.EscapeDataString(configuration.Destination.Account)}/workers/scripts");
        using JsonDocument? durableObjects = GetJson(client, $"/accounts/{Uri.EscapeDataString(configuration.Destination.Account)}/workers/durable_objects/namespaces");
        quotaAvailable = workers is not null && durableObjects is not null;
        return observedPlan is not null;
    }

    private static bool ContainsRequiredCheck(JsonElement root, string checkName)
    {
        IEnumerable<JsonElement> candidates = root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("rules", out JsonElement rules) && rules.ValueKind == JsonValueKind.Array => rules.EnumerateArray(),
            _ => [],
        };
        foreach (JsonElement rule in candidates)
        {
            if (!rule.TryGetProperty("type", out JsonElement type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "required_status_checks")
            {
                continue;
            }

            if (!rule.TryGetProperty("parameters", out JsonElement parameters) || parameters.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!parameters.TryGetProperty("strict_required_status_checks_policy", out JsonElement strict)
                || strict.ValueKind != JsonValueKind.True)
            {
                continue;
            }

            if (parameters.TryGetProperty("required_status_checks", out JsonElement checks) && checks.ValueKind == JsonValueKind.Array
                && checks.EnumerateArray().Any(check => check.ValueKind == JsonValueKind.Object
                    && check.TryGetProperty("context", out JsonElement context)
                    && context.ValueKind == JsonValueKind.String
                    && context.GetString() == checkName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RulesetAppliesToBaseRef(JsonElement ruleset, string baseRef, JsonElement repository)
    {
        if (ruleset.ValueKind != JsonValueKind.Object
            || !StringPropertyEquals(ruleset, "target", "branch")
            || !StringPropertyEquals(ruleset, "enforcement", "active")
            || !ruleset.TryGetProperty("conditions", out JsonElement conditions)
            || conditions.ValueKind != JsonValueKind.Object
            || !conditions.TryGetProperty("ref_name", out JsonElement refName)
            || refName.ValueKind != JsonValueKind.Object
            || !TryGetStringArray(refName, "include", out string[] include)
            || !TryGetStringArray(refName, "exclude", out string[] exclude))
        {
            return false;
        }

        string? defaultBranch = repository.TryGetProperty("default_branch", out JsonElement defaultBranchElement)
            && defaultBranchElement.ValueKind == JsonValueKind.String
            ? defaultBranchElement.GetString()
            : null;
        string fullRef = $"refs/heads/{baseRef}";
        return include.Any(pattern => MatchesRefPattern(pattern, fullRef, baseRef, defaultBranch))
            && !exclude.Any(pattern => MatchesRefPattern(pattern, fullRef, baseRef, defaultBranch));
    }

    private static bool StringPropertyEquals(JsonElement root, string name, string expected) =>
        root.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() == expected;

    private static bool TryGetStringArray(JsonElement root, string name, out string[] values)
    {
        values = [];
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<string> result = [];
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                return false;
            }

            result.Add(item.GetString()!);
        }

        values = [.. result];
        return true;
    }

    private static bool MatchesRefPattern(string pattern, string fullRef, string baseRef, string? defaultBranch)
    {
        if (pattern == "~DEFAULT_BRANCH")
        {
            return defaultBranch == baseRef;
        }

        return GlobMatches(fullRef, pattern) || GlobMatches(baseRef, pattern);
    }

    private static bool GlobMatches(string value, string pattern)
    {
        string regex = "^(?:" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + ")$";
        return Regex.IsMatch(value, regex, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }

    private static HttpClient CreateClient(string baseAddress, string token)
    {
        HttpClient client = new() { BaseAddress = new Uri(baseAddress), Timeout = _requestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("arch-linter-net-badge-setup/0.8");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static JsonDocument? GetJson(HttpClient client, string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        using HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        return bytes.Length > 1_048_576 ? null : JsonDocument.Parse(bytes);
    }

    private static string AddQuery(string uri, string name, string value) =>
        uri + (uri.Contains("?", StringComparison.Ordinal) ? "&" : "?") + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value);

    private static bool HasExpectedOidcClaims(string response, BadgeSetupConfiguration configuration)
    {
        string? token = response.Trim();
        if (token.Count(static character => character == '.') != 2)
        {
            try
            {
                using JsonDocument envelope = JsonDocument.Parse(response);
                token = envelope.RootElement.ValueKind == JsonValueKind.Object
                    && envelope.RootElement.TryGetProperty("value", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024)
        {
            return false;
        }

        string[] segments = token.Split('.');
        if (segments.Length != 3 || segments.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        try
        {
            string encodedPayload = segments[1].Replace("-", "+", StringComparison.Ordinal).Replace("_", "/", StringComparison.Ordinal);
            encodedPayload += new string('=', (4 - encodedPayload.Length % 4) % 4);
            byte[] payload = Convert.FromBase64String(encodedPayload);
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement claims = document.RootElement;
            string expectedWorkflowRef = $"{configuration.Pins?.WorkflowRef}@{configuration.Pins?.WorkflowSha}";
            string expectedSubject = $"repo:{configuration.Repository.Owner}/{configuration.Repository.Name}:ref:refs/heads/{configuration.BaseRef}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return StringClaim(claims, "iss") == "https://token.actions.githubusercontent.com"
                && AudienceClaimMatches(claims, configuration.Destination.Audience)
                && PositiveClaim(claims, "repository_id") == configuration.Repository.RepositoryId
                && PositiveClaim(claims, "repository_owner_id") == configuration.Repository.RepositoryOwnerId
                && StringClaim(claims, "repository") == $"{configuration.Repository.Owner}/{configuration.Repository.Name}"
                && StringClaim(claims, "repository_visibility") == configuration.Repository.Visibility
                && StringClaim(claims, "event_name") == "push"
                && StringClaim(claims, "ref") == $"refs/heads/{configuration.BaseRef}"
                && StringClaim(claims, "job_workflow_ref") == expectedWorkflowRef
                && StringClaim(claims, "job_workflow_sha") == configuration.Pins?.WorkflowSha
                && StringClaim(claims, "sub") == expectedSubject
                && HasValidTimeClaims(claims, now);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasValidTimeClaims(JsonElement claims, long now)
    {
        long issued = IntegerClaim(claims, "iat");
        long expires = IntegerClaim(claims, "exp");
        long notBefore = IntegerClaim(claims, "nbf");
        return issued <= now + 300
            && notBefore <= now + 300
            && expires >= now - 300
            && expires > issued
            && expires - issued <= 600;
    }

    private static bool AudienceClaimMatches(JsonElement claims, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || !claims.TryGetProperty("aud", out JsonElement value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() == expected;
        }

        return value.ValueKind == JsonValueKind.Array
            && value.GetArrayLength() > 0
            && value.EnumerateArray().All(static item => item.ValueKind == JsonValueKind.String)
            && value.EnumerateArray().Any(item => item.GetString() == expected);
    }

    private static string? StringClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? PositiveClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) && parsed > 0
            ? parsed
            : null;

    private static long IntegerClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
            ? parsed
            : throw new InvalidOperationException($"Missing {name} claim.");

    private static BadgeSetupCapabilityInspectionResult Success(
        BadgeSetupConfiguration configuration,
        BadgeSetupCapabilities capabilities) => new(
        new(
            configuration.Repository.Owner,
            configuration.Repository.Name,
            configuration.Repository.Visibility,
            capabilities),
        []);

    private static BadgeSetupCapabilityInspectionResult InvalidEvidence(
        BadgeSetupConfiguration configuration,
        string detail) => new(
        new(
            configuration.Repository.Owner,
            configuration.Repository.Name,
            configuration.Repository.Visibility,
            new(
                ProviderPlan: configuration.ProviderPlan,
                RepositoryId: configuration.Repository.RepositoryId,
                RepositoryOwnerId: configuration.Repository.RepositoryOwnerId,
                CapabilitySource: "invalid-evidence")),
        [BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidObservation, privateDetail: detail)]);

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Missing {name}.");
        }

        return value.GetString()!;
    }

    private static long RequiredPositiveLong(JsonElement root, string name)
    {
        long value = root.TryGetProperty(name, out JsonElement element) && element.TryGetInt64(out long parsed) ? parsed : 0;
        return value is > 0 and <= 9_007_199_254_740_991
            ? value
            : throw new InvalidOperationException($"Invalid {name}.");
    }

    private static bool RequiredBoolean(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException($"Missing {name}.");
        }

        return element.GetBoolean();
    }

    private static long ReadPositiveLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) && parsed > 0 ? parsed : 0;
}
