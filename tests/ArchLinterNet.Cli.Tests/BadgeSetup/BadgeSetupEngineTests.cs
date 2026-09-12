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
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 30);
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
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
            RelayConfiguration(renewalEnabled: true, cadenceMinutes: 15),
            new(
                "owner",
                "repo",
                "private",
                new(
                    CanUseRelay: true,
                    CanUseOidc: true,
                    HasRequiredCheck: true,
                    HasRulesApi: true,
                    ProviderPlan: "pro",
                    RepositoryId: 123,
                    RepositoryOwnerId: 456,
                    ProviderQuotaAvailable: true)));

        Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-renewal"));
    }

    [Test]
    public void ProviderPlanDeclarationDoesNotSatisfyRelayCapabilities()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440);
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(ProviderPlan: null, RepositoryId: 123, RepositoryOwnerId: 456)));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Plan.Prerequisites.Single(item => item.Code == "required-check").Satisfied, Is.False);
            Assert.That(result.Plan.Prerequisites.Single(item => item.Code == "rules-api").Satisfied, Is.False);
            Assert.That(result.Plan.Prerequisites.Single(item => item.Code == "oidc").Satisfied, Is.False);
            Assert.That(result.Plan.Prerequisites.Single(item => item.Code == "provider-quota").Satisfied, Is.False);
        });
    }

    [Test]
    public void RenewalAboveContractUpperBoundIsRejected()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            RelayConfiguration(renewalEnabled: true, cadenceMinutes: 1441),
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

        Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-renewal"));
    }

    [Test]
    public void DisabledRenewalStillUsesSchemaCadenceBounds()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1441);
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
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

        Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-renewal"));
    }

    [Test]
    public void HealthyDoctorObservationRemainsAvailable()
    {
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(
            RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440),
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

        BadgeDoctorReport report = BadgeSetupEngine.RunDoctor(plan, new());

        Assert.That(report.Available, Is.True);
        Assert.That(report.Diagnostics, Is.Empty);
    }

    [Test]
    public void HealthyDoctorObservationDocumentIsShapeAndFreshnessValidated()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440);
        string observation = JsonSerializer.Serialize(new
        {
            schema_id = BadgeSetupContract.DoctorObservationSchemaId,
            source = "live-doctor-inspector/v1",
            observed_at = DateTimeOffset.UtcNow.ToString("O"),
            repository = "owner/repo",
            repository_id = 123,
            repository_owner_id = 456,
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

        BadgeDoctorObservationParseResult parsed = BadgeDoctorObservationParser.Parse(observation, configuration);

        Assert.Multiple(() =>
        {
            Assert.That(parsed.IsValid, Is.True);
            Assert.That(parsed.Observations, Is.Not.Null);
            Assert.That(parsed.Observations!.FirstEvidenceAvailable, Is.True);
            Assert.That(parsed.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void HealthyGithubRawObservationCanOmitRelayOnlyImmutableIds()
    {
        BadgeSetupConfiguration configuration = new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            "github-raw",
            BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new("owner", "repo", "public"),
            new(null),
            new(false, 1440, 60));
        string observation = JsonSerializer.Serialize(new
        {
            schema_id = BadgeSetupContract.DoctorObservationSchemaId,
            source = "live-doctor-inspector/v1",
            observed_at = DateTimeOffset.UtcNow.ToString("O"),
            repository = "owner/repo",
            repository_id = (long?)null,
            repository_owner_id = (long?)null,
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

        BadgeDoctorObservationParseResult parsed = BadgeDoctorObservationParser.Parse(observation, configuration);
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "public", new(CanUseGithubRaw: true)));

        Assert.Multiple(() =>
        {
            Assert.That(parsed.IsValid, Is.True);
            Assert.That(plan.IsValid, Is.True);
            Assert.That(BadgeSetupEngine.RunDoctor(plan, parsed.Observations).Available, Is.True);
        });
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
    public void OutputWriterPreservesManualReadmeContentOutsideManagedRegion()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "README.md"), "manual content\n");
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
            BadgeSetupPlan plan = BadgeSetupEngine.BuildPlan(new BadgeSetupRequest(new("owner", "repo", "private", new()))).Plan;
            BadgeSetupOutputWriter.Write(directory, configuration, plan);
            string readme = File.ReadAllText(Path.Combine(directory, "README.md"));
            Assert.Multiple(() =>
            {
                Assert.That(readme, Does.StartWith("manual content"));
                Assert.That(readme, Does.Contain(BadgeSetupOutputWriter.ReadmeStartMarker));
                Assert.That(readme, Does.Contain(BadgeSetupOutputWriter.ReadmeEndMarker));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void OutputWriterUsesShieldsEndpointForGithubRawJson()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-raw-" + Guid.NewGuid().ToString("N"));
        BadgeSetupConfiguration configuration = new(
            BadgeSetupContract.SchemaId,
            BadgeSetupContract.ContractVersion,
            "github-raw",
            BadgeSetupContract.HeadlineOnlyProfile,
            BadgeSetupContract.Bundle,
            BadgeSetupContract.CompatibilityPlan,
            new("owner", "repo", "public", 123, 456),
            new(null),
            new(false, 1440, 60));
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "public", new(CanUseGithubRaw: true, RepositoryId: 123, RepositoryOwnerId: 456)));

        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);
            string readme = File.ReadAllText(Path.Combine(directory, "README.md"));
            Assert.Multiple(() =>
            {
                Assert.That(readme, Does.Contain("img.shields.io/endpoint?url="));
                Assert.That(readme, Does.Not.Contain("![Architecture Health](https://raw.githubusercontent.com/"));
                Assert.That(readme, Does.Contain("architecture-health-badge%2Farchitecture-health.json"));
            });
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
    public void OutputWriterRejectsInvalidPlanBeforeCreatingDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-invalid-" + Guid.NewGuid().ToString("N"));
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
        BadgeSetupPlan plan = new(false, "none", BadgeSetupContract.HeadlineOnlyProfile, false, false, [], BadgeSetupCostEstimate.None, [], []);

        try
        {
            Assert.Throws<InvalidOperationException>(() => BadgeSetupOutputWriter.Write(directory, configuration, plan));
            Assert.That(Directory.Exists(directory), Is.False);
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
    public void OutputWriterHonorsConfiguredBaseRefProducerAndAudience()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-setup-custom-" + Guid.NewGuid().ToString("N"));
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 30) with
        {
            BaseRef = "release",
            Project = new("configs/policy.yml", "src/Consumer.slnx"),
            Destination = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 30).Destination with
            {
                Audience = "consumer-badge/relay"
            },
            Producer = new(
                ".github/workflows/custom-badge.yml",
                new string('0', 40),
                "Custom Architecture Check",
                "Custom Architecture Check",
                "github-actions",
                "pull_request",
                "custom-badge",
                "custom-health",
                BadgeSetupContract.DefaultPayloadPath),
        };
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
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

        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);
            string producer = File.ReadAllText(Path.Combine(directory, ".github", "workflows", "custom-badge.yml"));
            string registry = File.ReadAllText(Path.Combine(directory, ".github", "badge-promotion", "registry.json"));
            string wrangler = File.ReadAllText(Path.Combine(directory, "relay", "wrangler.jsonc"));
            Assert.Multiple(() =>
            {
                Assert.That(producer, Does.Contain("- release"));
                Assert.That(producer, Does.Contain("configs/policy.yml"));
                Assert.That(producer, Does.Contain("src/Consumer.slnx"));
                Assert.That(producer, Does.Contain("Custom Architecture Check"));
                Assert.That(File.Exists(Path.Combine(directory, ".github", "workflows", "ci.yml")), Is.False);
                Assert.That(registry, Does.Contain("\"base_ref\": \"release\""));
                Assert.That(wrangler, Does.Contain("refs/heads/release"));
                Assert.That(wrangler, Does.Contain("consumer-badge/relay"));
            });
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
    public void SemanticValidationRejectsEndpointPinsAndManagedFileOverflow()
    {
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440) with
        {
            Destination = RelayConfiguration(renewalEnabled: false, cadenceMinutes: 1440).Destination with
            {
                Endpoint = "http://relay.example"
            },
            Pins = new(
                BadgeSetupContract.DefaultPublisherWorkflowRef,
                "not-a-sha",
                BadgeSetupContract.DefaultActionRef),
            ManagedFiles = Enumerable.Range(0, 33).Select(static index => $"managed/{index}.json").ToArray(),
        };
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-endpoint"));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-pin"));
            Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Code), Does.Contain("invalid-managed-path"));
        });
    }

    [Test]
    public void OutputWriterRendersAdopterBoundRelayAndExactProducerDigest()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-net-relay-" + Guid.NewGuid().ToString("N"));
        BadgeSetupConfiguration configuration = RelayConfiguration(renewalEnabled: true, cadenceMinutes: 30);
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
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

        try
        {
            Assert.That(result.IsValid, Is.True);
            BadgeSetupOutputWriter.Write(directory, configuration, result.Plan);
            string wrangler = File.ReadAllText(Path.Combine(directory, "relay", "wrangler.jsonc"));
            string registry = File.ReadAllText(Path.Combine(directory, ".github", "badge-promotion", "registry.json"));
            string producer = File.ReadAllText(Path.Combine(directory, ".github", "workflows", "architecture-health-badge-producer.yml"));
            using JsonDocument wranglerDocument = JsonDocument.Parse(wrangler);
            using JsonDocument registryDocument = JsonDocument.Parse(registry);
            using JsonDocument configDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "badge-relay-config.json")));
            string relayRegistry = wranglerDocument.RootElement.GetProperty("vars").GetProperty("RELAY_REGISTRY").GetString()!;
            using JsonDocument relayRegistryDocument = JsonDocument.Parse(relayRegistry);
            JsonElement entry = relayRegistryDocument.RootElement.GetProperty("registry_entry");
            string producerSha = configDocument.RootElement.GetProperty("producer").GetProperty("workflow_sha").GetString()!;
            string registrySha = registryDocument.RootElement.GetProperty("configurations").GetProperty("setup_generated").GetProperty("producer").GetProperty("workflow_sha").GetString()!;
            Assert.Multiple(() =>
            {
                Assert.That(wrangler, Does.Not.Contain("synthetic-owner-042"));
                Assert.That(wrangler, Does.Not.Contain("synthetic-repo-042"));
                Assert.That(entry.GetProperty("repository_id").GetInt64(), Is.EqualTo(123));
                Assert.That(entry.GetProperty("repository_owner_id").GetInt64(), Is.EqualTo(456));
                Assert.That(entry.GetProperty("owner").GetString(), Is.EqualTo("owner"));
                Assert.That(entry.GetProperty("repository").GetString(), Is.EqualTo("repo"));
                Assert.That(entry.GetProperty("destination_alias").GetString(), Is.EqualTo("a7f4k2m9"));
                Assert.That(entry.GetProperty("permitted_events").EnumerateArray().Select(static item => item.GetString()), Is.EquivalentTo(["push", "schedule"]));
                Assert.That(producerSha, Is.EqualTo(registrySha));
                Assert.That(producerSha, Is.EqualTo(BadgeSetupOutputWriter.ComputeGitBlobSha(producer)));
                Assert.That(File.Exists(Path.Combine(directory, ".github", "workflows", "architecture-health-badge-renewal.yml")), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(directory, "README.md")), Does.Contain("/badge-relay/v1/a7f4k2m9.svg"));
                Assert.That(File.Exists(Path.Combine(directory, "README.badge.md")), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

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
}
