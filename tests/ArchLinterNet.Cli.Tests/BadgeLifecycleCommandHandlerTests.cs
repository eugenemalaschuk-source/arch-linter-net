using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
[NonParallelizable]
public sealed class BadgeLifecycleCommandHandlerTests
{
    [Test]
    public void DryRunDoesNotCreateHttpClientOrReadAdminToken()
    {
        RecordingConsole console = new();
        bool tokenRead = false;
        bool clientCreated = false;
        BadgeLifecycleCommandHandler handler = Handler(console, out MemoryFileSystem fileSystem,
            () => { clientCreated = true; return new HttpClient(); },
            () => { tokenRead = true; return "should-not-be-read"; });

        int result = handler.Execute(Options(fileSystem, DryRun: true));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("\"dry_run\":true"));
            Assert.That(tokenRead, Is.False);
            Assert.That(clientCreated, Is.False);
        });
    }

    [Test]
    public void RealOperationUsesApprovedRouteAndRedactsResponse()
    {
        HttpRequestMessage? received = null;
        string? receivedBody = null;
        RecordingConsole console = new();
        BadgeLifecycleCommandHandler handler = Handler(console, out MemoryFileSystem fileSystem,
            () => new HttpClient(new CapturingHandler(request =>
            {
                received = request;
                receivedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse("{\"ok\":true,\"status\":{\"state\":\"ready\",\"generation\":4,\"revocation_epoch\":7,\"payload\":\"PRIVATE-PAYLOAD\",\"source\":\"https://private.example/run\"},\"provider_body\":\"PRIVATE\"}");
            })),
            () => "admin-secret");

        int result = handler.Execute(Options(fileSystem, Operation: "rotate", ExpectedGeneration: 4, ExpectedRevocationEpoch: 7,
            WorkflowSha: new string('a', 40), Audience: "arch-relay/v1", Format: "json"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(received.RequestUri!.AbsoluteUri, Is.EqualTo("https://relay.example/badge-relay/v1/admin/aabc1234/rotate"));
            Assert.That(received.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(received.Headers.Authorization.Parameter, Is.EqualTo("admin-secret"));
            Assert.That(receivedBody, Does.Contain("\"operation\":\"rotate\""));
            Assert.That(receivedBody, Does.Contain("\"expected_generation\":4"));
            Assert.That(receivedBody, Does.Contain("\"expected_revocation_epoch\":7"));
            Assert.That(console.Output, Does.Contain("\"state\":\"ready\""));
            Assert.That(console.Output, Does.Not.Contain("PRIVATE-PAYLOAD"));
            Assert.That(console.Output, Does.Not.Contain("private.example"));
            Assert.That(console.Output, Does.Not.Contain("provider_body"));
            Assert.That(console.Output, Does.Not.Contain("admin-secret"));
        });
    }

    [Test]
    public void StatusUsesReadOnlyGetRoute()
    {
        HttpRequestMessage? received = null;
        RecordingConsole console = new();
        BadgeLifecycleCommandHandler handler = Handler(console, out MemoryFileSystem fileSystem,
            () => new HttpClient(new CapturingHandler(request =>
            {
                received = request;
                return JsonResponse("{\"state\":\"unavailable\",\"generation\":1}");
            })),
            () => "admin-secret");

        int result = handler.Execute(Options(fileSystem, Operation: "status"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(received!.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(received.RequestUri!.AbsolutePath, Is.EqualTo("/badge-relay/v1/admin/aabc1234/status"));
        });
    }

    [Test]
    public void MissingAdminTokenDoesNotCreateHttpClient()
    {
        RecordingConsole console = new();
        bool clientCreated = false;
        BadgeLifecycleCommandHandler handler = Handler(console, out MemoryFileSystem fileSystem,
            () => { clientCreated = true; return new HttpClient(); },
            () => null);

        int result = handler.Execute(Options(fileSystem));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain("missing-admin-token"));
            Assert.That(console.Output, Does.Not.Contain("admin-secret"));
            Assert.That(clientCreated, Is.False);
        });
    }

    [TestCase("revoke")]
    [TestCase("transfer")]
    [TestCase("remove")]
    [TestCase("recover")]
    public void DestructiveOperationRequiresExplicitApproval(string operation)
    {
        RecordingConsole console = new();
        BadgeLifecycleCommandHandler handler = Handler(console, out MemoryFileSystem fileSystem,
            () => throw new AssertionException("HTTP must not be created"),
            () => "token");

        int result = handler.Execute(Options(fileSystem, Operation: operation));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.Output, Does.Contain(operation is "recover" ? "approve-recovery" : "approve-withdrawal"));
    }

    [TestCase("none", "https://relay.example", "aabc1234")]
    [TestCase("relay", "http://relay.example", "aabc1234")]
    [TestCase("relay", "https://relay.example/path", "aabc1234")]
    [TestCase("relay", "https://relay.example", "public")]
    public void LifecycleRequiresRelayHttpsOriginAndOpaqueAlias(string mode, string endpoint, string alias)
    {
        RecordingConsole console = new();
        BadgeSetupConfiguration configuration = Configuration(mode, endpoint, alias);
        MemoryFileSystem fileSystem = new(("badge-relay-config.json", JsonSerializer.Serialize(configuration)));
        BadgeLifecycleCommandHandler handler = new(console, fileSystem, () => throw new AssertionException("HTTP must not be created"), () => "token");

        int result = handler.Execute(Options(fileSystem));

        Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        Assert.That(console.Output, Does.Contain("invalid-configuration"));
    }

    private static BadgeLifecycleCommandHandler Handler(
        RecordingConsole console,
        out MemoryFileSystem fileSystem,
        Func<HttpClient> clientFactory,
        Func<string?> tokenProvider)
    {
        fileSystem = new(("badge-relay-config.json", JsonSerializer.Serialize(Configuration())));
        return new(console, fileSystem, clientFactory, tokenProvider);
    }

    private static BadgeLifecycleCommandOptions Options(
        MemoryFileSystem _,
        string Operation = "status",
        bool DryRun = false,
        string Format = "json",
        long? ExpectedGeneration = null,
        long? ExpectedRevocationEpoch = null,
        string? WorkflowSha = null,
        string? Audience = null) => new(
        Operation,
        "badge-relay-config.json",
        null,
        DryRun,
        Format,
        false,
        ExpectedGeneration,
        ExpectedRevocationEpoch,
        null,
        null,
        null,
        null,
        null,
        WorkflowSha,
        Audience);

    private static BadgeSetupConfiguration Configuration(
        string mode = "relay",
        string endpoint = "https://relay.example",
        string alias = "aabc1234") => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        mode,
        BadgeSetupContract.HeadlineOnlyProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "public", 101, 202),
        new(alias, "0123456789abcdef0123456789abcdef", endpoint, "arch-relay/v1"),
        new(false, 1440, 60),
        new("owner/repo/.github/workflows/publish.yml", new string('a', 40), "owner/repo/.github/actions/action@" + new string('a', 40)),
        ManagedFiles: ["badge-relay-config.json"],
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        Producer: new(".github/workflows/producer.yml", new string('b', 40), "job", "check", "github-actions", "pull_request", "artifact", "evidence", "badge.json"),
        DisclosureApproved: true,
        ProviderPlan: "free");

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();
        public TextWriter Out => _output;
        public TextWriter Error => _error;
        public string Output => _output.ToString();
        public string ErrorText => _error.ToString();
    }

    private sealed class MemoryFileSystem(params (string Path, string Contents)[] files) : IFileSystem
    {
        private readonly Dictionary<string, string> _files = files.ToDictionary(static file => file.Path, static file => file.Contents, StringComparer.Ordinal);
        public bool FileExists(string path) => _files.ContainsKey(path);
        public string ReadAllText(string path) => _files[path];
        public void WriteAllText(string path, string contents) => _files[path] = contents;
        public string WriteAllTextToTemp(string targetPath, string contents) => targetPath;
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) => _files.Remove(path);
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
