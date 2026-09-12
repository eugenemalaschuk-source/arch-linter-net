using System.Text.Json;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
public sealed class BadgeSetupEngineTests
{
    [Test]
    public void PrivateRepositoryDefaultsToNoneWithoutExternalCalls()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(new("owner", "repo", "private", new())));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Plan.Mode, Is.EqualTo("none"));
            Assert.That(result.Plan.ExternalCallsExpected, Is.False);
            Assert.That(result.Plan.PublicEndpointExpected, Is.False);
            Assert.That(result.Plan.Prerequisites.Single().Satisfied, Is.True);
        });
    }

    [Test]
    public void PrivateGithubRawIsRejectedBeforeAnyPlanChanges()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(
                new("owner", "repo", "private", new(CanUseGithubRaw: true)),
                Mode: "github-raw"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("visibility-conflict"));
            Assert.That(result.Plan.PlannedChanges, Is.Empty);
        });
    }

    [Test]
    public void RelayReportsBoundedCostAndRequiredCapabilities()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(
                new(
                    "owner",
                    "repo",
                    "private",
                    new(HasRequiredCheck: true, HasRulesApi: true, CanUseOidc: true, CanUseRelay: true, ProviderPlan: "pro")),
                Mode: "relay",
                DisclosureProfile: "headline-plus-freshness/v1",
                DestinationAccount: "account",
                DestinationAlias: "a7f4k2m9",
                RenewalEnabled: true,
                RenewalCadenceMinutes: 30,
                MaxLeaseMinutes: 60));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Plan.Cost.JobsPerDay, Is.EqualTo(48));
            Assert.That(result.Plan.Cost.JobsPerThirtyDayMonth, Is.EqualTo(48 * 30));
            Assert.That(result.Plan.Cost.GithubPrivateMinutesPerDay, Is.EqualTo(48));
            Assert.That(result.Plan.PublicEndpointExpected, Is.True);
            Assert.That(result.Plan.PlannedChanges, Does.Contain("adopter-owned Relay destination registration"));
        });
    }

    [Test]
    public void RenewalBelowThirtyMinutesIsRejected()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(
                new("owner", "repo", "private", new(CanUseRelay: true, CanUseOidc: true, HasRequiredCheck: true, HasRulesApi: true, ProviderPlan: "pro")),
                Mode: "relay",
                DestinationAccount: "account",
                DestinationAlias: "a7f4k2m9",
                RenewalEnabled: true,
                RenewalCadenceMinutes: 15));

        Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-renewal"));
    }

    [Test]
    public void ParserRejectsUnknownSchemaAndDuplicateProperties()
    {
        BadgeSetupConfigurationParseResult unsupported = BadgeSetupConfigurationParser.Parse(
            "{\"schema_id\":\"badge-relay-config/v2\",\"contract_version\":\"v1\"}");
        BadgeSetupConfigurationParseResult duplicate = BadgeSetupConfigurationParser.Parse(
            "{\"schema_id\":\"badge-relay-config/v1\",\"schema_id\":\"other\"}");

        Assert.Multiple(() =>
        {
            Assert.That(unsupported.IsValid, Is.False);
            Assert.That(unsupported.Diagnostics, Is.Not.Empty);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Diagnostics, Is.Not.Empty);
        });
    }

    [Test]
    public void PublicDoctorOutputRedactsPrivateContextAndProviderResponse()
    {
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(new("owner", "repo", "private", new())));
        BadgeDoctorPublicReport report = BadgeSetupEngine.RunDoctor(
            plan,
            new(
                DestinationReachable: false,
                FirstEvidenceAvailable: false,
                ArtifactValid: false,
                ValidityCurrent: false,
                PrivateContext: "repo owner/repo sha abcdef123456",
                Token: "secret-token",
                RawProviderResponse: "https://private.example/runs/42"))
            .ToPublic();

        string output = JsonSerializer.Serialize(report);
        Assert.Multiple(() =>
        {
            Assert.That(report.Available, Is.False);
            Assert.That(output, Does.Not.Contain("secret-token"));
            Assert.That(output, Does.Not.Contain("owner/repo"));
            Assert.That(output, Does.Not.Contain("private.example"));
            Assert.That(output, Does.Contain("destination-unavailable"));
        });
    }

    [Test]
    public void OutputWriterIsIdempotentAndBindsManifestDigests()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-" + Guid.NewGuid().ToString("N"));
        BadgeSetupConfiguration configuration = new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            "none",
            BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new("owner", "repo", "private"),
            new(null),
            new(false, 1440, 60));
        BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(new BadgeSetupRequest(new("owner", "repo", "private", new()))).Plan;

        try
        {
            BadgeSetupOutputWriter.Write(directory, configuration, plan);
            string firstManifest = File.ReadAllText(Path.Combine(directory, "badge-relay-manifest.json"));
            BadgeSetupOutputWriter.Write(directory, configuration, plan);
            Assert.That(File.ReadAllText(Path.Combine(directory, "badge-relay-manifest.json")), Is.EqualTo(firstManifest));

            using JsonDocument manifest = JsonDocument.Parse(firstManifest);
            foreach (JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                string relativePath = file.GetProperty("path").GetString()!;
                string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, relativePath)))).ToLowerInvariant();
                Assert.That(digest, Is.EqualTo(file.GetProperty("sha256").GetString()), relativePath);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public void OutputWriterRefusesManagedFileConflict()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "README.badge.md"), "manual content");
        BadgeSetupConfiguration configuration = new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            "none",
            BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new("owner", "repo", "private"),
            new(null),
            new(false, 1440, 60));

        try
        {
            Assert.That(
                () => BadgeSetupOutputWriter.Write(directory, configuration, new(false, "none", "headline-only/v1", false, false, [], BadgeSetupCostEstimate.None, [], [])),
                Throws.TypeOf<IOException>());
            Assert.That(File.Exists(Path.Combine(directory, "badge-relay-config.json")), Is.False);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
