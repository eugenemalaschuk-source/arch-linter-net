using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Same fake-service-composition style as ArchitectureValidationApplicationServiceFakeCompositionTests:
// ArchitectureBaselineApplicationService's four collaborators are all interfaces, so baseline
// generation's request validation, configuration-violation short-circuit, and strict/audit mode
// selection can be proven without a real runner, executor, or YAML serializer.
[TestFixture]
public sealed class ArchitectureBaselineApplicationServiceFakeCompositionTests
{
    private sealed class FakeRunnerSetupService : IArchitectureRunnerSetupService
    {
        public bool LoadDocumentCalled { get; private set; }

        public bool BuildRunnerCalled { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            LoadDocumentCalled = true;
            return DocumentToReturn;
        }

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null)
        {
            BuildRunnerCalled = true;
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }
    }

    private sealed class FakeContractRunner : IArchitectureContractRunner
    {
        public FakeContractRunner(ArchitectureAnalysisSession session)
        {
            Session = session;
        }

        public List<ArchitectureViolation> ConfigurationViolationsToReturn { get; set; } = new();

        public ArchitectureAnalysisSession Session { get; }

        public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations { get; }
            = Array.Empty<ArchitectureUnmatchedIgnoredViolation>();

        public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates { get; set; }
            = Array.Empty<ArchitectureBaselineCandidate>();

        public List<ArchitectureViolation> CheckConfiguration()
        {
            return CheckConfiguration(strict: true);
        }

        public List<ArchitectureViolation> CheckConfiguration(bool strict)
        {
            return ConfigurationViolationsToReturn;
        }

        public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency()
        {
            return new List<PolicyConsistencyDiagnostic>();
        }
    }

    private sealed class FakeContractHandlerRegistry : IArchitectureContractHandlerRegistry
    {
        public bool TryGetHandler(string family, out IArchitectureContractHandler? handler)
        {
            handler = null;
            return false;
        }

        public ArchitectureHandlerResult Execute(
            string family, ArchitectureAnalysisSession session, IArchitectureContract contract)
        {
            throw new InvalidOperationException("Not expected to be called directly by the application service.");
        }
    }

    private sealed class FakeContractExecutor : IArchitectureContractExecutor
    {
        public List<string> ModesReceived { get; } = new();

        public ArchitectureContractExecutionResult Execute(
            ArchitectureAnalysisSession session,
            string mode,
            IArchitectureContractHandlerRegistry handlerRegistry,
            bool includeAsmdefContracts = true,
            ValidationTiming? timing = null)
        {
            ModesReceived.Add(mode);
            return new ArchitectureContractExecutionResult(
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<string>(),
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<ArchitectureCoverageSummary>());
        }
    }

    private sealed class FakeBaselineGenerator : IArchitectureBaselineGenerator
    {
        public bool WasCalled { get; private set; }

        public string ReasonReceived { get; private set; } = string.Empty;

        public string YamlToReturn { get; set; } = "fake-baseline-yaml";

        public IReadOnlyList<ArchitectureBaselineComparisonEntry>? EntriesReceived { get; private set; }

        public ArchitectureBaselineDocument Generate(
            ArchitectureContractDocument policyDocument,
            IReadOnlyList<ArchitectureBaselineCandidate> candidates,
            string reason = "generated baseline")
        {
            WasCalled = true;
            ReasonReceived = reason;
            return new ArchitectureBaselineDocument { Version = 1 };
        }

        public ArchitectureBaselineDocument BuildFromEntries(IReadOnlyList<ArchitectureBaselineComparisonEntry> entries)
        {
            WasCalled = true;
            EntriesReceived = entries;
            return new ArchitectureBaselineDocument { Version = 1 };
        }

        public string Serialize(ArchitectureBaselineDocument document)
        {
            return YamlToReturn;
        }
    }

    private sealed class FakeBaselineLoadingService : IArchitectureBaselineLoadingService
    {
        public ArchitectureBaselineDocument DocumentToReturn { get; set; } =
            new() { Version = 1, Baseline = new ArchitectureBaselineContractGroups() };

        public void LoadAndMerge(ArchitectureContractDocument document, string baselinePath)
        {
        }

        public ArchitectureBaselineDocument Load(string baselinePath)
        {
            return DocumentToReturn;
        }
    }

    private static ArchitectureAnalysisSession CreateEmptySession(ArchitectureContractDocument document)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            Array.Empty<System.Reflection.Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
    }

    [Test]
    public void Generate_FakeCollaborators_ProducesBaselineWithoutRealInfrastructure()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document));
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
    }

    [Test]
    public void Generate_ConfigurationViolationsPresent_ShortCircuitsBeforeExecutorOrGenerator()
    {
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var runnerSetupService = new FakeRunnerSetupService { DocumentToReturn = document };
        var runner = new FakeContractRunner(CreateEmptySession(document))
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

    // Update/Prune/Diff/Verify all build on the same classification: a "known-rule" entry whose
    // (source, forbidden) still matches a candidate is frozen; a "known-rule" entry with no matching
    // candidate is resolved (stale); an "unknown-rule" entry is a configuration error; a candidate with
    // no matching baseline entry is new.
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
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

    // Scoped update/prune must only classify (and potentially remove) entries within the
    // requested --contract/--mode scope; entries outside that scope must be carried through
    // to the output baseline untouched, even if they would otherwise be "resolved".
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
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
            ContractIds = new[] { "known-rule" },
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
        {
            // No candidate for "other-rule" — its baseline entry would be classified "resolved"
            // if it were in scope, but --contract known-rule must keep it untouched.
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
            ContractIds = new[] { "known-rule" },
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
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
        var runner = new FakeContractRunner(CreateEmptySession(document))
        {
            // No candidate for the audit rule — it would be "resolved" if audit were in scope,
            // but --mode strict must leave the audit group untouched.
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
