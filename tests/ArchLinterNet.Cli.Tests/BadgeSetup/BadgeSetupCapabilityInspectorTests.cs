using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeSetupCapabilityInspectorTests
{
    [Test]
    public void LocalAndGithubRawModesUseOnlyTheirDeclaredLocalCapabilities()
    {
        BadgeSetupCapabilityInspectionResult local = BadgeSetupCapabilityInspector.Inspect(
            Configuration("none", "private"),
            Options(),
            new MemoryFileSystem());
        BadgeSetupCapabilityInspectionResult raw = BadgeSetupCapabilityInspector.Inspect(
            Configuration("github-raw", "public"),
            Options(),
            new MemoryFileSystem());
        BadgeSetupCapabilityInspectionResult unsupported = BadgeSetupCapabilityInspector.Inspect(
            Configuration("future", "public"),
            Options(),
            new MemoryFileSystem());

        Assert.Multiple(() =>
        {
            Assert.That(local.Diagnostics, Is.Empty);
            Assert.That(local.Repository.Capabilities.CapabilitySource, Is.EqualTo("local"));
            Assert.That(raw.Repository.Capabilities.CanUseGithubRaw, Is.True);
            Assert.That(raw.Repository.Capabilities.CapabilitySource, Is.EqualTo("repository-visibility"));
            Assert.That(unsupported.Diagnostics, Is.Empty);
            Assert.That(unsupported.Repository.Capabilities.CanUseGithubRaw, Is.False);
        });
    }

    [Test]
    public void RelayEvidenceMustBeFreshExactAndComplete()
    {
        BadgeSetupConfiguration configuration = Configuration("relay", "private");
        string valid = Evidence(configuration);
        MemoryFileSystem fileSystem = new(("capabilities.json", valid));

        BadgeSetupCapabilityInspectionResult accepted = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            fileSystem);
        BadgeSetupCapabilityInspectionResult unknown = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", valid[..^1] + ",\"unexpected\":true}")));
        BadgeSetupCapabilityInspectionResult duplicate = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", """{"schema_id":"badge-relay-capability-evidence/v1","schema_id":"duplicate"}""")));
        BadgeSetupCapabilityInspectionResult wrongIdentity = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", Evidence(configuration).Replace("\"repository_id\":123", "\"repository_id\":999", StringComparison.Ordinal))));
        BadgeSetupCapabilityInspectionResult stale = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", Evidence(configuration, DateTimeOffset.UtcNow.AddHours(-1)))));
        BadgeSetupCapabilityInspectionResult malformed = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", "{")));
        BadgeSetupCapabilityInspectionResult incomplete = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options("capabilities.json"),
            new MemoryFileSystem(("capabilities.json", """{"schema_id":"badge-relay-capability-evidence/v1"}""")));

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Diagnostics, Is.Empty);
            Assert.That(accepted.Repository.Capabilities.HasRequiredCheck, Is.True);
            Assert.That(accepted.Repository.Capabilities.RepositoryId, Is.EqualTo(123));
            Assert.That(unknown.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
            Assert.That(duplicate.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
            Assert.That(wrongIdentity.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
            Assert.That(stale.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
            Assert.That(malformed.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
            Assert.That(incomplete.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
        });
    }

    [Test]
    public void MissingLiveCredentialsFailsClosedWithoutNetworkAccess()
    {
        using EnvironmentScope scope = new(
            ("GITHUB_TOKEN", null),
            ("GH_TOKEN", null),
            ("CF_API_TOKEN", null),
            ("CLOUDFLARE_API_TOKEN", null),
            ("ACTIONS_ID_TOKEN_REQUEST_URL", null),
            ("ACTIONS_ID_TOKEN_REQUEST_TOKEN", null));

        BadgeSetupCapabilityInspectionResult result = BadgeSetupCapabilityInspector.Inspect(
            Configuration("relay", "private"),
            Options(),
            new MemoryFileSystem());

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Repository.Capabilities.CanUseRelay, Is.False);
            Assert.That(result.Repository.Capabilities.CapabilitySource, Is.EqualTo("missing-live-credentials"));
        });
    }

    [Test]
    public void LiveInspectionBindsRepositoryRulesProviderAndOidcClaims()
    {
        BadgeSetupConfiguration configuration = Configuration("relay", "private");
        using EnvironmentScope scope = LiveEnvironment();
        HttpClientFactory factory = new(configuration, useRulesetFallback: false, oidcEnvelope: true);

        BadgeSetupCapabilityInspectionResult result = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options(),
            new MemoryFileSystem(),
            factory.Create);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Repository.Capabilities.HasRequiredCheck, Is.True);
            Assert.That(result.Repository.Capabilities.HasRulesApi, Is.True);
            Assert.That(result.Repository.Capabilities.CanUseOidc, Is.True);
            Assert.That(result.Repository.Capabilities.CanUseRelay, Is.True);
            Assert.That(result.Repository.Capabilities.ProviderQuotaAvailable, Is.True);
            Assert.That(result.Repository.Capabilities.ProviderPlan, Is.EqualTo("pro"));
        });
    }

    [Test]
    public void LiveInspectionFallsBackToInheritedRulesetsAndRejectsBadIdentity()
    {
        BadgeSetupConfiguration configuration = Configuration("relay", "private");
        using EnvironmentScope scope = LiveEnvironment();
        HttpClientFactory fallback = new(configuration, useRulesetFallback: true);
        BadgeSetupCapabilityInspectionResult accepted = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options(),
            new MemoryFileSystem(),
            fallback.Create);

        HttpClientFactory wrongIdentity = new(configuration, useRulesetFallback: false, wrongRepositoryIdentity: true);
        BadgeSetupCapabilityInspectionResult rejected = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options(),
            new MemoryFileSystem(),
            wrongIdentity.Create);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Diagnostics, Is.Empty);
            Assert.That(accepted.Repository.Capabilities.HasRulesApi, Is.True);
            Assert.That(accepted.Repository.Capabilities.HasRequiredCheck, Is.True);
            Assert.That(rejected.Repository.Capabilities.HasRulesApi, Is.False);
            Assert.That(rejected.Repository.Capabilities.HasRequiredCheck, Is.False);
            Assert.That(rejected.Repository.Capabilities.CanUseRelay, Is.True);
        });
    }

    [Test]
    public void LiveInspectionHandlesProviderAndOidcFailuresAsUnavailable()
    {
        BadgeSetupConfiguration configuration = Configuration("relay", "private");
        using EnvironmentScope scope = LiveEnvironment();
        HttpClientFactory unavailable = new(configuration, useRulesetFallback: false, unavailableProvider: true, invalidOidc: true);

        BadgeSetupCapabilityInspectionResult result = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options(),
            new MemoryFileSystem(),
            unavailable.Create);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Repository.Capabilities.CanUseRelay, Is.False);
            Assert.That(result.Repository.Capabilities.CanUseOidc, Is.False);
            Assert.That(result.Repository.Capabilities.ProviderQuotaAvailable, Is.False);
            Assert.That(result.Repository.Capabilities.CapabilitySource, Is.EqualTo("live-github-provider-inspector/v1"));
        });
    }

    [Test]
    public void LiveInspectionFailsClosedWhenTheConfiguredBaseRefDoesNotExist()
    {
        BadgeSetupConfiguration configuration = Configuration("relay", "private");
        using EnvironmentScope scope = LiveEnvironment();
        HttpClientFactory missingBranch = new(configuration, useRulesetFallback: false, missingBranch: true);

        BadgeSetupCapabilityInspectionResult result = BadgeSetupCapabilityInspector.Inspect(
            configuration,
            Options(),
            new MemoryFileSystem(),
            missingBranch.Create);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Repository.Capabilities.HasRulesApi, Is.False);
            Assert.That(result.Repository.Capabilities.HasRequiredCheck, Is.False);
            Assert.That(result.Repository.Capabilities.CanUseRelay, Is.True);
        });
    }

    private static BadgeSetupCommandOptions Options(string? capabilityEvidencePath = null) => new(
        InputPath: null,
        OutputDirectory: null,
        Repository: null,
        Visibility: null,
        Mode: null,
        DisclosureProfile: null,
        Account: null,
        Alias: null,
        Endpoint: null,
        ProviderPlan: null,
        CadenceMinutes: null,
        MaxLeaseMinutes: null,
        RenewalEnabled: false,
        DryRun: true,
        PublicDiagnostics: false,
        Format: "json",
        ShowHelp: false,
        CapabilityEvidencePath: capabilityEvidencePath);

    private static BadgeSetupConfiguration Configuration(string mode, string visibility) => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        mode,
        BadgeSetupContract.HeadlineOnlyProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", visibility, 123, 456),
        mode == BadgeSetupMode.Relay.ToWireValue()
            ? new("a7f4k2m9", "0123456789abcdef0123456789abcdef", "https://relay.example", "consumer-badge/relay")
            : new(null),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        ProviderPlan: mode == BadgeSetupMode.Relay.ToWireValue() ? "pro" : null,
        DisclosureApproved: true);

    private static string Evidence(BadgeSetupConfiguration configuration, DateTimeOffset? observedAt = null) =>
        JsonSerializer.Serialize(new
        {
            schema_id = BadgeSetupContract.CapabilityEvidenceSchemaId,
            source = "live-github-provider-inspector/v1",
            observed_at = (observedAt ?? DateTimeOffset.UtcNow).ToString("O"),
            repository = $"{configuration.Repository.Owner}/{configuration.Repository.Name}",
            repository_id = configuration.Repository.RepositoryId,
            repository_owner_id = configuration.Repository.RepositoryOwnerId,
            visibility = configuration.Repository.Visibility,
            base_ref = configuration.BaseRef,
            provider_plan = configuration.ProviderPlan,
            provider_account = configuration.Destination.Account,
            required_check = true,
            rules_api = true,
            oidc = true,
            provider_quota = true,
            relay = true,
        });

    private static EnvironmentScope LiveEnvironment() => new(
        ("GITHUB_TOKEN", "github-test-token"),
        ("GH_TOKEN", null),
        ("CF_API_TOKEN", "cloudflare-test-token"),
        ("CLOUDFLARE_API_TOKEN", null),
        ("ACTIONS_ID_TOKEN_REQUEST_URL", "https://actions.example/oidc"),
        ("ACTIONS_ID_TOKEN_REQUEST_TOKEN", "oidc-test-token"));

    private sealed class MemoryFileSystem(params (string Path, string Contents)[] files) : IFileSystem
    {
        private readonly Dictionary<string, string> _files = files.ToDictionary(
            static item => item.Path,
            static item => item.Contents,
            StringComparer.Ordinal);

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path) => _files[path];

        public void WriteAllText(string path, string contents) => _files[path] = contents;

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            _files[targetPath] = contents;
            return targetPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath) => _files[targetPath] = _files[tempPath];

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;

        public void DeleteFile(string path) => _files.Remove(path);

        public bool TryCreateNewFile(string path) => _files.TryAdd(path, string.Empty);

        public bool DirectoryExists(string path) => true;

        public void DeleteDirectoryIfEmpty(string path) { }

        public bool CanWriteToDirectory(string path) => true;
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvironmentScope(params (string Name, string? Value)[] values)
        {
            foreach ((string name, string? value) in values)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach ((string name, string? value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private sealed class HttpClientFactory(
        BadgeSetupConfiguration configuration,
        bool useRulesetFallback,
        bool wrongRepositoryIdentity = false,
        bool unavailableProvider = false,
        bool invalidOidc = false,
        bool missingBranch = false,
        bool oidcEnvelope = false)
    {
        public HttpClient Create(string baseAddress, string? token)
        {
            HttpClient client = new(new RoutingHandler(Respond))
            {
                BaseAddress = new Uri(baseAddress),
            };
            return client;
        }

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/repos/owner/repo")
            {
                return Json(wrongRepositoryIdentity
                    ? """{"id":999,"owner":{"id":456},"visibility":"private","default_branch":"main"}"""
                    : """{"id":123,"owner":{"id":456},"visibility":"private","default_branch":"main"}""");
            }

            if (path == "/repos/owner/repo/branches/main")
            {
                return missingBranch ? NotFound() : Json("""{"name":"main"}""");
            }

            if (path == "/repos/owner/repo/rules/branches/main")
            {
                return useRulesetFallback ? NotFound() : Json(RequiredCheckRules());
            }

            if (path == "/repos/owner/repo/rulesets")
            {
                return useRulesetFallback ? Json("""[{"id":0},{"id":42}]""") : NotFound();
            }

            if (path == "/repos/owner/repo/rulesets/42")
            {
                return Json(
                    """{"target":"branch","enforcement":"active","conditions":{"ref_name":{"include":["refs/heads/*"],"exclude":[]}},"rules":[{"type":"required_status_checks","parameters":{"strict_required_status_checks_policy":true,"required_status_checks":[{"context":"Architecture Coverage"}]}}]}""");
            }

            if (path == "/client/v4/accounts/0123456789abcdef0123456789abcdef")
            {
                return unavailableProvider
                    ? NotFound()
                    : Json("""{"result":{"id":"0123456789abcdef0123456789abcdef","plan":{"slug":"pro"}}}""");
            }

            if (path == "/client/v4/accounts/0123456789abcdef0123456789abcdef/workers/scripts"
                || path == "/client/v4/accounts/0123456789abcdef0123456789abcdef/workers/durable_objects/namespaces")
            {
                return unavailableProvider ? NotFound() : Json("""{"result":[]}""");
            }

            if (path == "/oidc")
            {
                if (invalidOidc)
                {
                    return Json("not-a-token");
                }

                return oidcEnvelope
                    ? Json(JsonSerializer.Serialize(new { value = CreateJwt(configuration) }))
                    : JsonToken(configuration);
            }

            return NotFound();
        }

        private static string RequiredCheckRules() =>
            """[{"type":"required_status_checks","parameters":{"strict_required_status_checks_policy":true,"required_status_checks":[{"context":"Architecture Coverage"}]}}]""";

        private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

        private static HttpResponseMessage JsonToken(BadgeSetupConfiguration configuration) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateJwt(configuration), Encoding.UTF8, "application/jwt"),
            };

        private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

        private static string CreateJwt(BadgeSetupConfiguration configuration)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string header = Encode(new { alg = "RS256", typ = "JWT" });
            string payload = Encode(new
            {
                iss = "https://token.actions.githubusercontent.com",
                aud = new[] { configuration.Destination.Audience, "other-audience" },
                repository_id = configuration.Repository.RepositoryId,
                repository_owner_id = configuration.Repository.RepositoryOwnerId,
                repository = "owner/repo",
                repository_visibility = "private",
                event_name = "push",
                @ref = "refs/heads/main",
                job_workflow_ref = $"{configuration.Pins!.WorkflowRef}@{configuration.Pins.WorkflowSha}",
                job_workflow_sha = configuration.Pins.WorkflowSha,
                sub = "repo:owner/repo:ref:refs/heads/main",
                iat = now - 1,
                exp = now + 300,
                nbf = now - 1,
            });
            return $"{header}.{payload}.signature";
        }

        private static string Encode<T>(T value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}
