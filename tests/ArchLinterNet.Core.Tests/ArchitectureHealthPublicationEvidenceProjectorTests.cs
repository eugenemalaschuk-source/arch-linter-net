using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureHealthPublicationEvidenceProjectorTests
{
    [Test]
    public void Project_UsesEvaluationDateHorizonAndIsDeterministic()
    {
        ArchitectureHealthOutcome outcome = Outcome(
            Waiver("first", new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 12)),
            Waiver("second", new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10)));

        ArchitectureHealthPublicationEvidence first = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);
        ArchitectureHealthPublicationEvidence second = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsReady, Is.True);
            Assert.That(first.SemanticHorizon, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public void Project_UsesEvaluationDateRolloverWhenWaiverHasNoExpiry()
    {
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(
            Outcome(Waiver("active", new DateOnly(2026, 9, 9), null)));

        Assert.That(evidence.SemanticHorizon, Is.EqualTo(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void Project_FailsClosedForExpiredOrMissingValidityEvidence()
    {
        ArchitectureHealthPublicationEvidence expired = ArchitectureHealthPublicationEvidenceProjector.Project(
            Outcome(Waiver("expired", new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 8), "expired")));
        ArchitectureHealthPublicationEvidence missing = ArchitectureHealthPublicationEvidenceProjector.Project(Outcome());

        Assert.Multiple(() =>
        {
            Assert.That(expired.IsReady, Is.False);
            Assert.That(expired.Reasons.Select(reason => reason.Code), Does.Contain("expired_waiver"));
            Assert.That(missing.IsReady, Is.False);
            Assert.That(missing.Reasons.Select(reason => reason.Code), Does.Contain("missing_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenRequiredExternalEvidenceHasNoHorizon()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", new DateOnly(2026, 9, 9), null));
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with
        {
            ExternalEvidenceRequirements = [new ArchitectureExternalEvidenceRequirement { Id = "external", Required = true }],
        };
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("required_external_evidence_horizon_unknown"));
            Assert.That(evidence.SemanticHorizon, Is.Null);
        });
    }

    private static ArchitectureHealthOutcome Outcome(params ArchitectureWaiverLifecycleRecord[] waivers)
    {
        ArchitecturePolicyInventory inventory = new(
            ArchitecturePolicyInventory.CurrentSchemaId,
            42,
            new ArchitecturePolicyInventoryRules(42, 0, 0),
            new ArchitecturePolicyInventoryIgnoreDebt(waivers.Length, waivers.Length, 0, 0, 0, 0),
            waivers);
        ValidationOutcome validation = new(
            true, [], [], [], "off", [], "off", [], "off", [], [], [])
        {
            PolicyInventory = inventory,
            WaiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment("strict", waivers, ["expired", "invalid", "stale"]),
        };
        return new ArchitectureHealthOutcome(
            new ArchitectureHealthSummary(ArchitectureHealthSummary.CurrentSchemaId, ArchitectureHealthGate.Pass, ArchitectureHealthState.Healthy, []),
            [new ArchitectureHealthValidationOutcome("strict", validation)],
            new ArchitectureDebtGateOutcome(true, true, new ArchitectureDebtGateEvaluation(true, "strict", []),
                new BaselineVerifyOutcome(true, true, [], [], [], [], [])));
    }

    private static ArchitectureWaiverLifecycleRecord Waiver(string id, DateOnly evaluationDate, DateOnly? expires, string state = "active") =>
        new(id, state, "contract", "contract", "group", "type", "forbidden", null, "reason", "owner", "issue",
            evaluationDate, expires, evaluationDate, true);
}
