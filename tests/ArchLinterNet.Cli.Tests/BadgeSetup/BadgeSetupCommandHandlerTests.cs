using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeSetupCommandHandlerTests
{
    [Test]
    public void SetupHelpAndMalformedRepositoryUseTheRequestedOutputFormat()
    {
        RecordingConsole helpConsole = new();
        int help = Handler(new MemoryFileSystem(), helpConsole).ExecuteSetup(Options(ShowHelp: true));

        RecordingConsole invalidConsole = new();
        int invalid = Handler(new MemoryFileSystem(), invalidConsole).ExecuteSetup(
            Options(Repository: "owner", Visibility: "private", Format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(help, Is.EqualTo(CliExitCodes.Success));
            Assert.That(helpConsole.Output, Does.Contain("badge architecture-health setup"));
            Assert.That(invalid, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(invalidConsole.Output, Does.Contain("unavailable"));
            Assert.That(invalidConsole.Output, Does.Contain(BadgeSetupDiagnosticCodes.MalformedIdentity));
            Assert.That(invalidConsole.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void SetupPreviewSupportsJsonAndHumanForPrivateNone()
    {
        RecordingConsole jsonConsole = new();
        int json = Handler(new MemoryFileSystem(), jsonConsole).ExecuteSetup(
            Options(Repository: "owner/repo", Visibility: "private"));
        RecordingConsole humanConsole = new();
        int human = Handler(new MemoryFileSystem(), humanConsole).ExecuteSetup(
            Options(Repository: "owner/repo", Visibility: "private", Format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo(CliExitCodes.Success));
            Assert.That(jsonConsole.Output, Does.Contain("\"IsValid\":true"));
            Assert.That(jsonConsole.Output, Does.Contain("\"Mode\":\"none\""));
            Assert.That(human, Is.EqualTo(CliExitCodes.Success));
            Assert.That(humanConsole.Output, Does.StartWith("ready: none / headline-only/v1"));
        });
    }

    [Test]
    public void NonDryRunRequiresOutputOnlyAfterAValidPlan()
    {
        RecordingConsole console = new();
        int result = Handler(new MemoryFileSystem(), console).ExecuteSetup(
            Options(Repository: "owner/repo", Visibility: "private", DryRun: false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorText, Does.Contain("requires --output"));
            Assert.That(console.Output, Does.Contain("\"IsValid\":true"));
        });
    }

    [Test]
    public void RelaySetupRequiresDisclosureApprovalBeforeWriting()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(disclosureApproved: false);
        string configJson = JsonSerializer.Serialize(configuration);
        string evidenceJson = CapabilityEvidence(configuration);
        RecordingConsole console = new();
        int result;
        using EnvironmentScope scope = WithoutLiveCredentials();
        result = Handler(
            new MemoryFileSystem(("config.json", configJson), ("capabilities.json", evidenceJson)),
            console).ExecuteSetup(Options(
                InputPath: "config.json",
                CapabilityEvidencePath: "capabilities.json",
                DryRun: false));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.Contain(BadgeSetupDiagnosticCodes.DisclosureApprovalRequired));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void InputConfigurationAndCapabilityEvidenceProduceReadyRelayPreview()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(disclosureApproved: true);
        RecordingConsole console = new();
        int result = Handler(
            new MemoryFileSystem(
                ("config.json", JsonSerializer.Serialize(configuration)),
                ("capabilities.json", CapabilityEvidence(configuration))),
            console).ExecuteSetup(Options(
                InputPath: "config.json",
                CapabilityEvidencePath: "capabilities.json"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("\"Mode\":\"relay\""));
            Assert.That(console.Output, Does.Contain("\"relay-plan\""));
            Assert.That(console.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void DoctorHandlesHelpMissingInputAndInvalidConfiguration()
    {
        RecordingConsole helpConsole = new();
        int help = Handler(new MemoryFileSystem(), helpConsole).ExecuteDoctor(Options(ShowHelp: true));
        RecordingConsole missingConsole = new();
        int missing = Handler(new MemoryFileSystem(), missingConsole).ExecuteDoctor(Options());
        RecordingConsole invalidConsole = new();
        int invalid = Handler(new MemoryFileSystem(("config.json", "{}")), invalidConsole).ExecuteDoctor(
            Options(InputPath: "config.json", PublicDiagnostics: true));

        Assert.Multiple(() =>
        {
            Assert.That(help, Is.EqualTo(CliExitCodes.Success));
            Assert.That(helpConsole.Output, Does.Contain("badge architecture-health doctor"));
            Assert.That(missing, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(missingConsole.ErrorText, Does.Contain("requires --input"));
            Assert.That(invalid, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(invalidConsole.Output, Does.Contain(BadgeSetupDiagnosticCodes.InvalidConfiguration));
            Assert.That(invalidConsole.ErrorText, Is.Empty);
        });
    }

    [Test]
    public void DoctorUsesLocalNoneDefaultsAndPublicJsonProjection()
    {
        BadgeSetupConfiguration configuration = NoneConfiguration();
        RecordingConsole humanConsole = new();
        int human = Handler(new MemoryFileSystem(("config.json", JsonSerializer.Serialize(configuration))), humanConsole)
            .ExecuteDoctor(Options(InputPath: "config.json", Format: "human"));
        RecordingConsole jsonConsole = new();
        int json = Handler(new MemoryFileSystem(("config.json", JsonSerializer.Serialize(configuration))), jsonConsole)
            .ExecuteDoctor(Options(InputPath: "config.json", PublicDiagnostics: true));

        Assert.Multiple(() =>
        {
            Assert.That(human, Is.EqualTo(CliExitCodes.Success));
            Assert.That(humanConsole.Output, Does.StartWith("available"));
            Assert.That(json, Is.EqualTo(CliExitCodes.Success));
            Assert.That(jsonConsole.Output, Does.Contain("\"Available\":true"));
            Assert.That(jsonConsole.Output, Does.Contain("\"Diagnostics\":[]"));
        });
    }

    [Test]
    public void DoctorAcceptsFreshRelayObservationAndRejectsMalformedObservation()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(disclosureApproved: true);
        string configJson = JsonSerializer.Serialize(configuration);
        string observationJson = DoctorObservation(configuration);
        RecordingConsole healthyConsole = new();
        int healthy = Handler(new MemoryFileSystem(
            ("config.json", configJson),
            ("observation.json", observationJson)), healthyConsole).ExecuteDoctor(
                Options(InputPath: "config.json", ObservationPath: "observation.json"));
        RecordingConsole invalidConsole = new();
        int invalid = Handler(new MemoryFileSystem(
            ("config.json", configJson),
            ("observation.json", "{}")), invalidConsole).ExecuteDoctor(
                Options(InputPath: "config.json", ObservationPath: "observation.json", Format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(healthy, Is.EqualTo(CliExitCodes.Success));
            Assert.That(healthyConsole.Output, Does.Contain("\"Available\":true"));
            Assert.That(invalid, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(invalidConsole.Output, Does.Contain("unavailable"));
            Assert.That(invalidConsole.Output, Does.Contain(BadgeSetupDiagnosticCodes.InvalidObservation));
        });
    }

    [Test]
    public void RelayDoctorWithoutObservationIsUnavailableWhenLiveCredentialsAreMissing()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(disclosureApproved: true);
        RecordingConsole console = new();
        using EnvironmentScope scope = WithoutLiveCredentials();
        int result = Handler(new MemoryFileSystem(("config.json", JsonSerializer.Serialize(configuration))), console)
            .ExecuteDoctor(Options(InputPath: "config.json", Format: "human"));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Does.StartWith("unavailable"));
            Assert.That(console.Output, Does.Contain(BadgeSetupDiagnosticCodes.MissingCapability));
            Assert.That(console.Output, Does.Contain(BadgeSetupDiagnosticCodes.FirstEvidenceMissing));
        });
    }

    private static BadgeSetupCommandHandler Handler(MemoryFileSystem fileSystem, RecordingConsole? console = null) =>
        new(console ?? new RecordingConsole(), fileSystem);

    private static BadgeSetupCommandOptions Options(
        string? InputPath = null,
        string? Repository = null,
        string? Visibility = null,
        string? Mode = null,
        string? OutputDirectory = null,
        string? CapabilityEvidencePath = null,
        string? ObservationPath = null,
        bool DryRun = true,
        bool PublicDiagnostics = false,
        string Format = "json",
        bool ShowHelp = false,
        bool ApproveDisclosure = false) => new(
        InputPath,
        OutputDirectory,
        Repository,
        Visibility,
        Mode,
        DisclosureProfile: null,
        Account: Mode == "relay" ? "0123456789abcdef0123456789abcdef" : null,
        Alias: Mode == "relay" ? "a7f4k2m9" : null,
        Endpoint: Mode == "relay" ? "https://relay.example" : null,
        ProviderPlan: Mode == "relay" ? "pro" : null,
        CadenceMinutes: Mode == "relay" ? 1440 : null,
        MaxLeaseMinutes: Mode == "relay" ? 60 : null,
        RenewalEnabled: false,
        DryRun,
        PublicDiagnostics,
        Format,
        ShowHelp,
        RepositoryId: Mode == "relay" ? 123 : null,
        RepositoryOwnerId: Mode == "relay" ? 456 : null,
        BaseRef: "main",
        PolicyPath: "architecture/dependencies.arch.yml",
        SolutionPath: "ArchLinterNet.slnx",
        CapabilityEvidencePath,
        ObservationPath,
        ApproveDisclosure);

    private static BadgeSetupConfiguration NoneConfiguration() => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.None.ToWireValue(),
        BadgeSetupContract.HeadlineOnlyProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private"),
        new(null),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        Producer: Producer(),
        DisclosureApproved: true);

    private static BadgeSetupConfiguration RelayConfiguration(bool disclosureApproved) => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.Relay.ToWireValue(),
        BadgeSetupContract.HeadlinePlusFreshnessProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private", 123, 456),
        new("a7f4k2m9", "0123456789abcdef0123456789abcdef", "https://relay.example", "architecture-health-badge-relay/a7f4k2m9"),
        new(false, 1440, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        Producer: Producer(),
        DisclosureApproved: disclosureApproved,
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

    private static string DoctorObservation(BadgeSetupConfiguration configuration) => JsonSerializer.Serialize(new
    {
        schema_id = BadgeSetupContract.DoctorObservationSchemaId,
        source = "live-doctor-inspector/v1",
        observed_at = DateTimeOffset.UtcNow.ToString("O"),
        repository = "owner/repo",
        repository_id = configuration.Repository.RepositoryId,
        repository_owner_id = configuration.Repository.RepositoryOwnerId,
        identity_valid = true,
        pins_valid = true,
        oidc_valid = true,
        required_check_available = true,
        rules_api_available = true,
        destination_reachable = true,
        first_evidence_available = true,
        artifact_valid = true,
        validity_current = true,
        destination_revoked = false,
        provider_quota_available = true,
        cache_fresh = true,
    });

    private static EnvironmentScope WithoutLiveCredentials() => new(
        ("GITHUB_TOKEN", null),
        ("GH_TOKEN", null),
        ("CF_API_TOKEN", null),
        ("CLOUDFLARE_API_TOKEN", null),
        ("ACTIONS_ID_TOKEN_REQUEST_URL", null),
        ("ACTIONS_ID_TOKEN_REQUEST_TOKEN", null));

    private sealed class RecordingConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        private readonly TextWriter _out;
        private readonly TextWriter _errorWriter;

        public RecordingConsole()
        {
            _out = new StringWriter(_output);
            _errorWriter = new StringWriter(_error);
        }

        public TextWriter Out => _out;
        public TextWriter Error => _errorWriter;
        public string Output => _output.ToString();
        public string ErrorText => _error.ToString();
    }

    private sealed class MemoryFileSystem(params (string Path, string Contents)[] files) : IFileSystem
    {
        private readonly Dictionary<string, string> _files = files.ToDictionary(
            static file => file.Path,
            static file => file.Contents,
            StringComparer.Ordinal);

        public bool FileExists(string path) => _files.ContainsKey(path);
        public string ReadAllText(string path) => _files.TryGetValue(path, out string? value)
            ? value
            : throw new FileNotFoundException(path);
        public void WriteAllText(string path, string contents) => _files[path] = contents;
        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            string temporaryPath = targetPath + ".tmp";
            _files[temporaryPath] = contents;
            return temporaryPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath) => _files[targetPath] = _files[tempPath];
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => _files.TryAdd(targetPath, _files[tempPath]);
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
}
