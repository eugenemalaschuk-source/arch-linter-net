using System.Text.Json;
using ArchLinterNet.Cli.Commands.Badge.Application.Setup;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests.BadgeSetup;

[TestFixture]
public sealed class BadgeSetupConfigurationParserTests
{
    [Test]
    public void ParserReadsPinsManagedFilesAndNullableProviderPlan()
    {
        BadgeSetupConfiguration configuration = ValidConfiguration() with
        {
            ManagedFiles = ["README.md", "architecture-health-badge.json"],
            ProviderPlan = null,
        };

        BadgeSetupConfigurationParseResult result = BadgeSetupConfigurationParser.Parse(JsonSerializer.Serialize(configuration));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Configuration, Is.Not.Null);
            Assert.That(result.Configuration!.Pins!.WorkflowSha, Is.EqualTo(BadgeSetupContract.DefaultPublisherWorkflowSha));
            Assert.That(result.Configuration.ManagedFiles, Is.EquivalentTo(["README.md", "architecture-health-badge.json"]));
            Assert.That(result.Configuration.ProviderPlan, Is.Null);
        });
    }

    [Test]
    public void ParserRejectsInvalidPrimitiveShapesAndSafeIntegerBounds()
    {
        BadgeSetupConfiguration configuration = ValidConfiguration() with
        {
            ManagedFiles = ["README.md", "architecture-health-badge.json"],
        };
        string valid = JsonSerializer.Serialize(configuration);
        string pins = JsonSerializer.Serialize(configuration.Pins);
        string[] invalidInputs =
        [
            "",
            "[]",
            "{",
            valid.Replace("\"repository_id\":123", "\"repository_id\":0", StringComparison.Ordinal),
            valid.Replace("\"enabled\":false", "\"enabled\":\"false\"", StringComparison.Ordinal),
            valid.Replace("\"cadence_minutes\":1440", "\"cadence_minutes\":\"1440\"", StringComparison.Ordinal),
            valid.Replace(pins, "5", StringComparison.Ordinal),
            valid.Replace("[\"README.md\",\"architecture-health-badge.json\"]", "[1,\"\"]", StringComparison.Ordinal),
            valid[..^1] + ",\"unknown\":true}",
        ];

        foreach (string invalid in invalidInputs)
        {
            BadgeSetupConfigurationParseResult result = BadgeSetupConfigurationParser.Parse(invalid);
            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False, invalid);
                Assert.That(result.Configuration, Is.Null, invalid);
                Assert.That(result.Diagnostics, Is.Not.Empty, invalid);
            });
        }
    }

    [Test]
    public void ParserAcceptsNullableOptionalObjectsButRequiresCoreObjects()
    {
        BadgeSetupConfiguration configuration = ValidConfiguration() with
        {
            Pins = null,
            ManagedFiles = null,
        };
        string valid = JsonSerializer.Serialize(configuration);
        string withoutProject = valid.Replace(",\"project\":{\"policy_path\":\"architecture/dependencies.arch.yml\",\"solution_path\":\"ArchLinterNet.slnx\"}", string.Empty, StringComparison.Ordinal);

        BadgeSetupConfigurationParseResult nullable = BadgeSetupConfigurationParser.Parse(valid);
        BadgeSetupConfigurationParseResult missingProject = BadgeSetupConfigurationParser.Parse(withoutProject);

        Assert.Multiple(() =>
        {
            Assert.That(nullable.IsValid, Is.True);
            Assert.That(nullable.Configuration!.Pins, Is.Null);
            Assert.That(nullable.Configuration.ManagedFiles, Is.Null);
            Assert.That(missingProject.IsValid, Is.False);
            Assert.That(missingProject.Diagnostics, Is.Not.Empty);
        });
    }

    private static BadgeSetupConfiguration ValidConfiguration() => new(
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
