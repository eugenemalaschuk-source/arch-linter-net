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
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("already version 2"));
            Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        });
    }

    [Test]
    public void Migrate_AlwaysClassifiesEveryEntry_RegardlessOfContractGroup()
    {
        // There is no --mode/--contract scoping any more: every entry in the file — strict and
        // audit alike — is always correlated against the full candidate set and classified.
        var document = CreateDocumentWithStrictAndAuditRules();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
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
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            // The strict entry (SrcA/RefA) matches its sole candidate; the audit entry (SrcY/RefY)
            // has zero candidates in this scenario and is genuinely stale — not a special
            // "out of scope" status, and not silently carried through with a fabricated identity.
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcA").Status, Is.EqualTo("matched"));
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcY").Status, Is.EqualTo("stale"));
            Assert.That(outcome.MatchedCount, Is.EqualTo(1));
            Assert.That(outcome.StaleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Migrate_AmbiguousEntryInOtherContractGroup_StillDetectedAndBlocksWrite()
    {
        // Reproduces the exact scenario from review: a legacy pair that would have been labeled
        // "out of scope" under mode/contract filtering (here: an audit-group entry) must still be
        // recognized as ambiguous when it genuinely correlates to more than one current candidate —
        // never silently upgraded to a single fabricated v2 identity.
        var document = CreateDocumentWithStrictAndAuditRules();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("audit", "audit-rule", "SrcY", "RefY"),
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
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Yaml, Is.Null);
            Assert.That(outcome.Report.Single(r => r.SourceType == "SrcY").Status, Is.EqualTo("ambiguous"));
            Assert.That(outcome.AmbiguousCount, Is.EqualTo(1));
        });
    }
}
