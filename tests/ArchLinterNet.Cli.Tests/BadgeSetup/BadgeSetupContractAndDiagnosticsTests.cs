using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
public sealed class BadgeSetupContractAndDiagnosticsTests
{
    [Test]
    public void ContractEnumsRoundTripAndRejectUnknownWireValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BadgeSetupMode.None.ToWireValue(), Is.EqualTo("none"));
            Assert.That(BadgeSetupMode.GithubRaw.ToWireValue(), Is.EqualTo("github-raw"));
            Assert.That(BadgeSetupMode.Relay.ToWireValue(), Is.EqualTo("relay"));
            Assert.That(BadgeDisclosureProfile.HeadlineOnlyV1.ToWireValue(), Is.EqualTo(BadgeSetupContract.HeadlineOnlyProfile));
            Assert.That(BadgeDisclosureProfile.HeadlinePlusFreshnessV1.ToWireValue(), Is.EqualTo(BadgeSetupContract.HeadlinePlusFreshnessProfile));
            Assert.That(BadgeSetupContract.TryParseMode("none", out BadgeSetupMode none), Is.True);
            Assert.That(none, Is.EqualTo(BadgeSetupMode.None));
            Assert.That(BadgeSetupContract.TryParseMode("github-raw", out BadgeSetupMode raw), Is.True);
            Assert.That(raw, Is.EqualTo(BadgeSetupMode.GithubRaw));
            Assert.That(BadgeSetupContract.TryParseMode("relay", out BadgeSetupMode relay), Is.True);
            Assert.That(relay, Is.EqualTo(BadgeSetupMode.Relay));
            Assert.That(BadgeSetupContract.TryParseMode(null, out _), Is.False);
            Assert.That(BadgeSetupContract.TryParseMode("unsupported", out _), Is.False);
            Assert.That(BadgeSetupContract.TryParseProfile(BadgeSetupContract.HeadlineOnlyProfile, out _), Is.True);
            Assert.That(BadgeSetupContract.TryParseProfile(BadgeSetupContract.HeadlinePlusFreshnessProfile, out _), Is.True);
            Assert.That(BadgeSetupContract.TryParseProfile(null, out _), Is.False);
            Assert.That(BadgeSetupContract.TryParseProfile("unsupported", out _), Is.False);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => ((BadgeSetupMode)99).ToWireValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((BadgeDisclosureProfile)99).ToWireValue());
    }

    [Test]
    public void DiagnosticCatalogProvidesPublicShapeForEveryCode()
    {
        string[] codes =
        [
            BadgeSetupDiagnosticCodes.InvalidConfiguration,
            BadgeSetupDiagnosticCodes.UnsupportedSchema,
            BadgeSetupDiagnosticCodes.UnsupportedContractVersion,
            BadgeSetupDiagnosticCodes.UnsupportedMode,
            BadgeSetupDiagnosticCodes.UnsupportedDisclosureProfile,
            BadgeSetupDiagnosticCodes.UnsupportedBundle,
            BadgeSetupDiagnosticCodes.UnsupportedCompatibilityPlan,
            BadgeSetupDiagnosticCodes.MalformedIdentity,
            BadgeSetupDiagnosticCodes.UnsupportedVisibility,
            BadgeSetupDiagnosticCodes.VisibilityConflict,
            BadgeSetupDiagnosticCodes.MissingCapability,
            BadgeSetupDiagnosticCodes.UnsupportedPlan,
            BadgeSetupDiagnosticCodes.MissingDestination,
            BadgeSetupDiagnosticCodes.InvalidRenewal,
            BadgeSetupDiagnosticCodes.RenewalCostExceeded,
            BadgeSetupDiagnosticCodes.LeaseExceeded,
            BadgeSetupDiagnosticCodes.ConfigurationConflict,
            BadgeSetupDiagnosticCodes.DestinationUnavailable,
            BadgeSetupDiagnosticCodes.FirstEvidenceMissing,
            BadgeSetupDiagnosticCodes.ArtifactInvalid,
            BadgeSetupDiagnosticCodes.ValidityExpired,
            BadgeSetupDiagnosticCodes.DestinationRevoked,
            BadgeSetupDiagnosticCodes.ProviderQuotaFailure,
            BadgeSetupDiagnosticCodes.InvalidPin,
            BadgeSetupDiagnosticCodes.InvalidManagedPath,
            BadgeSetupDiagnosticCodes.InvalidEndpoint,
            BadgeSetupDiagnosticCodes.InvalidProject,
            BadgeSetupDiagnosticCodes.InvalidObservation,
            BadgeSetupDiagnosticCodes.DisclosureApprovalRequired,
            BadgeSetupDiagnosticCodes.CacheStale,
        ];

        foreach (string code in codes)
        {
            BadgeSetupDiagnostic diagnostic = BadgeSetupDiagnosticCatalog.Create(
                code,
                BadgeSetupDiagnosticSeverity.Warning,
                privateDetail: "private-only detail");
            BadgeSetupPublicDiagnostic publicDiagnostic = diagnostic.ToPublic();
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Code, Is.EqualTo(code));
                Assert.That(diagnostic.Message, Is.Not.Empty);
                Assert.That(diagnostic.Remediation, Is.Not.Empty);
                Assert.That(publicDiagnostic.Code, Is.EqualTo(code));
                Assert.That(publicDiagnostic.Severity, Is.EqualTo("warning"));
                Assert.That(publicDiagnostic.Message, Does.Not.Contain("private-only"));
            });
        }

        BadgeSetupDiagnostic fallback = BadgeSetupDiagnosticCatalog.Create("unknown-code");
        Assert.That(fallback.Code, Is.EqualTo(BadgeSetupDiagnosticCodes.InvalidConfiguration));
    }
}
