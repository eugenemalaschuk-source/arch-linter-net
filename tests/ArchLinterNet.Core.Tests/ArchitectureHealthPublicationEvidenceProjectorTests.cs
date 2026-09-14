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
    public void Project_UsesExplicitEvaluationDateWhenWaiverSetIsEmpty()
    {
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(
            OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9)));

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.True);
            Assert.That(evidence.SemanticHorizon,
                Is.EqualTo(new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(evidence.Reasons, Is.Empty);
        });
    }

    [Test]
    public void Project_FailsClosedForInvalidExplicitEvaluationDate()
    {
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(
            OutcomeWithEvaluationDate(DateOnly.MinValue));

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("invalid_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenLifecycleReceiptsDisagreeOnEvaluationDate()
    {
        ArchitectureHealthOutcome first = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9));
        ArchitectureHealthOutcome second = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 10));
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(first with
        {
            ValidationOutcomes = [first.ValidationOutcomes[0], second.ValidationOutcomes[0]],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("inconsistent_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenExplicitEvaluationDateHasNoFiniteHorizon()
    {
        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(
            OutcomeWithEvaluationDate(DateOnly.MaxValue));

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("invalid_evaluation_date"));
        });
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

    [Test]
    public void Project_FailsClosedWhenValidationOutcomesAreEmpty()
    {
        ArchitectureHealthOutcome outcome = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9)) with
        {
            ValidationOutcomes = [],
        };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("missing_validation_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedForBlankModeAndMissingValidationOutcome()
    {
        ArchitectureHealthOutcome outcome = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9));
        ArchitectureHealthValidationOutcome blankMode = outcome.ValidationOutcomes[0] with { Mode = " " };
        ArchitectureHealthValidationOutcome missingOutcome = new("other", null!);

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [blankMode, missingOutcome],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code),
                Does.Contain("malformed_waiver_receipt").And.Contain("missing_validation_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedForUntrustedPolicyInventory()
    {
        ArchitectureHealthOutcome outcome = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9));
        ArchitecturePolicyInventory invalidInventory = outcome.ValidationOutcomes[0].Outcome.PolicyInventory! with
        {
            EffectiveRuleCount = -1,
        };
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with { PolicyInventory = invalidInventory };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("invalid_policy_inventory"));
        });
    }

    [Test]
    public void Project_FailsClosedForBlankWaiverLifecycleProfile()
    {
        ArchitectureHealthOutcome outcome = OutcomeWithEvaluationDate(new DateOnly(2026, 9, 9));
        ArchitectureWaiverLifecycleAssessment lifecycle = outcome.ValidationOutcomes[0].Outcome.WaiverLifecycleAssessment! with
        {
            Profile = " ",
        };
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with { WaiverLifecycleAssessment = lifecycle };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("malformed_waiver_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenPolicyInventoryWaiversDisagreeWithLifecycleRecords()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", new DateOnly(2026, 9, 9), null));
        ArchitecturePolicyInventory mismatchedInventory = outcome.ValidationOutcomes[0].Outcome.PolicyInventory! with
        {
            Waivers = [],
        };
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with { PolicyInventory = mismatchedInventory };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("inconsistent_waiver_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedForIncompleteWaiverLifecycleRecords()
    {
        ArchitectureWaiverLifecycleRecord missingDate = Waiver("missing-date", DateOnly.MinValue, null);
        ArchitectureWaiverLifecycleRecord malformed = Waiver("malformed", new DateOnly(2026, 9, 9), null) with { State = " " };

        ArchitectureHealthOutcome outcome = Outcome(missingDate, malformed);

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code),
                Does.Contain("missing_evaluation_date").And.Contain("malformed_waiver_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenWaiverRecordsDisagreeOnEvaluationDate()
    {
        ArchitectureHealthOutcome outcome = Outcome(
            Waiver("first", new DateOnly(2026, 9, 9), null),
            Waiver("second", new DateOnly(2026, 9, 10), null));

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("inconsistent_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenWaiverRecordEvaluationDateHasNoFiniteHorizon()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", DateOnly.MaxValue, null));

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("invalid_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenWaiverExpiryHasNoFiniteHorizon()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", new DateOnly(2026, 9, 9), DateOnly.MaxValue));

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome);

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("invalid_evaluation_date"));
        });
    }

    [Test]
    public void Project_FailsClosedForDuplicateExternalEvidenceRequirementIds()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", new DateOnly(2026, 9, 9), null));
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with
        {
            ExternalEvidenceRequirements =
            [
                new ArchitectureExternalEvidenceRequirement { Id = "dup", Required = false },
                new ArchitectureExternalEvidenceRequirement { Id = "dup", Required = false },
            ],
        };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("inconsistent_external_evidence_receipt"));
        });
    }

    [Test]
    public void Project_FailsClosedWhenExternalEvidenceTrustReceiptsHaveNoMatchingRequirements()
    {
        ArchitectureHealthOutcome outcome = Outcome(Waiver("active", new DateOnly(2026, 9, 9), null));
        SarifEvidenceReadResult trustReceipt = new(
            SarifEvidenceTrustStatus.Valid,
            "ok",
            "trusted",
            new SarifEvidenceProvenance("external", null, null, null, null, null, null, null));
        ValidationOutcome validation = outcome.ValidationOutcomes[0].Outcome with
        {
            ExternalEvidenceTrustReceipts = [trustReceipt],
        };

        ArchitectureHealthPublicationEvidence evidence = ArchitectureHealthPublicationEvidenceProjector.Project(outcome with
        {
            ValidationOutcomes = [new ArchitectureHealthValidationOutcome("strict", validation)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(evidence.IsReady, Is.False);
            Assert.That(evidence.Reasons.Select(reason => reason.Code), Does.Contain("inconsistent_external_evidence_receipt"));
        });
    }

    private static ArchitectureHealthOutcome Outcome(params ArchitectureWaiverLifecycleRecord[] waivers) =>
        OutcomeCore(null, waivers);

    private static ArchitectureHealthOutcome OutcomeWithEvaluationDate(
        DateOnly evaluationDate,
        params ArchitectureWaiverLifecycleRecord[] waivers) =>
        OutcomeCore(evaluationDate, waivers);

    private static ArchitectureHealthOutcome OutcomeCore(
        DateOnly? evaluationDate,
        params ArchitectureWaiverLifecycleRecord[] waivers)
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
            WaiverLifecycleAssessment = new ArchitectureWaiverLifecycleAssessment(
                "strict", waivers, ["expired", "invalid", "stale"])
            {
                EvaluationDate = evaluationDate,
            },
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
