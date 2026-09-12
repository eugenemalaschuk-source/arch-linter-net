using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
public sealed class BadgeSetupEngineEdgeCaseTests
{
    [Test]
    public void PublicRepositoryRequestDefaultsToGithubRaw()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(new("owner", "repo", "public", new(CanUseGithubRaw: true))));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Plan.Mode, Is.EqualTo(BadgeSetupMode.GithubRaw.ToWireValue()));
            Assert.That(result.Plan.PublicEndpointExpected, Is.True);
        });
    }

    [Test]
    public void UnsupportedContractIdentityAndVisibilityValuesAreRejected()
    {
        BadgeSetupConfiguration configuration = ValidRelay() with
        {
            SchemaId = "other-schema",
            ContractVersion = "v2",
            Bundle = "other-bundle",
            CompatibilityPlan = "other-plan",
            Mode = "future",
            DisclosureProfile = "future-profile",
        };
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            configuration,
            new("bad owner", "bad/repo", "internal", new()));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedSchema));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedContractVersion));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedBundle));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedCompatibilityPlan));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedMode));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedDisclosureProfile));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.MalformedIdentity));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedVisibility));
        });
    }

    [Test]
    public void RelayRejectsUnsafeDestinationProjectPinsManagedFilesAndProducer()
    {
        BadgeSetupConfiguration configuration = ValidRelay() with
        {
            Repository = new("owner", "repo", "private", 0, 9_007_199_254_740_992),
            Destination = new("bad alias", "not-an-account", "http://relay.example/path", "bad audience!"),
            ProviderPlan = "unsupported",
            BaseRef = "/main",
            Project = new("../policy.yml", "/solution.slnx"),
            Pins = new("owner/repo/workflow.yml", "NOT-A-SHA", "owner/action@not-a-sha", "not-a-digest"),
            ManagedFiles = ["README.md", "README.md", "../escape"],
            Producer = new(
                "producer.yml",
                "not-a-sha",
                "bad/name",
                "Different check",
                "Bad App",
                "push",
                "bad/name",
                "bad/name",
                "wrong.json"),
        };
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            configuration,
            new("owner", "repo", "private", new(RepositoryId: 123, RepositoryOwnerId: 456)));
        string[] codes = result.Diagnostics.Select(static item => item.Code).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.MalformedIdentity));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedPlan));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidEndpoint));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidConfiguration));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidProject));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidPin));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidManagedPath));
        });
    }

    [Test]
    public void ExistingDeploymentAndMissingRelayCapabilitiesBlockChanges()
    {
        BadgeSetupPlanResult result = BadgeSetupEngine.BuildPlan(
            ValidRelay(),
            new("owner", "repo", "private", new(ProviderPlan: "free", RepositoryId: 123, RepositoryOwnerId: 456)),
            new(true, Mode: "github-raw", DisclosureProfile: BadgeSetupContract.HeadlineOnlyProfile, Alias: "other123"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Plan.PlannedChanges, Is.Empty);
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.ConfigurationConflict));
            Assert.That(result.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.UnsupportedPlan));
            Assert.That(result.Diagnostics.Count(static item => item.Code == BadgeSetupDiagnosticCodes.MissingCapability), Is.GreaterThanOrEqualTo(4));
        });
    }

    [Test]
    public void RenewalValidationCoversNonRelayZeroAndCostPaths()
    {
        BadgeSetupConfiguration nonRelay = NoneConfiguration() with
        {
            Renewal = new(true, 0, 0),
        };
        BadgeSetupPlanResult nonRelayResult = BadgeSetupEngine.BuildPlan(
            nonRelay,
            new("owner", "repo", "private", new()));
        BadgeSetupPlanResult costResult = BadgeSetupEngine.BuildPlan(
            ValidRelay() with { Renewal = new(true, 1, 60) },
            new("owner", "repo", "private", new(
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
            Assert.That(nonRelayResult.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidRenewal));
            Assert.That(nonRelayResult.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.ConfigurationConflict));
            Assert.That(nonRelayResult.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.LeaseExceeded));
            Assert.That(costResult.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.InvalidRenewal));
            Assert.That(costResult.Diagnostics.Select(static item => item.Code), Does.Contain(BadgeSetupDiagnosticCodes.RenewalCostExceeded));
        });
    }

    [Test]
    public void DoctorReportsEveryUnhealthyObservationDimension()
    {
        BadgeSetupPlanResult plan = BadgeSetupEngine.BuildPlan(
            new BadgeSetupRequest(new("owner", "repo", "private", new())));
        BadgeDoctorReport report = BadgeSetupEngine.RunDoctor(plan, new(
            DestinationReachable: false,
            FirstEvidenceAvailable: false,
            ArtifactValid: false,
            ValidityCurrent: false,
            DestinationRevoked: true,
            ProviderQuotaAvailable: false,
            IdentityValid: false,
            PinsValid: false,
            OidcValid: false,
            RequiredCheckAvailable: false,
            RulesApiAvailable: false,
            CacheFresh: false));
        string[] codes = report.Diagnostics.Select(static item => item.Code).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(report.Available, Is.False);
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.DestinationUnavailable));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.FirstEvidenceMissing));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.ArtifactInvalid));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.ValidityExpired));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.DestinationRevoked));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.ProviderQuotaFailure));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.MalformedIdentity));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.InvalidPin));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.MissingCapability));
            Assert.That(codes, Does.Contain(BadgeSetupDiagnosticCodes.CacheStale));
        });
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

    private static BadgeSetupConfiguration ValidRelay() => new(
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
        Producer: new(
            BadgeSetupContract.DefaultProducerWorkflowPath,
            new string('0', 40),
            BadgeSetupContract.DefaultCheckName,
            BadgeSetupContract.DefaultCheckName,
            BadgeSetupContract.DefaultCheckApp,
            "pull_request",
            BadgeSetupContract.DefaultArtifactName,
            BadgeSetupContract.DefaultEvidenceArtifactName,
            BadgeSetupContract.DefaultPayloadPath),
        DisclosureApproved: true,
        ProviderPlan: "pro");
}
