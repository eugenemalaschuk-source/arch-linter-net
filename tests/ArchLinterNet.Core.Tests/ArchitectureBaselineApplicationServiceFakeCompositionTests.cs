using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Fake-composition tests for baseline application-service orchestration.
[TestFixture]
public sealed class ArchitectureBaselineApplicationServiceFakeCompositionTests
{
    private static readonly string[] KnownRule = { "known-rule" };

    [Test]
    public void Generate_FakeCollaborators_ProducesBaselineWithoutRealInfrastructure()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document));
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator, new FakeBaselineLoadingService());

        BaselineGenerationOutcome outcome = applicationService.Generate(
            new BaselineGenerationRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                Mode = "all",
                Reason = "fake reason",
            });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.Yaml, Is.EqualTo("fake-baseline-yaml"));
        Assert.That(baselineGenerator.WasCalled, Is.True);
        Assert.That(baselineGenerator.ReasonReceived, Is.EqualTo("fake reason"));
        Assert.That(contractExecutor.ModesReceived, Is.EquivalentTo(new[] { "strict", "audit" }));
        Assert.That(runner.StrictArgumentsReceived, Is.EqualTo(new[] { true }));
        Assert.That(runnerSetupService.ModeReceived, Is.Null);
    }

    [Test]
    public void Generate_ConfigurationViolationsPresent_ShortCircuitsBeforeExecutorOrGenerator()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            ConfigurationViolationsToReturn = new List<ArchitectureViolation>
            {
                new("<configuration>", null, "fake-subject", "fake-configuration-violation", new[] { "fake" }),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator, new FakeBaselineLoadingService());

        BaselineGenerationOutcome outcome = applicationService.Generate(
            new BaselineGenerationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "all" });

        Assert.That(outcome.Succeeded, Is.False);
        Assert.That(outcome.ConfigurationViolations, Has.Count.EqualTo(1));
        Assert.That(contractExecutor.ModesReceived, Is.Empty);
        Assert.That(baselineGenerator.WasCalled, Is.False);
        Assert.That(runner.StrictArgumentsReceived, Is.EqualTo(new[] { true }));
    }

    [Test]
    public void Generate_InvalidMode_ThrowsWithoutCallingAnyCollaborator()
    {
        var runnerSetupService = new FakeRunnerSetupService();
        var handlerRegistry = new FakeContractHandlerRegistry();
        var contractExecutor = new FakeContractExecutor();
        var baselineGenerator = new FakeBaselineGenerator();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, handlerRegistry, contractExecutor, baselineGenerator, new FakeBaselineLoadingService());

        Assert.That(
            () => applicationService.Generate(
                new BaselineGenerationRequest { PolicyPath = "unused-by-fakes.arch.yml", Mode = "not-a-real-mode" }),
            Throws.ArgumentException);

        Assert.That(runnerSetupService.LoadDocumentCalled, Is.False);
        Assert.That(runnerSetupService.BuildRunnerCalled, Is.False);
        Assert.That(contractExecutor.ModesReceived, Is.Empty);
        Assert.That(baselineGenerator.WasCalled, Is.False);
    }

    [Test]
    public void Generate_UnknownSelectedContract_ThrowsBeforeRunnerSetup()
    {
        var document = CreateDocumentWithKnownRule();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document, RunnerToReturn = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document)) };
        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService,
            new FakeContractHandlerRegistry(),
            new FakeContractExecutor(),
            new FakeBaselineGenerator(),
            new FakeBaselineLoadingService());

        var ex = Assert.Throws<InvalidOperationException>(() => applicationService.Generate(
            new BaselineGenerationRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                Mode = "all",
                ContractIds = new[] { "missing-rule" },
            }));

        Assert.That(ex!.Message, Does.Contain("Unknown contract IDs"));
        Assert.That(ex.Message, Does.Contain("missing-rule"));
        Assert.That(runnerSetupService.BuildRunnerCalled, Is.False);
    }

    [Test]
    public void Verify_AuditMode_UsesAuditConfigurationScopeAndPassesModeToRunnerSetup()
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
            DocumentToReturn = new ArchitectureBaselineDocument
            {
                Version = 1,
                Baseline = new ArchitectureBaselineContractGroups
                {
                    Audit = new List<ArchitectureBaselineContractEntry>
                    {
                        new()
                        {
                            Id = "audit-rule",
                            IgnoredViolations = new List<ArchitectureIgnoredViolation>
                            {
                                new() { SourceType = "SrcY", ForbiddenReference = "RefY", Reason = "audit reason" },
                            },
                        },
                    },
                },
            },
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), new FakeBaselineGenerator(), baselineLoadingService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "audit",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.InSync, Is.True);
        Assert.That(runner.StrictArgumentsReceived, Is.EqualTo(new[] { false }));
        Assert.That(runnerSetupService.ModeReceived, Is.EqualTo("audit"));
    }

    // Mixed baseline: frozen + resolved + configuration-error + new candidate.
    private static ArchitectureContractDocument CreateDocumentWithKnownRule()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "known-rule", Name = "known-rule", Source = "core" },
                },
            },
        };
    }

    private static ArchitectureBaselineDocument CreateMixedBaseline()
    {
        return new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "known-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcA", ForbiddenReference = "RefA", Reason = "old reason A" },
                            new() { SourceType = "SrcB", ForbiddenReference = "RefB", Reason = "old reason B" },
                        },
                    },
                    new()
                    {
                        Id = "unknown-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcC", ForbiddenReference = "RefC", Reason = "old reason C" },
                        },
                    },
                },
            },
        };
    }

    private static (FakeRunnerSetupService RunnerSetupService, FakeContractExecutor ContractExecutor,
        FakeBaselineGenerator BaselineGenerator, FakeBaselineLoadingService BaselineLoadingService)
        CreateMixedScenarioCollaborators()
    {
        var document = CreateDocumentWithKnownRule();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
                new("strict", "known-rule", "SrcNew", "RefNew"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;

        return (runnerSetupService, new FakeContractExecutor(), new FakeBaselineGenerator(),
            new FakeBaselineLoadingService { DocumentToReturn = CreateMixedBaseline() });
    }

    [Test]
    public void Update_PreservesFrozenReasonAndAddsNewEntry()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineUpdateOutcome outcome = applicationService.Update(new BaselineUpdateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            Reason = "freshly generated",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.PreservedCount, Is.EqualTo(1));
        Assert.That(outcome.NewCount, Is.EqualTo(1));

        var entries = baselineGenerator.EntriesReceived!;
        Assert.That(entries, Has.Count.EqualTo(4));
        Assert.That(entries.Single(e => e.SourceType == "SrcA").Reason, Is.EqualTo("old reason A"));
        Assert.That(entries.Single(e => e.SourceType == "SrcB").Reason, Is.EqualTo("old reason B"));
        Assert.That(entries.Single(e => e.SourceType == "SrcC").Reason, Is.EqualTo("old reason C"));
        Assert.That(entries.Single(e => e.SourceType == "SrcNew").Reason, Is.EqualTo("freshly generated"));
    }

    [Test]
    public void Prune_RemovesResolvedAndConfigurationErrorEntriesOnly()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselinePruneOutcome outcome = applicationService.Prune(new BaselinePruneRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(baselineGenerator.EntriesReceived, Has.Count.EqualTo(1));
        Assert.That(baselineGenerator.EntriesReceived!.Single().SourceType, Is.EqualTo("SrcA"));

        Assert.That(outcome.RemovedEntries, Has.Count.EqualTo(2));
        Assert.That(outcome.RemovedEntries.Single(r => r.Entry.SourceType == "SrcB").RemovalReason, Is.EqualTo("resolved"));
        Assert.That(outcome.RemovedEntries.Single(r => r.Entry.SourceType == "SrcC").RemovalReason, Is.EqualTo("configuration-error"));
    }

    [Test]
    public void Diff_ReportsAllFourCategories()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineDiffOutcome outcome = applicationService.Diff(new BaselineDiffRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.New.Single().SourceType, Is.EqualTo("SrcNew"));
        Assert.That(outcome.Frozen.Single().SourceType, Is.EqualTo("SrcA"));
        Assert.That(outcome.Resolved.Single().SourceType, Is.EqualTo("SrcB"));
        Assert.That(outcome.ConfigurationErrors.Single().SourceType, Is.EqualTo("SrcC"));
    }

    [Test]
    public void Verify_OutOfSync_WhenResolvedOrConfigurationErrorsExist()
    {
        (var runnerSetupService, var contractExecutor, var baselineGenerator, var baselineLoadingService) =
            CreateMixedScenarioCollaborators();

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), contractExecutor, baselineGenerator, baselineLoadingService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.InSync, Is.False);
    }

    [Test]
    public void Verify_InSync_WhenOnlyNewDebtPresent()
    {
        var document = CreateDocumentWithKnownRule();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
                new("strict", "known-rule", "SrcNew", "RefNew"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;

        var baselineLoadingService = new FakeBaselineLoadingService
        {
            DocumentToReturn = new ArchitectureBaselineDocument
            {
                Version = 1,
                Baseline = new ArchitectureBaselineContractGroups
                {
                    Strict = new List<ArchitectureBaselineContractEntry>
                    {
                        new()
                        {
                            Id = "known-rule",
                            IgnoredViolations = new List<ArchitectureIgnoredViolation>
                            {
                                new() { SourceType = "SrcA", ForbiddenReference = "RefA", Reason = "old reason A" },
                            },
                        },
                    },
                },
            },
        };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), new FakeBaselineGenerator(), baselineLoadingService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.InSync, Is.True);
        Assert.That(outcome.New.Single().SourceType, Is.EqualTo("SrcNew"));
    }

    // Scoped update/prune must preserve out-of-scope entries untouched.
    private static ArchitectureContractDocument CreateDocumentWithTwoContractRules()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "known-rule", Name = "known-rule", Source = "core" },
                    new() { Id = "other-rule", Name = "other-rule", Source = "core" },
                },
            },
        };
    }

    private static ArchitectureBaselineDocument CreateBaselineWithTwoContractRules()
    {
        return new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "known-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcA", ForbiddenReference = "RefA", Reason = "old reason A" },
                        },
                    },
                    new()
                    {
                        Id = "other-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcX", ForbiddenReference = "RefX", Reason = "old reason X" },
                        },
                    },
                },
            },
        };
    }

    [Test]
    public void Update_ScopedToOneContract_PreservesOutOfScopeContractEntries()
    {
        var document = CreateDocumentWithTwoContractRules();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var baselineGenerator = new FakeBaselineGenerator();
        var baselineLoadingService = new FakeBaselineLoadingService { DocumentToReturn = CreateBaselineWithTwoContractRules() };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), baselineGenerator, baselineLoadingService);

        BaselineUpdateOutcome outcome = applicationService.Update(new BaselineUpdateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            ContractIds = KnownRule,
        });

        Assert.That(outcome.Succeeded, Is.True);
        var entries = baselineGenerator.EntriesReceived!;
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Single(e => e.ContractId == "known-rule").SourceType, Is.EqualTo("SrcA"));
        Assert.That(entries.Single(e => e.ContractId == "other-rule").SourceType, Is.EqualTo("SrcX"));
        Assert.That(entries.Single(e => e.ContractId == "other-rule").Reason, Is.EqualTo("old reason X"));
    }

    [Test]
    public void Prune_ScopedToOneContract_PreservesOutOfScopeContractEntriesEvenIfStale()
    {
        var document = CreateDocumentWithTwoContractRules();
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcA", "RefA"),
            },
        };
        runnerSetupService.RunnerToReturn = runner;
        var baselineGenerator = new FakeBaselineGenerator();
        var baselineLoadingService = new FakeBaselineLoadingService { DocumentToReturn = CreateBaselineWithTwoContractRules() };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), baselineGenerator, baselineLoadingService);

        BaselinePruneOutcome outcome = applicationService.Prune(new BaselinePruneRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            ContractIds = KnownRule,
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.RemovedEntries, Is.Empty);
        var entries = baselineGenerator.EntriesReceived!;
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Single(e => e.ContractId == "known-rule").SourceType, Is.EqualTo("SrcA"));
        Assert.That(entries.Single(e => e.ContractId == "other-rule").SourceType, Is.EqualTo("SrcX"));
    }

    private static ArchitectureContractDocument CreateDocumentWithStrictAndAuditRules()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "known-rule", Name = "known-rule", Source = "core" },
                },
                Audit = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "audit-rule", Name = "audit-rule", Source = "core" },
                },
            },
        };
    }

    private static ArchitectureBaselineDocument CreateBaselineWithStrictAndAuditEntries()
    {
        return new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "known-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcA", ForbiddenReference = "RefA", Reason = "old reason A" },
                        },
                    },
                },
                Audit = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "audit-rule",
                        IgnoredViolations = new List<ArchitectureIgnoredViolation>
                        {
                            new() { SourceType = "SrcY", ForbiddenReference = "RefY", Reason = "old reason Y" },
                        },
                    },
                },
            },
        };
    }

    [Test]
    public void Update_ScopedToStrictMode_PreservesAuditBaselineEntries()
    {
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
        var baselineGenerator = new FakeBaselineGenerator();
        var baselineLoadingService = new FakeBaselineLoadingService { DocumentToReturn = CreateBaselineWithStrictAndAuditEntries() };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), baselineGenerator, baselineLoadingService);

        BaselineUpdateOutcome outcome = applicationService.Update(new BaselineUpdateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "strict",
        });

        Assert.That(outcome.Succeeded, Is.True);
        var entries = baselineGenerator.EntriesReceived!;
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Single(e => e.ContractGroup == "audit").SourceType, Is.EqualTo("SrcY"));
        Assert.That(entries.Single(e => e.ContractGroup == "audit").Reason, Is.EqualTo("old reason Y"));
    }

    [Test]
    public void Prune_ScopedToStrictMode_PreservesAuditBaselineEntriesEvenIfStale()
    {
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
        var baselineGenerator = new FakeBaselineGenerator();
        var baselineLoadingService = new FakeBaselineLoadingService { DocumentToReturn = CreateBaselineWithStrictAndAuditEntries() };

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(), baselineGenerator, baselineLoadingService);

        BaselinePruneOutcome outcome = applicationService.Prune(new BaselinePruneRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "strict",
        });

        Assert.That(outcome.Succeeded, Is.True);
        Assert.That(outcome.RemovedEntries, Is.Empty);
        var entries = baselineGenerator.EntriesReceived!;
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Single(e => e.ContractGroup == "audit").SourceType, Is.EqualTo("SrcY"));
    }
}
