using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Fake-composition tests for ArchitectureBaselineApplicationService.Migrate — split out to keep
// the main fake-composition test file under the file-size lint threshold.
public sealed partial class ArchitectureBaselineApplicationServiceFakeCompositionTests
{
    [Test]
    public void Migrate_MixedScenario_ClassifiesMatchedAndStaleEntries()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.Yaml, Is.Not.Null);
            // SrcA/RefA matches the sole candidate for that pair; SrcB/RefB and SrcC/RefC (under the
            // unknown "unknown-rule" contract) have zero matching candidates.
            Assert.That(outcome.MatchedCount, Is.EqualTo(1));
            Assert.That(outcome.StaleCount, Is.EqualTo(2));
            Assert.That(outcome.AmbiguousCount, Is.EqualTo(0));
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcA").Status, Is.EqualTo("matched"));
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcB").Status, Is.EqualTo("stale"));
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcC").Status, Is.EqualTo("stale"));
        });

        var migratedEntry = baselineGenerator.EntriesReceived!.Single(e => e.SourceType == "SrcA");
        Assert.That(migratedEntry.Reason, Is.EqualTo("old reason A"));
    }

    [Test]
    public void Migrate_AmbiguousMatch_FailsClosedAndDoesNotWrite()
    {
        var document = CreateDocumentWith_knownRule();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
                new("strict", "known-rule", "SrcA", "RefA"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var baselineLoadingService = new FakeBaselineLoadingService
        {
            DocumentToReturn = new ArchitectureBaselineDocument
            {
                Version = 1,
                Baseline = new Contracts.ArchitectureBaselineContractGroups
                {
                    Strict = new List<ArchitectureBaselineContractEntry>
                    {
                        new()
                        {
                            Id = "known-rule",
                            IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                            {
                                new() { SourceType = "SrcA", ForbiddenReference = "RefA", Reason = "ambiguous debt" },
                            },
                        },
                    },
                },
            },
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), new FakeBaselineGenerator(), baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Yaml, Is.Null);
            Assert.That(outcome.AmbiguousCount, Is.EqualTo(1));
            Assert.That(outcome.Report.Single().Status, Is.EqualTo("ambiguous"));
            Assert.That(outcome.Report.Single().MatchCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Migrate_DryRun_ReportsWithoutProducingYaml()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            DryRun = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.Yaml, Is.Null, "dry-run must never produce output to write.");
            Assert.That(outcome.MatchedCount, Is.EqualTo(1));
            Assert.That(baselineGenerator.WasCalled, Is.False, "dry-run must not invoke the generator.");
        });
    }

    [Test]
    public void Migrate_MissingOutputWithoutDryRun_FailsWithoutTouchingCollaborators()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("--output is required"));
            Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        });
    }

    [Test]
    public void Migrate_OutputEqualsBaselinePath_RefusesWithoutTouchingCollaborators()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "same-path.baseline.yml",
            OutputPath = "same-path.baseline.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("never overwrites the source file"));
            Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        });
    }

    [Test]
    public void Migrate_AlreadyVersion2Baseline_RefusesBeforeCollectingCandidates()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, _) =
            CreateMixedScenarioCollaborators();
        var baselineLoadingService = new FakeBaselineLoadingService
        {
            DocumentToReturn = new ArchitectureBaselineDocument { Version = 2, Baseline = new Contracts.ArchitectureBaselineContractGroups() },
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("already version 2"));
            Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        });
    }

    [Test]
    public void Migrate_ScopedToAuditMode_CarriesStrictEntryThroughAsOutOfScope()
    {
        var document = CreateDocumentWithStrictAndAuditRules();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("audit", "audit-rule", "SrcY", "RefY"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var baselineLoadingService = new FakeBaselineLoadingService
        {
            DocumentToReturn = CreateBaselineWithStrictAndAuditEntries(),
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), new FakeBaselineGenerator(), baselineLoadingService);

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "audit",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            // The strict "known-rule" entry (SrcA/RefA) is outside --mode audit scope: it must be
            // reported out_of_scope, not stale, and must not count toward matched/stale/ambiguous.
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcA").Status, Is.EqualTo("out_of_scope"));
            Assert.That(outcome.MatchedCount, Is.EqualTo(1));
            Assert.That(outcome.StaleCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Migrate_UnknownSelectedContract_FailsWithoutWriting()
    {
        var document = CreateDocumentWith_knownRule();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>(),
        };
        runnerSetupService.RunnerToReturn = runner;
        var baselineGenerator = new FakeBaselineGenerator();
        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), baselineGenerator,
            new FakeBaselineLoadingService { DocumentToReturn = CreateMixedBaseline() });

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "all",
            ContractIds = new[] { "missing-rule" },
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("Unknown contract IDs"));
            Assert.That(outcome.Error, Does.Contain("missing-rule"));
            Assert.That(baselineGenerator.WasCalled, Is.False);
        });
    }

    [Test]
    public void Migrate_InvalidMode_FailsWithoutCallingCollaborators()
    {
        var runnerSetupService = new FakeRunnerSetupService();
        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), new FakeBaselineGenerator(),
            new FakeBaselineLoadingService { DocumentToReturn = CreateMixedBaseline() });

        BaselineMigrateOutcome outcome = applicationService.Migrate(new BaselineMigrateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            OutputPath = "unused-by-fakes.migrated.yml",
            Mode = "not-a-real-mode",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("Invalid mode"));
            Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        });
    }
}
