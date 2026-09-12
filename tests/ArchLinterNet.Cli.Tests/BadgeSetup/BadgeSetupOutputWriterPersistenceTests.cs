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

    private static string TemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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
}
