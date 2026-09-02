using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureApplicabilityBaselineLifecycleTests
{
    private const string PolicyId = "applicability-policy";
    private const string Family = "dependency";

    [Test]
    public void BaselineLifecycle_ApplicabilityFindingIsNewThenExactKnownAndChangedIdentityIsNew()
    {
        ArchitectureContractExecutionResult initialExecution = Execution(
            "control-a", ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput);
        BaselineDiffOutcome initial = Diff(initialExecution, EmptyBaseline());
        ArchitectureBaselineComparisonEntry initialEntry = initial.New.Single();

        BaselineDiffOutcome exactKnown = Diff(initialExecution, BaselineFor(initialEntry));
        BaselineDiffOutcome changed = Diff(
            Execution("control-b", ArchitectureApplicabilityReasonCodes.StaleDeclaration),
            BaselineFor(initialEntry));

        Assert.Multiple(() =>
        {
            Assert.That(initial.New, Has.Count.EqualTo(1));
            Assert.That(initialEntry.Identity!.Kind, Is.EqualTo("applicability"));
            Assert.That(initialEntry.Identity.ContractId, Is.EqualTo(initialEntry.ContractId));
            Assert.That(exactKnown.New, Is.Empty);
            Assert.That(exactKnown.Frozen, Has.Count.EqualTo(1));
            Assert.That(exactKnown.Frozen[0].Identity, Is.EqualTo(initialEntry.Identity));
            Assert.That(changed.New, Has.Count.EqualTo(1));
            Assert.That(changed.New[0].Identity, Is.Not.EqualTo(initialEntry.Identity));
            Assert.That(changed.Resolved.Single().Identity, Is.EqualTo(initialEntry.Identity));
        });
    }

    [Test]
    public void BaselineMerge_ApplicabilityIdentityRemainsLifecycleOnly()
    {
        ArchitectureBaselineComparisonEntry entry = Diff(
            Execution("control-a", ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput), EmptyBaseline()).New.Single();
        ArchitectureContractDocument document = Document();

        ArchitectureBaselineLoadingService.MergeAndValidate(document, BaselineFor(entry));

        Assert.That(document.Contracts.Strict.Single().IgnoredViolations, Is.Empty,
            "A known applicability baseline is lifecycle evidence, not an ignore that can suppress assessment trust.");
    }

    [Test]
    public void DebtGate_ApplicabilityCandidatesDistinguishExactKnownDebtFromNewDebt()
    {
        ArchitectureContractExecutionResult initialExecution = Execution(
            "control-a", ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput);
        ArchitectureBaselineDocument baseline = BaselineFor(Diff(initialExecution, EmptyBaseline()).New.Single());

        ArchitectureDebtGateOutcome exactKnown = EvaluateDebtGate(initialExecution, baseline);
        ArchitectureDebtGateOutcome changed = EvaluateDebtGate(
            Execution("control-b", ArchitectureApplicabilityReasonCodes.StaleDeclaration), baseline);

        Assert.Multiple(() =>
        {
            Assert.That(exactKnown.Passed, Is.True);
            Assert.That(exactKnown.PersistentDebt.Frozen, Has.Count.EqualTo(1));
            Assert.That(changed.Passed, Is.False);
            Assert.That(changed.PersistentDebt.New, Has.Count.EqualTo(1));
            Assert.That(changed.PersistentDebt.New[0].Identity,
                Is.Not.EqualTo(exactKnown.PersistentDebt.Frozen[0].Identity));
        });
    }

    [Test]
    public void ChangeSnapshot_ApplicabilityFindingIsNewThenExactKnownAndChangedIdentityIsNew()
    {
        ArchitectureApplicabilityProjection initialProjection = Projection(
            "control-a", ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput);
        ArchitectureApplicabilityProjection changedProjection = Projection(
            "control-b", ArchitectureApplicabilityReasonCodes.StaleDeclaration);
        ArchitectureChangeSnapshot withoutApplicability = Snapshot(null);
        ArchitectureChangeSnapshot initial = Snapshot(initialProjection);
        ArchitectureChangeSnapshot changed = Snapshot(changedProjection);

        ArchitectureChangeReport newlyIntroduced = ArchitectureChangeReports.Compare(withoutApplicability, initial, "run");
        ArchitectureChangeReport exactKnown = ArchitectureChangeReports.Compare(initial, initial, "run");
        ArchitectureChangeReport identityChanged = ArchitectureChangeReports.Compare(initial, changed, "run");

        Assert.Multiple(() =>
        {
            Assert.That(newlyIntroduced.NewFindings.Single().Identity,
                Is.EqualTo(initialProjection.Findings.Single().CanonicalIdentity));
            Assert.That(exactKnown.NewFindings, Is.Empty);
            Assert.That(exactKnown.ExistingFindings.Single().Identity,
                Is.EqualTo(initialProjection.Findings.Single().CanonicalIdentity));
            Assert.That(identityChanged.NewFindings.Single().Identity,
                Is.EqualTo(changedProjection.Findings.Single().CanonicalIdentity));
            Assert.That(identityChanged.NewFindings.Single().Identity,
                Is.Not.EqualTo(initialProjection.Findings.Single().CanonicalIdentity));
        });
    }

    private static BaselineDiffOutcome Diff(
        ArchitectureContractExecutionResult execution,
        ArchitectureBaselineDocument baseline)
    {
        return CreateBaselineService(execution, baseline).Diff(new BaselineDiffRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "strict",
        });
    }

    private static ArchitectureDebtGateOutcome EvaluateDebtGate(
        ArchitectureContractExecutionResult execution,
        ArchitectureBaselineDocument baseline)
    {
        return new ArchitectureDebtGateApplicationService(CreateBaselineService(execution, baseline)).Evaluate(
            new ArchitectureDebtGateRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "strict",
            });
    }

    private static ArchitectureBaselineApplicationService CreateBaselineService(
        ArchitectureContractExecutionResult execution,
        ArchitectureBaselineDocument baseline)
    {
        ArchitectureContractDocument document = Document();
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = new FakeContractRunner(
                ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document)),
        };
        var contractExecutor = new FakeContractExecutor();
        contractExecutor.ResultsByMode["strict"] = execution;
        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService,
            new FakeContractHandlerRegistry(),
            contractExecutor,
            new FakeBaselineGenerator(),
            new FakeBaselineLoadingService { DocumentToReturn = baseline });

        return applicationService;
    }

    private static ArchitectureContractDocument Document() => new()
    {
        Version = 1,
        Name = "Applicability baseline lifecycle",
        Contracts = new ArchitectureContractGroups
        {
            Strict =
            [
                new ArchitectureDependencyContract
                {
                    Id = PolicyId,
                    Name = PolicyId,
                    Source = "core",
                },
            ],
        },
    };

    private static ArchitectureContractExecutionResult Execution(string control, string reasonCode)
    {
        ArchitectureApplicabilityProvenance provenance = new(Family, control, PolicyId);
        ArchitectureApplicabilityExpectedEntry expected = new(
            control, Family, ArchitectureApplicabilityMembership.Required, provenance);
        ArchitectureApplicabilityRecord record = new(
            control,
            Family,
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason(reasonCode, Family, control, PolicyId)],
            provenance);

        return new ArchitectureContractExecutionResult(
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureCoverageSummary>())
        {
            ApplicabilityExpectedEntries = [expected],
            ApplicabilityRecords = [record],
        };
    }

    private static ArchitectureBaselineDocument EmptyBaseline() => new()
    {
        Version = ArchitectureViolationIdentity.CurrentVersion,
        Baseline = new ArchitectureBaselineContractGroups(),
    };

    private static ArchitectureBaselineDocument BaselineFor(ArchitectureBaselineComparisonEntry entry) => new()
    {
        Version = ArchitectureViolationIdentity.CurrentVersion,
        Baseline = new ArchitectureBaselineContractGroups
        {
            Strict =
            [
                new ArchitectureBaselineContractEntry
                {
                    Id = entry.ContractId,
                    IgnoredViolations =
                    [
                        ArchitectureBaselineIgnoredViolation.FromIdentity(
                            entry.Identity!, entry.SourceType, entry.ForbiddenReference, "reviewed applicability debt"),
                    ],
                },
            ],
        },
    };

    private static ArchitectureApplicabilityProjection Projection(string control, string reasonCode)
    {
        ArchitectureContractExecutionResult execution = Execution(control, reasonCode);
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            execution.ApplicabilityExpectedEntries, execution.ApplicabilityRecords, conformancePassed: true)!;
        return ArchitectureApplicabilityProjector.Project(completion, "strict")!;
    }

    private static ArchitectureChangeSnapshot Snapshot(ArchitectureApplicabilityProjection? projection)
    {
        var outcome = new ValidationOutcome(
            Passed: false,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>(),
            CoverageFindings: Array.Empty<ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
            PolicyConsistencyConfig: "off",
            CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
            ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
            ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = "/repo",
            DiscoveredProjectPaths = ["/repo/src/Applicability/Applicability.csproj"],
            ApplicabilityProjection = projection,
        };

        return ArchitectureChangeSnapshotProjector.Project(
            "strict",
            outcome,
            new ArchitectureGraphOutcome(new ArchitectureDependencyGraph(
                Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>())),
            new ArchitectureGraphOutcome(new ArchitectureDependencyGraph(
                Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>())),
            Array.Empty<ArchitectureBaselineComparisonEntry>());
    }
}
