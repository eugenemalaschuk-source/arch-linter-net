using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeDoctorInspectorTests
{
    [Test]
    public void GithubRawHealthyArtifactIsInspectedWithoutAnObservationFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            BadgeSetupConfiguration configuration = WriteProducer(directory, GithubRawConfiguration());
            EndpointFactory factory = new(_ => Response(CanonicalHeadline()));

            BadgeDoctorInspectionResult inspected = BadgeDoctorInspector.Inspect(
                configuration,
                Options(),
                Path.Combine(directory, "badge-relay-config.json"),
                new FileSystem(),
                factory.Create);
            BadgeDoctorReport report = BadgeSetupEngine.RunDoctor(inspected.Plan, inspected.Observations);

            Assert.Multiple(() =>
            {
                Assert.That(report.Available, Is.True);
                Assert.That(inspected.Observations.DestinationReachable, Is.True);
                Assert.That(inspected.Observations.FirstEvidenceAvailable, Is.True);
                Assert.That(inspected.Observations.ArtifactValid, Is.True);
                Assert.That(inspected.Observations.PinsValid, Is.True);
                Assert.That(factory.Requests.Single().RequestUri!.AbsolutePath, Does.Contain("/architecture-health-badge/architecture-health.json"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void RelayFreshEvidenceIsInspectedAndCacheHeadersAreBounded()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            BadgeSetupConfiguration configuration = WriteProducer(directory, RelayConfiguration());
            string evidencePath = Path.Combine(directory, "capabilities.json");
            File.WriteAllText(evidencePath, CapabilityEvidence(configuration));
            DateTimeOffset validUntil = DateTimeOffset.UtcNow.AddMinutes(30);
            EndpointFactory factory = new(request =>
            {
                HttpResponseMessage response = Response(CanonicalFreshness(validUntil));
                response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };
                response.Headers.Age = TimeSpan.FromSeconds(30);
                return response;
            });

            BadgeDoctorInspectionResult inspected = BadgeDoctorInspector.Inspect(
                configuration,
                Options(evidencePath),
                Path.Combine(directory, "badge-relay-config.json"),
                new FileSystem(),
                factory.Create);
            BadgeDoctorReport report = BadgeSetupEngine.RunDoctor(inspected.Plan, inspected.Observations);

            Assert.Multiple(() =>
            {
                Assert.That(report.Available, Is.True);
                Assert.That(inspected.Observations.ValidityCurrent, Is.True);
                Assert.That(inspected.Observations.CacheFresh, Is.True);
                Assert.That(inspected.Observations.OidcValid, Is.True);
                Assert.That(inspected.Observations.RequiredCheckAvailable, Is.True);
                Assert.That(inspected.Observations.RulesApiAvailable, Is.True);
                Assert.That(inspected.Observations.ProviderQuotaAvailable, Is.True);
                Assert.That(factory.Requests.Single().RequestUri!.AbsolutePath, Is.EqualTo("/badge-relay/v1/a7f4k2m9.json"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void RelayUnavailableAndRevokedResponsesRemainUnavailable()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            BadgeSetupConfiguration configuration = WriteProducer(directory, RelayConfiguration());
            string evidencePath = Path.Combine(directory, "capabilities.json");
            File.WriteAllText(evidencePath, CapabilityEvidence(configuration));

            EndpointFactory unavailable = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            BadgeDoctorInspectionResult missing = BadgeDoctorInspector.Inspect(
                configuration,
                Options(evidencePath),
                Path.Combine(directory, "badge-relay-config.json"),
                new FileSystem(),
                unavailable.Create);

            EndpointFactory revoked = new(_ => new HttpResponseMessage(HttpStatusCode.Gone));
            BadgeDoctorInspectionResult revokedInspection = BadgeDoctorInspector.Inspect(
                configuration,
                Options(evidencePath),
                Path.Combine(directory, "badge-relay-config.json"),
                new FileSystem(),
                revoked.Create);

            Assert.Multiple(() =>
            {
                Assert.That(BadgeSetupEngine.RunDoctor(missing.Plan, missing.Observations).Available, Is.False);
                Assert.That(missing.Observations.FirstEvidenceAvailable, Is.False);
                Assert.That(missing.Observations.ArtifactValid, Is.False);
                Assert.That(revokedInspection.Observations.DestinationRevoked, Is.True);
                Assert.That(BadgeSetupEngine.RunDoctor(revokedInspection.Plan, revokedInspection.Observations).Available, Is.False);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ExpiredOrInvalidRelayArtifactFailsClosed()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            BadgeSetupConfiguration configuration = WriteProducer(directory, RelayConfiguration());
            string evidencePath = Path.Combine(directory, "capabilities.json");
            File.WriteAllText(evidencePath, CapabilityEvidence(configuration));
            EndpointFactory factory = new(_ => Response(CanonicalFreshness(DateTimeOffset.UtcNow.AddMinutes(-1))));

            BadgeDoctorInspectionResult inspected = BadgeDoctorInspector.Inspect(
                configuration,
                Options(evidencePath),
                Path.Combine(directory, "badge-relay-config.json"),
                new FileSystem(),
                factory.Create);

            Assert.Multiple(() =>
            {
                Assert.That(inspected.Observations.ArtifactValid, Is.True);
                Assert.That(inspected.Observations.FirstEvidenceAvailable, Is.True);
                Assert.That(inspected.Observations.ValidityCurrent, Is.False);
                Assert.That(BadgeSetupEngine.RunDoctor(inspected.Plan, inspected.Observations).Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.ValidityExpired));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private static BadgeSetupConfiguration GithubRawConfiguration() => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.GithubRaw.ToWireValue(),
        BadgeSetupContract.HeadlineOnlyProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "public"),
        new(null),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        Producer: Producer(),
        DisclosureApproved: true);

    private static BadgeSetupConfiguration RelayConfiguration() => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.Relay.ToWireValue(),
        BadgeSetupContract.HeadlinePlusFreshnessProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private", 123, 456),
        new("a7f4k2m9", "0123456789abcdef0123456789abcdef", "https://relay.example", "consumer-badge/relay"),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        Producer: Producer(),
        DisclosureApproved: true,
        ProviderPlan: "pro");

    private static BadgeSetupProducer Producer() => new(
        BadgeSetupContract.DefaultProducerWorkflowPath,
        new string('0', 40),
        BadgeSetupContract.DefaultCheckName,
        BadgeSetupContract.DefaultCheckName,
        BadgeSetupContract.DefaultCheckApp,
        "pull_request",
        BadgeSetupContract.DefaultArtifactName,
        BadgeSetupContract.DefaultEvidenceArtifactName,
        BadgeSetupContract.DefaultPayloadPath);

    private static BadgeSetupConfiguration WriteProducer(string directory, BadgeSetupConfiguration configuration)
    {
        string contents = "name: Architecture Coverage\non: pull_request\n";
        BadgeSetupProducer producer = configuration.Producer
            ?? throw new InvalidOperationException("Test configuration requires producer metadata.");
        producer = producer with { WorkflowSha = BadgeSetupOutputWriter.ComputeGitBlobSha(contents) };
        string path = Path.Combine(directory, producer.WorkflowPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? directory);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return configuration with { Producer = producer };
    }

    private static string CapabilityEvidence(BadgeSetupConfiguration configuration) => JsonSerializer.Serialize(new
    {
        schema_id = BadgeSetupContract.CapabilityEvidenceSchemaId,
        source = "live-github-provider-inspector/v1",
        observed_at = DateTimeOffset.UtcNow.ToString("O"),
        repository = "owner/repo",
        repository_id = 123,
        repository_owner_id = 456,
        visibility = "private",
        base_ref = configuration.BaseRef,
        provider_plan = configuration.ProviderPlan,
        provider_account = configuration.Destination.Account,
        required_check = true,
        rules_api = true,
        oidc = true,
        provider_quota = true,
        relay = true,
    });

    private static string CanonicalHeadline() =>
        """{"schemaVersion":1,"label":"architecture","message":"PASS \u00B7 HEALTHY \u00B7 0 ignores \u00B7 1 rules","color":"brightgreen"}""";

    private static string CanonicalFreshness(DateTimeOffset validUntil)
    {
        string verifiedAt = validUntil.ToUniversalTime().AddMinutes(-30).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        string expires = validUntil.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        return $"{{\"schemaVersion\":1,\"label\":\"architecture\",\"message\":\"PASS \\u00B7 HEALTHY \\u00B7 0 ignores \\u00B7 1 rules\",\"color\":\"brightgreen\",\"verified_at\":\"{verifiedAt}\",\"valid_until\":\"{expires}\"}}";
    }

    private static HttpResponseMessage Response(string contents) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(contents, Encoding.UTF8, "application/json"),
    };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class EndpointFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        internal List<HttpRequestMessage> Requests { get; } = [];

        internal HttpClient Create(string baseAddress, string? token)
        {
            return new(new RoutingHandler(request =>
            {
                Requests.Add(request);
                return responder(request);
            }))
            {
                BaseAddress = new Uri(baseAddress),
            };
        }
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
