using System.Text.Json;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
[NonParallelizable]
public sealed class BadgeSetupRenewalRenderingTests
{
    [Test]
    public void DisabledRenewalOmitsWorkflowAllowlistAndManagedPath()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440);
        BadgeSetupPlanResult result = BuildPlan(configuration);
        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);

            using JsonDocument generatedConfiguration = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(directory, "badge-relay-config.json")));
            string[] managedFiles = generatedConfiguration.RootElement
                .GetProperty("managed_files")
                .EnumerateArray()
                .Select(static item => item.GetString() ?? string.Empty)
                .ToArray();
            string[] permittedEvents = ReadPermittedEvents(directory);
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(directory, ".github", "workflows", "architecture-health-badge-renewal.yml")), Is.False);
                Assert.That(managedFiles, Does.Not.Contain(".github/workflows/architecture-health-badge-renewal.yml"));
                Assert.That(permittedEvents, Does.Contain("push"));
                Assert.That(permittedEvents, Does.Not.Contain("schedule"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void NinetyMinuteRenewalRendersExactBoundedSchedule()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 90);
        BadgeSetupPlanResult result = BuildPlan(configuration);
        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);
            string workflow = File.ReadAllText(Path.Combine(directory, ".github", "workflows", "architecture-health-badge-renewal.yml"));
            string[] cronEntries = CronEntries(workflow);
            Assert.Multiple(() =>
            {
                Assert.That(cronEntries, Has.Length.EqualTo(2));
                Assert.That(cronEntries, Does.Contain("    - cron: \"0 0,3,6,9,12,15,18,21 * * *\""));
                Assert.That(cronEntries, Does.Contain("    - cron: \"30 1,4,7,10,13,16,19,22 * * *\""));
                Assert.That(CountCronSlots(cronEntries), Is.EqualTo(result.Plan.Cost.JobsPerDay));
                Assert.That(workflow, Does.Not.Contain("0 * * * *"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void TwoHourRenewalRendersOnlyEvenUtcHours()
    {
        string directory = TemporaryDirectory();
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 120);
        BadgeSetupPlanResult result = BuildPlan(configuration);
        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);
            string workflow = File.ReadAllText(Path.Combine(directory, ".github", "workflows", "architecture-health-badge-renewal.yml"));
            string[] cronEntries = CronEntries(workflow);
            Assert.Multiple(() =>
            {
                Assert.That(cronEntries, Has.Length.EqualTo(1));
                Assert.That(cronEntries[0], Is.EqualTo("    - cron: \"0 0,2,4,6,8,10,12,14,16,18,20,22 * * *\""));
                Assert.That(CountCronSlots(cronEntries), Is.EqualTo(result.Plan.Cost.JobsPerDay));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string[] ReadPermittedEvents(string directory)
    {
        using JsonDocument wrangler = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory, "relay", "wrangler.jsonc")));
        string registry = wrangler.RootElement.GetProperty("vars").GetProperty("RELAY_REGISTRY").GetString()
            ?? throw new AssertionException("Generated Relay registry is missing.");
        using JsonDocument registryDocument = JsonDocument.Parse(registry);
        return registryDocument.RootElement
            .GetProperty("registry_entry")
            .GetProperty("permitted_events")
            .EnumerateArray()
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();
    }

    private static string[] CronEntries(string workflow) => workflow
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(static line => line.Contains("- cron:", StringComparison.Ordinal))
        .ToArray();

    private static int CountCronSlots(IEnumerable<string> entries) => entries.Sum(static entry =>
    {
        int firstQuote = entry.IndexOf('"');
        int lastQuote = entry.LastIndexOf('"');
        string[] fields = entry[(firstQuote + 1)..lastQuote].Split(' ');
        return fields[0].Split(',').Length * fields[1].Split(',').Length;
    });

    private static BadgeSetupPlanResult BuildPlan(BadgeSetupConfiguration configuration) => BadgeSetupEngine.BuildPlan(
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
                ProviderQuotaAvailable: true)));

    private static BadgeSetupConfiguration RelayConfiguration(bool renewalEnabled, int cadenceMinutes) => new(
        BadgeSetupContract.SchemaId,
        BadgeSetupContract.ContractVersion,
        "relay",
        BadgeSetupContract.HeadlinePlusFreshnessProfile,
        BadgeSetupContract.Bundle,
        BadgeSetupContract.CompatibilityPlan,
        new("owner", "repo", "private", 123, 456),
        new("a7f4k2m9", "0123456789abcdef0123456789abcdef", "https://relay.example", "architecture-health-badge-relay/a7f4k2m9"),
        new(renewalEnabled, cadenceMinutes, 60),
        new(BadgeSetupContract.DefaultPublisherWorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowSha, BadgeSetupContract.DefaultActionRef),
        BaseRef: "main",
        Project: new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        ProviderPlan: "pro",
        DisclosureApproved: true);

    private static string TemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-renewal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
