using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeSetupOutputWriterPersistenceTests
{
    [Test]
    public void ExistingManagedFileConflictIsRejectedBeforeAnyWrite()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = NoneConfiguration();
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(new("owner", "repo", "private", new()))).Plan;
        try
        {
            File.WriteAllText(Path.Combine(directory, "badge-relay-config.json"), "manual content");

            Assert.Throws<IOException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.That(File.ReadAllText(Path.Combine(directory, "badge-relay-config.json")), Is.EqualTo("manual content"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void WriterRejectsCustomPublisherPinsEvenWhenPlanIsOtherwiseValid()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = NoneConfiguration() with
        {
            Pins = new(
                "attacker/repository/.github/workflows/publish.yml",
                new string('a', 40),
                "attacker/repository/.github/actions/publish@" + new string('a', 40)),
        };
        BadgeSetupPlan plan = new(
            IsValid: true,
            Mode: BadgeSetupMode.None.ToWireValue(),
            DisclosureProfile: BadgeSetupContract.HeadlineOnlyProfile,
            ExternalCallsExpected: false,
            PublicEndpointExpected: false,
            Prerequisites: [],
            Cost: BadgeSetupCostEstimate.None,
            PlannedChanges: [],
            Diagnostics: []);
        try
        {
            Assert.Throws<InvalidOperationException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.That(Directory.EnumerateFileSystemEntries(directory), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase("\"bundle\": \"badge-relay/v1\"", "\"bundle\": \"badge-relay/v2\"")]
    [TestCase("\"commit\": \"36c88c88cca708c10ade98ac1f1fee8c56c1cb30\"", "\"commit\": \"0000000000000000000000000000000000000000\"")]
    public void RelayBundleManifestTamperIsRejectedBeforeManagedWrites(string original, string replacement)
    {
        string directory = TemporaryDirectory();
        string manifestPath = Path.Combine(RepositoryRoot(), "relay", "bundle-manifest.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        BadgeSetupConfiguration configuration = RelayConfiguration();
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(
                HasRequiredCheck: true,
                HasRulesApi: true,
                CanUseOidc: true,
                CanUseRelay: true,
                ProviderPlan: "pro",
                RepositoryId: 123,
                RepositoryOwnerId: 456,
                ProviderQuotaAvailable: true))).Plan;
        try
        {
            string manifest = File.ReadAllText(manifestPath).Replace(original, replacement, StringComparison.Ordinal);
            File.WriteAllText(manifestPath, manifest);

            IOException? exception = Assert.Throws<IOException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("Local Relay bundle integrity validation failed"));
                Assert.That(Directory.EnumerateFileSystemEntries(directory), Is.Empty);
            });
        }
        finally
        {
            File.WriteAllBytes(manifestPath, manifestBytes);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void RelayBundleUnexpectedSourceFileIsRejectedBeforeManagedWrites()
    {
        string directory = TemporaryDirectory();
        string extraPath = Path.Combine(RepositoryRoot(), "relay", "src", "unexpected.ts");
        BadgeSetupConfiguration configuration = RelayConfiguration();
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(
                HasRequiredCheck: true,
                HasRulesApi: true,
                CanUseOidc: true,
                CanUseRelay: true,
                ProviderPlan: "pro",
                RepositoryId: 123,
                RepositoryOwnerId: 456,
                ProviderQuotaAvailable: true))).Plan;
        try
        {
            File.WriteAllText(extraPath, "export const unexpected = true;\n");

            IOException? exception = Assert.Throws<IOException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("unexpected or missing files"));
                Assert.That(Directory.EnumerateFileSystemEntries(directory), Is.Empty);
            });
        }
        finally
        {
            if (File.Exists(extraPath)) File.Delete(extraPath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void WriteFailureRestoresPreviouslyExistingFiles()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = RelayConfiguration();
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(
                HasRequiredCheck: true,
                HasRulesApi: true,
                CanUseOidc: true,
                CanUseRelay: true,
                ProviderPlan: "pro",
                RepositoryId: 123,
                RepositoryOwnerId: 456,
                ProviderQuotaAvailable: true))).Plan;
        try
        {
            BadgeSetupOutputWriter.Write(directory, configuration, plan);
            string originalConfig = File.ReadAllText(Path.Combine(directory, "badge-relay-config.json"));
            Assert.That(File.Exists(Path.Combine(directory, "relay", "THIRD-PARTY-NOTICES.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "relay", "bundle-manifest.json")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(directory, "relay", "THIRD-PARTY-NOTICES.txt")), Is.EqualTo(File.ReadAllText(Path.Combine(RepositoryRoot(), "relay", "THIRD-PARTY-NOTICES.txt"))));
            string packagePath = Path.Combine(directory, "relay", "package.json");
            File.Delete(packagePath);
            Directory.CreateDirectory(packagePath);

            Exception? caughtException = Assert.Catch<Exception>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.That(caughtException, Is.TypeOf<IOException>().Or.TypeOf<UnauthorizedAccessException>());
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(Path.Combine(directory, "badge-relay-config.json")), Is.EqualTo(originalConfig));
                Assert.That(Directory.Exists(packagePath), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void GeneratedRelayManifestDescribesRenderedFiles()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = RelayConfiguration();
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(
                HasRequiredCheck: true,
                HasRulesApi: true,
                CanUseOidc: true,
                CanUseRelay: true,
                ProviderPlan: "pro",
                RepositoryId: 123,
                RepositoryOwnerId: 456,
                ProviderQuotaAvailable: true))).Plan;
        try
        {
            BadgeSetupOutputWriter.Write(directory, configuration, plan);

            Assert.DoesNotThrow(() => BadgeRelayBundleIntegrityValidator.ValidateGenerated(
                Path.Combine(directory, "relay"), configuration));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SymlinkedGithubDirectoryIsRejectedBeforeManagedWrites()
    {
        string directory = TemporaryDirectory();
        string outside = TemporaryDirectory();
        string link = Path.Combine(directory, ".github");
        try
        {
            CreateDirectoryLinkOrIgnore(link, outside);
            BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
                new BadgeSetupRequest(new("owner", "repo", "private", new()))).Plan;

            Assert.Throws<IOException>(() => BadgeSetupOutputWriter.Write(directory, NoneConfiguration(), plan));
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(directory, "badge-relay-config.json")), Is.False);
                Assert.That(Directory.EnumerateFileSystemEntries(outside), Is.Empty);
            });
        }
        finally
        {
            DeleteTemporaryDirectory(directory, link);
            DeleteTemporaryDirectory(outside);
        }
    }

    [Test]
    public void SymlinkedRelayDirectoryIsRejectedBeforeManagedWrites()
    {
        string directory = TemporaryDirectory();
        string outside = TemporaryDirectory();
        string link = Path.Combine(directory, "relay");
        try
        {
            CreateDirectoryLinkOrIgnore(link, outside);
            BadgeSetupConfiguration configuration = RelayConfiguration();
            BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(
                configuration,
                new(
                    "owner",
                    "repo",
                    "private",
                    new(
                        HasRequiredCheck: true,
                        HasRulesApi: true,
                        CanUseOidc: true,
                        CanUseRelay: true,
                        ProviderPlan: "pro",
                        RepositoryId: 123,
                        RepositoryOwnerId: 456,
                        ProviderQuotaAvailable: true))).Plan;

            Assert.Throws<IOException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.That(Directory.EnumerateFileSystemEntries(outside), Is.Empty);
        }
        finally
        {
            DeleteTemporaryDirectory(directory, link);
            DeleteTemporaryDirectory(outside);
        }
    }

    private static string TemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? current = new(TestContext.CurrentContext.TestDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ArchLinterNet.slnx"))) return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static BadgeSetupConfiguration NoneConfiguration() => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        BadgeSetupMode.None.ToWireValue(),
        BadgeSetupContract.HeadlineOnlyProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private"),
        new(null),
        new(false, 1440, 60));

    private static BadgeSetupConfiguration RelayConfiguration() => new(
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
        DisclosureApproved: true,
        ProviderPlan: "pro");

    private static void CreateDirectoryLinkOrIgnore(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Ignore("Symbolic link creation is not permitted/supported in this environment.");
        }
    }

    private static void DeleteTemporaryDirectory(string directory, string? link = null)
    {
        if (link is not null)
        {
            try { Directory.Delete(link); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
