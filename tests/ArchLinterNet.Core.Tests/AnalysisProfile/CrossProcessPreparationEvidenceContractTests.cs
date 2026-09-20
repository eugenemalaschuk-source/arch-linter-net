using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class CrossProcessPreparationEvidenceContractTests
{
    [Test]
    public void Evidence_RoundTripsDeterministicallyAndRetainsRawProfiles()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence();

        string first = CrossProcessPreparationEvidenceJson.Serialize(evidence);
        CrossProcessPreparationEvidenceDocument roundTrip = CrossProcessPreparationEvidenceJson.Deserialize(first);
        string second = CrossProcessPreparationEvidenceJson.Serialize(roundTrip);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second, Does.Contain("cross-process-preparation-evidence/v1"));
            Assert.That(second, Does.Contain("analysis-profile/v1"));
            Assert.That(roundTrip.BenchmarkEvidence.EvidenceSchemaId, Is.EqualTo(BenchmarkEvidenceDocument.SchemaId));
            Assert.That(roundTrip.Processes, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void ProcessIdentity_SeparatesCommandProjectionExecutionAndRevisionRoles()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence();

        Assert.Multiple(() =>
        {
            Assert.That(evidence.Processes[0].Identity.Projection.CommandFamily, Is.EqualTo("strict"));
            Assert.That(evidence.Processes[1].Identity.ExecutionKind, Is.EqualTo(PreparationExecutionKind.InProcessProjection));
            Assert.That(evidence.Processes[2].Identity.Projection.CommandFamily, Is.EqualTo("architecture_health"));
            Assert.That(evidence.Processes[3].Identity.RevisionRole, Is.EqualTo(PreparationRevisionRole.Base));
            Assert.That(evidence.Processes[2].Identity.ExecutionKind, Is.EqualTo(PreparationExecutionKind.InProcessProjection));
            Assert.That(evidence.CandidateIndependentProcesses, Has.Count.EqualTo(1));
            Assert.That(evidence.PreparedEffect.RepresentativeProcessCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void PreparedEffect_UsesStrictBreakEvenAndSeparatesCacheAvoidableWork()
    {
        PreparedEffectContract effect = CreateEffect(representativeProcessCount: 1);

        Assert.Multiple(() =>
        {
            Assert.That(effect.CalculateBreakEvenProcessCount(), Is.EqualTo(2));
            Assert.That(effect.BreakEvenProcessCount, Is.EqualTo(2));
            Assert.That(effect.IndependentOneShotCost(3), Is.EqualTo(150m));
            Assert.That(effect.PreparedReuseCost(3), Is.EqualTo(80m));
            Assert.That(effect.ExpectedSavings(3), Is.EqualTo(70m));
            Assert.That(effect.PreparedStateOnlyWork, Is.EqualTo(70));
            Assert.That(effect.ExactCacheHitSavingsExcluded, Is.True);
            Assert.That(effect.OneProcessWorkEvidenceComplete, Is.True);
            Assert.That(effect.MissingOneProcessWorkEvidenceFamilies, Is.Empty);
        });
    }

    [Test]
    public void BaseRevisionPreparation_IsExcludedFromCandidateSavings()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence();
        CrossProcessProcessEvidence baseProcess = evidence.Processes.Single(process =>
            process.Identity.RevisionRole == PreparationRevisionRole.Base);

        Assert.Multiple(() =>
        {
            Assert.That(baseProcess.Identity.RevisionRole, Is.EqualTo(PreparationRevisionRole.Base));
            Assert.That(evidence.CandidateIndependentProcesses, Does.Not.Contain(baseProcess));
            Assert.That(evidence.PreparedEffect.RepresentativeProcessCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ResourceAvailability_RetainsUnavailableReasonsWithoutInventingValues()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence();
        string json = CrossProcessPreparationEvidenceJson.Serialize(evidence);
        CrossProcessPreparationEvidenceDocument roundTrip = CrossProcessPreparationEvidenceJson.Deserialize(json);
        PreparationResourceEvidence resources = roundTrip.PreparedEffect.Resources;

        Assert.Multiple(() =>
        {
            Assert.That(resources.StorageBytes.Status, Is.EqualTo(BenchmarkMeasurementStatus.Unavailable));
            Assert.That(resources.StorageBytes.Value, Is.Null);
            Assert.That(resources.StorageBytes.Reason, Does.Contain("Synthetic"));
            Assert.That(resources.IoOperations.Status, Is.EqualTo(BenchmarkMeasurementStatus.NotApplicable));
        });
    }

    [TestCase(PreparationDecisionOutcome.B)]
    [TestCase(PreparationDecisionOutcome.C)]
    public void DeferredOrRoutedDecisions_RequireExplicitReason(PreparationDecisionOutcome outcome)
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            Decision = new PreparationDecision
            {
                Outcome = outcome,
                Route = outcome == PreparationDecisionOutcome.B ? "defer-prepared-analysis" : "route-to-one-process",
                Reason = "Synthetic decision requires additional evidence.",
                OneProcessAlternativeEvaluated = true,
                BreakEvenObserved = false,
            },
        };

        Assert.That(() => evidence.Validate(), Throws.Nothing);
    }

    [Test]
    public void OutcomeA_FailsClosedWithoutOneProcessAndBreakEvenEvidence()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            Decision = new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.A,
                Route = "prepared-analysis-implementation",
                Reason = "Synthetic contract deliberately omits required gates.",
                OneProcessAlternativeEvaluated = false,
                BreakEvenObserved = false,
            },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void OutcomeA_FailsClosedWithoutBoundedResourceTradeOffEvidence()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            PreparedEffect = CreateEffect(1) with { Resources = UnavailableResources() },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void OutcomeA_FailsClosedBelowMaterialSavingsThreshold()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            PreparedEffect = CreateEffect(1) with
            {
                MaterialSavingsThreshold = 0.60m,
            },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void MaterialSavingsRatio_MustBeDerivedFromMeasuredCosts()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            PreparedEffect = CreateEffect(1) with
            {
                MeasuredMaterialSavingsRatio = 0.10m,
            },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void IncompleteOneProcessWorkEvidence_CannotClaimMeasuredAlternativeOrDistinctValue()
    {
        PreparedEffectContract effect = CreateEffect(1) with
        {
            MeasuredOneProcessAlternativeWork = null,
            OneProcessWorkEvidenceComplete = false,
            MissingOneProcessWorkEvidenceFamilies = ["measure"],
            DistinctCrossProcessValue = false,
            MeasuredMaterialSavingsRatio = 0,
        };

        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            Workflow = CreateEvidence().Workflow with { OneProcessAlternativeMeasured = false },
            PreparedEffect = effect,
            Decision = new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.C,
                Route = "route-instrumentation-gap",
                Reason = "Synthetic contract records missing process-bound work evidence.",
                OneProcessAlternativeEvaluated = false,
                BreakEvenObserved = false,
            },
        };

        Assert.That(() => evidence.Validate(), Throws.Nothing);
    }

    [Test]
    public void EquivalentModes_RejectDifferentCanonicalResults()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence();
        BenchmarkCanonicalResultIdentity different = BenchmarkIdentity.CreateCanonicalResult(
            "Success",
            0,
            [new BenchmarkFindingIdentity
            {
                ContractId = "synthetic-contract",
                Kind = "synthetic-finding",
                SourceAssembly = "synthetic-assembly",
                SourceType = "Synthetic.Type",
                SourceMember = "Synthetic.Member",
                Location = "src/Synthetic.cs",
            }]);
        CrossProcessProcessEvidence altered = evidence.Processes[1] with { CanonicalResult = different };
        evidence = evidence with { Processes = [evidence.Processes[0], altered, evidence.Processes[2], evidence.Processes[3]] };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void OneProcessAlternative_RejectsAnUncoveredMeasuredCommandFamily()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            Workflow = CreateEvidence().Workflow with
            {
                MeasuredCommandFamilies = ["strict", "architecture_health", "measure"],
            },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    [Test]
    public void PrivacySafeIdentity_RejectsPrivateAdopterTokens()
    {
        CrossProcessPreparationEvidenceDocument evidence = CreateEvidence() with
        {
            Workflow = CreateEvidence().Workflow with
            {
                CandidateRevision = new PreparationRevisionIdentity
                {
                    Role = PreparationRevisionRole.Candidate,
                    Identity = "private-adopter-branch",
                },
            },
        };

        Assert.That(() => evidence.Validate(), Throws.InvalidOperationException);
    }

    private static CrossProcessPreparationEvidenceDocument CreateEvidence()
    {
        BenchmarkWorkloadDefinition workload = BenchmarkWorkloadGenerator.Create(
            "synthetic-cross-process-contract",
            BenchmarkTopologyShape.Diamond,
            new BenchmarkDimensionSet { ProjectCount = 4, FindingCandidates = 1 },
            BenchmarkCompilationMode.StagedAssemblies,
            BenchmarkExecutionMode.MultiCommand,
            independentProcesses: 1);

        JsonElement profile = JsonDocument.Parse("""
            {
              "SchemaId": "analysis-profile/v1",
              "CompletionStatus": "Success",
              "Counters": {},
              "Phases": [],
              "Output": { "OutputFailed": false }
            }
            """).RootElement.Clone();

        BenchmarkCanonicalResultIdentity canonical = BenchmarkIdentity.CreateCanonicalResult("Success", 0, []);
        BenchmarkProfileSample[] samples =
        [
            CreateSample(profile, 1),
            CreateSample(profile, 2),
            CreateSample(profile, 3),
            CreateSample(profile, 4),
        ];
        PreparationProjectionIdentity strict = new() { CommandFamily = "strict", ProjectionId = "synthetic-strict-candidate", ComparisonGroup = "strict-validation", ProcessBound = true };
        PreparationProjectionIdentity strictInProcess = new() { CommandFamily = "strict", ProjectionId = "synthetic-strict-in-process", ComparisonGroup = "strict-validation", ProcessBound = false };
        PreparationProjectionIdentity health = new() { CommandFamily = "architecture_health", ProjectionId = "synthetic-health-candidate", ComparisonGroup = "health-process-bound", ProcessBound = false };
        PreparationProjectionIdentity baseProjection = new() { CommandFamily = "strict", ProjectionId = "synthetic-strict-base", ComparisonGroup = "strict-base", ProcessBound = true };
        CrossProcessPreparationWorkflow workflow = new()
        {
            EvidenceSchemaId = CrossProcessPreparationWorkflow.SchemaId,
            WorkloadIdentity = workload.WorkloadIdentity,
            PreparationBoundary = PreparationBoundaryKind.StagedAssemblies,
            CandidateRevision = new PreparationRevisionIdentity { Role = PreparationRevisionRole.Candidate, Identity = "synthetic-candidate-revision" },
            BaseRevision = new PreparationRevisionIdentity { Role = PreparationRevisionRole.Base, Identity = "synthetic-base-revision" },
            Projections = [strict, strictInProcess, health, baseProjection],
            MeasuredCommandFamilies = ["strict", "architecture_health"],
            CacheModesMeasured = ["disabled", "miss", "hit"],
            OneProcessAlternativeMeasured = true,
        };
        CrossProcessProcessEvidence[] processes =
        [
            CreateProcess(strict, 1, PreparationRevisionRole.Candidate, PreparationExecutionKind.IndependentProcess, samples[0], canonical),
            CreateProcess(strictInProcess, 2, PreparationRevisionRole.Candidate, PreparationExecutionKind.InProcessProjection, samples[1], canonical),
            CreateProcess(health, 3, PreparationRevisionRole.Candidate, PreparationExecutionKind.InProcessProjection, samples[2], canonical),
            CreateProcess(baseProjection, 4, PreparationRevisionRole.Base, PreparationExecutionKind.IndependentProcess, samples[3], canonical),
        ];
        BenchmarkEvidenceDocument benchmarkEvidence = BenchmarkEvidenceFactory.Create(
            workload,
            profile,
            canonical,
            sample: samples[0]) with
        {
            Samples = samples,
            Run = samples[0].Run,
        };

        return new CrossProcessPreparationEvidenceDocument
        {
            EvidenceSchemaId = CrossProcessPreparationEvidenceDocument.SchemaId,
            BenchmarkEvidence = benchmarkEvidence,
            Workflow = workflow,
            Processes = processes,
            PreparedEffect = CreateEffect(1),
            Decision = new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.A,
                Route = "prepared-analysis-implementation",
                Reason = "Synthetic contract records measured one-process and break-even gates.",
                OneProcessAlternativeEvaluated = true,
                BreakEvenObserved = true,
            },
        };
    }

    private static PreparedEffectContract CreateEffect(int representativeProcessCount) => new()
    {
        IssueReference = "#493",
        RepresentativeProcessCount = representativeProcessCount,
        RepeatedWorkShare = 0.5m,
        CandidatePreparedBoundaryWork = 100,
        CacheAvoidableWork = 30,
        PreparedStateAvoidableWork = 70,
        ColdPrepareCost = 50m,
        PerConsumerLoadAuthorizationCost = 10m,
        PerConsumerLoadAuthorizationCostLowerBound = 10m,
        PerConsumerLoadAuthorizationCostUpperBound = 10m,
        PerConsumerLoadAuthorizationCostBasis = "Synthetic contract projection measurement.",
        UnavoidableProjectionWork = 0m,
        MeasuredIndependentWorkflowWork = 100m,
        MeasuredOneProcessAlternativeWork = 120m,
        OneProcessWorkEvidenceComplete = true,
        MissingOneProcessWorkEvidenceFamilies = [],
        ExpectedPersistedReuseWork = 60m,
        DistinctCrossProcessValue = true,
        MaterialSavingsThreshold = 0.10m,
        MeasuredMaterialSavingsRatio = 0.5m,
        WorkMeasurementBasis = "Synthetic contract counters.",
        BreakEvenProcessCount = 2,
        CacheModesMeasured = ["disabled", "miss", "hit"],
        Resources = BoundedResources(),
        ScaleEvidenceBasis = "Synthetic measured scale matrix for the contract test.",
        ScaleEvidence =
        [
            SyntheticScalePoint("small", 50m, 1.0m),
            SyntheticScalePoint("medium", 50m, 1.2m),
            SyntheticScalePoint("large", 50m, 1.5m),
        ],
        ExpectedEffect = new BenchmarkExpectedEffectEvidence
        {
            IssueReference = "#493",
            TargetPhase = "candidate_preparation",
            BaselinePhaseShare = 0.5m,
            CurrentWorkModel = "R x one-shot preparation",
            TargetWorkModel = "prepare + R x load/authorization",
            ExpectedLocalSpeedupSmall = 1.0m,
            ExpectedLocalSpeedupMedium = 1.2m,
            ExpectedLocalSpeedupLarge = 1.5m,
            ExpectedEndToEndUpperBound = 1.3m,
            MemoryAllocationTradeOff = "Synthetic resource availability is explicit; no private machine value is claimed.",
            ColdPathTradeOff = "Cold preparation remains measured separately from reuse.",
            SuccessThreshold = "Prepared-state cost is lower after the recorded break-even point.",
            KillCriterion = "Defer when canonical results differ or no break-even is observed.",
            Confidence = "synthetic deterministic contract",
        },
        ExactCacheHitSavingsExcluded = true,
    };

    private static PreparedEffectScalePoint SyntheticScalePoint(
        string label,
        decimal coldPrepareCost,
        decimal expectedLocalSpeedup)
    {
        return new PreparedEffectScalePoint
        {
            Label = label,
            WorkloadId = $"synthetic-scale-{label}",
            WorkloadIdentity = new string('0', 64),
            ProjectCount = 1,
            TypeCount = 1,
            SourceFileCount = 1,
            ReferenceEdgeCount = 0,
            CommandCount = 1,
            IndependentPreparationWork = coldPrepareCost,
            IndependentProjectionWork = 0,
            ColdPrepareCost = coldPrepareCost,
            ExpectedLocalSpeedup = expectedLocalSpeedup,
            MeasurementBasis = "Synthetic measured analysis-profile counters.",
        };
    }

    private static BenchmarkProfileSample CreateSample(JsonElement profile, int ordinal) => new()
    {
        Run = new BenchmarkRunDescriptor
        {
            ExecutionMode = BenchmarkExecutionMode.MultiCommand,
            CacheMode = "disabled",
            PreparedStateMode = "unprepared",
            ParallelMode = "sequential",
            SampleOrdinal = ordinal,
            IsWarmSample = false,
        },
        RawAnalysisProfile = profile,
        CompletionStatus = "Success",
        ExitCode = 0,
        OutputFailed = false,
        WallClock = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no wall-clock measurement."),
        ProcessorTime = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no processor-time measurement."),
        AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no allocation measurement."),
        PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Synthetic contract sample has no memory measurement."),
    };

    private static CrossProcessProcessEvidence CreateProcess(
        PreparationProjectionIdentity projection,
        int ordinal,
        PreparationRevisionRole revisionRole,
        PreparationExecutionKind executionKind,
        BenchmarkProfileSample sample,
        BenchmarkCanonicalResultIdentity canonical) => new()
        {
            Identity = new PreparationProcessIdentity
            {
                Projection = projection,
                ProcessOrdinal = ordinal,
                RevisionRole = revisionRole,
                ExecutionKind = executionKind,
            },
            Sample = sample,
            CanonicalResult = canonical,
            Resources = UnavailableResources(),
        };

    private static PreparationResourceEvidence UnavailableResources() => new()
    {
        StorageBytes = BenchmarkResourceMeasurement.Unavailable("Synthetic contract does not measure storage bytes."),
        IoOperations = new BenchmarkResourceMeasurement
        {
            Status = BenchmarkMeasurementStatus.NotApplicable,
            Reason = "Synthetic contract has no storage-I/O counter.",
        },
        AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Synthetic contract does not measure prepared-state allocations."),
        PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Synthetic contract does not measure prepared-state memory."),
    };

    private static PreparationResourceEvidence BoundedResources() => UnavailableResources() with
    {
        StorageBound = SyntheticBound("bytes"),
        IoOperationsBound = SyntheticBound("operations"),
        AllocatedBytesBound = SyntheticBound("bytes"),
        PeakManagedMemoryBound = SyntheticBound("bytes"),
    };

    private static PreparationResourceBound SyntheticBound(string unit) => new()
    {
        LowerBound = 1,
        UpperBound = 4,
        Unit = unit,
        Basis = "Synthetic bounded resource sensitivity fixture.",
        Uncertainty = "Synthetic fixture range.",
    };
}
