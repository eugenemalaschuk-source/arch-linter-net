using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthTemporalPublicationRevalidatorTests
{
    private static readonly DateOnly _sourceDate = new(2026, 9, 9);
    private static readonly DateOnly _crossMidnightDate = new(2026, 9, 10);
    private const string PayloadSha256 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string TreeSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string ProducerSha256 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    [Test]
    public void Revalidate_CrossMidnight_UsesNewFiniteHorizonAndAllBindings()
    {
        byte[] artifact = Artifact(Waiver("active", expires: new DateOnly(2026, 9, 12)));
        ArchitectureHealthTemporalPublicationReceipt receipt = Revalidate(artifact, _crossMidnightDate);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.IsReady, Is.True);
            Assert.That(receipt.SemanticHorizon, Is.EqualTo(new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(receipt.SourceHealthSha256, Is.EqualTo(Digest(artifact)));
            Assert.That(receipt.BadgePayloadSha256, Is.EqualTo(PayloadSha256));
            Assert.That(receipt.MergedTreeSha, Is.EqualTo(TreeSha));
            Assert.That(receipt.ProducerIdentitySha256, Is.EqualTo(ProducerSha256));
            Assert.That(receipt.Reasons, Is.Empty);
        });
    }

    [TestCase("expired", "expired_waiver")]
    [TestCase("stale", "stale_waiver")]
    [TestCase("invalid", "invalid_waiver")]
    [TestCase("metadata_incomplete", "metadata_incomplete_waiver")]
    public void Revalidate_NonReusableLifecycleState_FailsClosed(string state, string reason)
    {
        ArchitectureHealthTemporalPublicationReceipt receipt = Revalidate(
            Artifact(Waiver(state)), _crossMidnightDate);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.IsReady, Is.False);
            Assert.That(receipt.SemanticHorizon, Is.Null);
            Assert.That(receipt.Reasons.Select(item => item.Code), Does.Contain(reason));
        });
    }

    [Test]
    public void Revalidate_ExpiredAtNewEvaluationDate_FailsClosed()
    {
        ArchitectureHealthTemporalPublicationReceipt receipt = Revalidate(
            Artifact(Waiver("active", expires: new DateOnly(2026, 9, 9))), _crossMidnightDate);

        Assert.That(receipt.Reasons.Select(item => item.Code), Does.Contain("expired_waiver"));
    }

    [Test]
    public void Revalidate_RequiredExternalEvidenceWithoutFiniteHorizon_FailsClosed()
    {
        byte[] artifact = Artifact(
            Waiver("active"),
            new ArchitectureExternalEvidenceRequirement
            {
                Id = "scanner",
                Format = "sarif",
                Required = true,
                Tool = "scanner",
                Run = "run",
            });

        ArchitectureHealthTemporalPublicationReceipt receipt = Revalidate(artifact, _crossMidnightDate);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.IsReady, Is.False);
            Assert.That(receipt.Reasons.Select(item => item.Code),
                Does.Contain("required_external_evidence_horizon_unknown"));
        });
    }

    [Test]
    public void Revalidate_OriginallyUnassessablePublicationEvidence_CannotBeRevived()
    {
        JsonObject document = JsonNode.Parse(Encoding.UTF8.GetString(Artifact(Waiver("active"))))!.AsObject();
        JsonObject publication = document["report_evidence"]!["publication_evidence"]!.AsObject();
        publication["state"] = "unassessable";
        publication["semantic_horizon"] = null;
        publication["reasons"] = new JsonArray
        {
            new JsonObject
            {
                ["code"] = "source_unavailable",
                ["detail"] = "The original evidence was not ready.",
            },
        };

        ArchitectureHealthTemporalPublicationReceipt receipt = Revalidate(
            Encoding.UTF8.GetBytes(document.ToJsonString()), _crossMidnightDate);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.IsReady, Is.False);
            Assert.That(receipt.Reasons.Select(item => item.Code),
                Does.Contain("original_publication_evidence_unassessable"));
        });
    }

    [Test]
    public void Revalidate_SourceDigestMismatch_FailsClosedBeforeParsingEvidence()
    {
        byte[] artifact = Artifact(Waiver("active"));
        var binding = new ArchitectureHealthTemporalPublicationBinding(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            PayloadSha256,
            TreeSha,
            ProducerSha256);

        ArchitectureHealthTemporalPublicationReceipt receipt =
            ArchitectureHealthTemporalPublicationRevalidator.Revalidate(artifact, _crossMidnightDate, binding);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.IsReady, Is.False);
            Assert.That(receipt.Reasons.Select(item => item.Code), Does.Contain("source_health_sha256_mismatch"));
        });
    }

    [Test]
    public void Revalidate_MalformedOrIncompleteEvidence_FailsClosedDeterministically()
    {
        byte[] malformed = Encoding.UTF8.GetBytes("{\"schema_id\":\"architecture-health/v1\"}");
        ArchitectureHealthTemporalPublicationReceipt first = Revalidate(malformed, _crossMidnightDate);
        ArchitectureHealthTemporalPublicationReceipt second = Revalidate(malformed, _crossMidnightDate);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsReady, Is.False);
            Assert.That(first.Reasons.Select(item => item.Code), Does.Contain("missing_report_evidence").Or.Contain("malformed_report_evidence"));
            Assert.That(second.State, Is.EqualTo(first.State));
            Assert.That(second.SemanticHorizon, Is.EqualTo(first.SemanticHorizon));
            Assert.That(second.Reasons.Select(item => (item.Code, item.Detail)),
                Is.EqualTo(first.Reasons.Select(item => (item.Code, item.Detail))));
        });
    }

    private static ArchitectureHealthTemporalPublicationReceipt Revalidate(byte[] artifact, DateOnly date) =>
        ArchitectureHealthTemporalPublicationRevalidator.Revalidate(
            artifact,
            date,
            new ArchitectureHealthTemporalPublicationBinding(
                Digest(artifact), PayloadSha256, TreeSha, ProducerSha256));

    private static byte[] Artifact(
        ArchitectureWaiverLifecycleRecord waiver,
        ArchitectureExternalEvidenceRequirement? external = null)
    {
        ArchitecturePolicyInventory inventory = new(
            ArchitecturePolicyInventory.CurrentSchemaId,
            1,
            new ArchitecturePolicyInventoryRules(1, 0, 0),
            new ArchitecturePolicyInventoryIgnoreDebt(1, 1, 0, 0, 0, 0),
            [waiver]);
        ValidationOutcome validation = new(
            true, [], [], [], "off", [], "off", [], "off", [], [], [])
        {
            PolicyInventory = inventory,
            WaiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment(
                "strict", [waiver], ["expired", "invalid", "stale"])
            {
                EvaluationDate = _sourceDate,
            },
            ExternalEvidenceRequirements = external is null ? [] : [external],
            RepositoryRoot = "/repo",
            PolicyImportPaths = [],
            ResolvedAssemblyPaths = [],
            DiscoveredProjectPaths = [],
        };
        var debt = new BaselineVerifyOutcome(true, true, [], [], [], [], []);
        var debtGate = new ArchitectureDebtGateOutcome(
            true,
            true,
            new ArchitectureDebtGateEvaluation(true, "strict", []),
            debt);
        var healthOutcome = new ArchitectureHealthOutcome(
            ArchitectureHealthProjector.Project([new ArchitectureHealthValidationOutcome("strict", validation)], debtGate),
            [new ArchitectureHealthValidationOutcome("strict", validation)],
            debtGate)
        {
            ExecutionContext = "run",
            ConditionSetName = "ci",
        };
        return Encoding.UTF8.GetBytes(ArchitectureHealthProjector.FormatAsJson(healthOutcome));
    }

    private static ArchitectureWaiverLifecycleRecord Waiver(string state, DateOnly? expires = null) => new(
        "waiver-1",
        state,
        "dependency",
        "contract-id",
        "strict",
        "App",
        "Infrastructure",
        "target",
        "reviewed",
        "owner",
        "#1",
        new DateOnly(2026, 1, 1),
        expires,
        _sourceDate,
        true);

    private static string Digest(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
