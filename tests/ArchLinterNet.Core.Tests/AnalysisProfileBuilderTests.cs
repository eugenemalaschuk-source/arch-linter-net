using System.Diagnostics;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #374: AnalysisProfileBuilder assembles the analysis-profile/v1 contract from existing
// ArchitectureAnalysisSnapshotCounters/ValidationTiming instrumentation. See
// openspec/specs/analysis-profile/spec.md.
[TestFixture]
public sealed class AnalysisProfileBuilderTests
{
    private static ArchitectureAnalysisSnapshotCounters Counters(int modes = 1) => new()
    {
        PolicyCompositions = 1,
        ProjectGraphEvaluations = 1,
        AssemblyLoads = 1,
        DiscoveredProjectCount = 3,
        RetainedAssemblyCount = 2,
        SelectedAssemblyCount = 3,
        ModesEvaluated = modes,
        SnapshotMaterializations = 1,
        FactIndexMaterializations = 1,
        SourceScanPasses = 1,
        SourceFilesScanned = 7,
        ContractFamilyResultCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["dependency"] = 4,
        },
    };

    private static ValidationTiming TimingWithContractFamilies()
    {
        var timing = new ValidationTiming();
        using (timing.Measure("total")) { }
        using (timing.MeasureContractFamily("dependency", () => 5)) { }
        using (timing.MeasureContractFamily("coverage", () => 2)) { }
        return timing;
    }

    [Test]
    public void Build_CountersIdentical_WhetherOrNotTimingSupplied()
    {
        ArchitectureAnalysisSnapshotCounters snapshotCounters = Counters();

        AnalysisProfile withTiming = AnalysisProfileBuilder.Build(
            snapshotCounters, TimingWithContractFamilies(), renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);
        AnalysisProfile withoutTiming = AnalysisProfileBuilder.Build(
            snapshotCounters, timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(withTiming.Counters.PolicyCompositions, Is.EqualTo(withoutTiming.Counters.PolicyCompositions));
            Assert.That(withTiming.Counters.ProjectGraphEvaluations, Is.EqualTo(withoutTiming.Counters.ProjectGraphEvaluations));
            Assert.That(withTiming.Counters.AssemblyLoads, Is.EqualTo(withoutTiming.Counters.AssemblyLoads));
            Assert.That(withTiming.Counters.ModesEvaluated, Is.EqualTo(withoutTiming.Counters.ModesEvaluated));
            Assert.That(withTiming.Counters.RenderedSinkCount, Is.EqualTo(withoutTiming.Counters.RenderedSinkCount));
            Assert.That(withTiming.Counters.OutputSinkCount, Is.EqualTo(withoutTiming.Counters.OutputSinkCount));

            // Only the environment-dependent parts differ when timing is absent.
            Assert.That(withoutTiming.Counters.ContractFamilyCounts, Is.Empty);
            Assert.That(withTiming.Counters.ContractFamilyCounts, Is.Not.Empty);
            Assert.That(withoutTiming.Phases, Is.Empty);
            Assert.That(withTiming.Phases, Is.Not.Empty);
        });
    }

    [Test]
    public void Build_ContractFamilyCounts_MatchMeasuredCounts()
    {
        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), TimingWithContractFamilies(), renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Counters.ContractFamilyCounts["dependency"], Is.EqualTo(5));
            Assert.That(profile.Counters.ContractFamilyCounts["coverage"], Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_RepeatedContractFamilyMeasurements_AreSummedAcrossModes()
    {
        var timing = new ValidationTiming();
        using (timing.MeasureContractFamily("dependency", () => 3)) { }
        using (timing.MeasureContractFamily("dependency", () => 2)) { }

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(modes: 2), timing, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.That(profile.Counters.ContractFamilyCounts["dependency"], Is.EqualTo(5));
    }

    [Test]
    public void Build_ExposesSnapshotFactIndexAndSourceCounters()
    {
        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Counters.SnapshotMaterializations, Is.EqualTo(1));
            Assert.That(profile.Counters.FactIndexMaterializations, Is.EqualTo(1));
            Assert.That(profile.Counters.SourceScanPasses, Is.EqualTo(1));
            Assert.That(profile.Counters.SourceFilesScanned, Is.EqualTo(7));
            Assert.That(profile.Counters.DiscoveredProjectCount, Is.EqualTo(3));
            Assert.That(profile.Counters.RetainedAssemblyCount, Is.EqualTo(2));
            Assert.That(profile.Counters.SelectedAssemblyCount, Is.EqualTo(3));
            Assert.That(profile.Counters.ContractFamilyResultCounts["dependency"], Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_AdditionalSink_ChangesOnlyRenderAndOutputCounters()
    {
        ArchitectureAnalysisSnapshotCounters snapshotCounters = Counters();
        ValidationTiming timing = TimingWithContractFamilies();

        AnalysisProfile oneSink = AnalysisProfileBuilder.Build(
            snapshotCounters, timing, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);
        AnalysisProfile threeSinks = AnalysisProfileBuilder.Build(
            snapshotCounters, timing, renderedSinkCount: 3, outputSinkCount: 3,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(oneSink.Counters.RenderedSinkCount, Is.EqualTo(1));
            Assert.That(threeSinks.Counters.RenderedSinkCount, Is.EqualTo(3));
            Assert.That(oneSink.Counters.OutputSinkCount, Is.EqualTo(1));
            Assert.That(threeSinks.Counters.OutputSinkCount, Is.EqualTo(3));

            Assert.That(oneSink.Counters.PolicyCompositions, Is.EqualTo(threeSinks.Counters.PolicyCompositions));
            Assert.That(oneSink.Counters.ProjectGraphEvaluations, Is.EqualTo(threeSinks.Counters.ProjectGraphEvaluations));
            Assert.That(oneSink.Counters.AssemblyLoads, Is.EqualTo(threeSinks.Counters.AssemblyLoads));
            Assert.That(oneSink.Counters.ContractFamilyCounts, Is.EqualTo(threeSinks.Counters.ContractFamilyCounts));
        });
    }

    [Test]
    public void Build_ReservedCacheAndConcurrencyFields_ReportNotApplicable()
    {
        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Counters.Cache.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.NotApplicable));
            Assert.That(profile.Counters.Cache.Lookups, Is.EqualTo(0));
            Assert.That(profile.Counters.Cache.Hits, Is.EqualTo(0));
            Assert.That(profile.Counters.Concurrency.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.NotApplicable));
            Assert.That(profile.Counters.Concurrency.MaxParallelism, Is.EqualTo(0));
            Assert.That(profile.Counters.Concurrency.ScheduledWorkItems, Is.EqualTo(0));
            Assert.That(profile.Counters.Concurrency.CompletedWorkItems, Is.EqualTo(0));
            Assert.That(profile.Counters.Concurrency.ObservedMaxConcurrency, Is.EqualTo(0));
            Assert.That(profile.Counters.Concurrency.MergeOperations, Is.EqualTo(0));
        });
    }

    // Issue #408: Counters.Concurrency reports Active with real scheduling evidence once a
    // parallel-eligible scanning phase actually ran, and NotApplicable/all-zero when it didn't
    // (e.g. --max-parallelism 1 or a small target/source set) — see
    // openspec/specs/analysis-profile/spec.md, "Cache and concurrency fields are populated when
    // their capability is active".
    [Test]
    public void Build_ParallelEligibleRun_ReportsActiveConcurrencyWithRealCounters()
    {
        ArchitectureAnalysisSnapshotCounters counters = Counters() with
        {
            MaxParallelism = 4,
            ParallelScheduledWorkItems = 6,
            ParallelCompletedWorkItems = 6,
            ParallelObservedMaxConcurrency = 4,
            ParallelMergeOperations = 2,
        };

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters, timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Counters.Concurrency.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.Active));
            Assert.That(profile.Counters.Concurrency.MaxParallelism, Is.EqualTo(4));
            Assert.That(profile.Counters.Concurrency.ScheduledWorkItems, Is.EqualTo(6));
            Assert.That(profile.Counters.Concurrency.CompletedWorkItems, Is.EqualTo(6));
            Assert.That(profile.Counters.Concurrency.ObservedMaxConcurrency, Is.EqualTo(4));
            Assert.That(profile.Counters.Concurrency.MergeOperations, Is.EqualTo(2));
        });
    }

    [Test]
    public void Build_SequentialModeRun_ReportsNotApplicableConcurrencyWithMaxParallelismZeroed()
    {
        // MaxParallelism is otherwise "the resolved effective degree regardless of whether it was
        // used" — but the NotApplicable/all-zero contract takes precedence, so a resolved value of
        // 1 (or any other) must not leak through when no phase actually ran in parallel.
        ArchitectureAnalysisSnapshotCounters counters = Counters() with
        {
            MaxParallelism = 4,
            ParallelScheduledWorkItems = 0,
        };

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            counters, timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Counters.Concurrency.Status, Is.EqualTo(AnalysisProfileReservedFieldStatus.NotApplicable));
            Assert.That(profile.Counters.Concurrency.MaxParallelism, Is.EqualTo(0));
        });
    }

    [TestCase(AnalysisProfileCompletionStatus.Success)]
    [TestCase(AnalysisProfileCompletionStatus.ValidationFailure)]
    [TestCase(AnalysisProfileCompletionStatus.PreparationFailure)]
    [TestCase(AnalysisProfileCompletionStatus.Cancelled)]
    public void Build_CompletionStatus_IsRecordedAsSupplied(AnalysisProfileCompletionStatus expected)
    {
        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            expected, cancellationObserved: expected == AnalysisProfileCompletionStatus.Cancelled);

        Assert.That(profile.CompletionStatus, Is.EqualTo(expected));
    }

    [Test]
    public void Build_CancelledCompletion_IsDistinguishableFromGenericFailure()
    {
        AnalysisProfile cancelled = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Cancelled, cancellationObserved: true);
        AnalysisProfile failed = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.ValidationFailure, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(cancelled.CompletionStatus, Is.EqualTo(AnalysisProfileCompletionStatus.Cancelled));
            Assert.That(cancelled.CancellationObserved, Is.True);
            Assert.That(failed.CompletionStatus, Is.EqualTo(AnalysisProfileCompletionStatus.ValidationFailure));
            Assert.That(failed.CancellationObserved, Is.False);
        });
    }

    [Test]
    public void Build_SchemaId_IsTheVersionedConstant()
    {
        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.That(profile.SchemaId, Is.EqualTo("analysis-profile/v1"));
        Assert.That(profile.SchemaId, Is.EqualTo(AnalysisProfileId.V1));
    }

    [Test]
    public void Build_CycleRecorderMeasurements_PreservesJointTuplesAndSummarizesDenominators()
    {
        ValidationTiming timing = ValidationTiming.CreateForAnalysisProfile();
        timing.RecordCycleRecorderObservation(new CycleRecorderObservation(
            "strict", true, 3, 4, 5, 2, 3, 1.5, 1.1, 0.4, 256, 0.2, 64,
            new CycleRecorderWorkObservation(1, 3, 4, 3, 4, 4, 4, 3, 4, 3, 4, 5, 5, 4, 3)));
        timing.RecordCycleRecorderObservation(new CycleRecorderObservation(
            "audit", false, 2, 0, 0, 0, 0, 0.01, 0, 0, 0, 0.02, 8,
            new CycleRecorderWorkObservation(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), timing, renderedSinkCount: 0, outputSinkCount: 0,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        AnalysisProfileCycleRecorderMeasurements recorder = profile.Measurements!.CycleRecorder!;
        Assert.Multiple(() =>
        {
            Assert.That(recorder.InvocationCount, Is.EqualTo(2));
            Assert.That(recorder.EnabledInvocationCount, Is.EqualTo(1));
            Assert.That(recorder.DisabledInvocationCount, Is.EqualTo(1));
            Assert.That(recorder.EmptyEvidenceInvocationCount, Is.EqualTo(1));
            Assert.That(recorder.NonEmptyEvidenceInvocationCount, Is.EqualTo(1));
            Assert.That(recorder.Vertices, Is.EqualTo(5));
            Assert.That(recorder.Edges, Is.EqualTo(4));
            Assert.That(recorder.CandidateEvidenceCount, Is.EqualTo(5));
            Assert.That(recorder.UniqueCandidateEdgePairs, Is.EqualTo(2));
            Assert.That(recorder.EmittedCandidateCount, Is.EqualTo(3));
            Assert.That(recorder.ElapsedMs, Is.EqualTo(1.51).Within(0.0001));
            Assert.That(recorder.ComponentIndexBuildElapsedMs, Is.EqualTo(1.1));
            Assert.That(recorder.CandidateEvaluationElapsedMs, Is.EqualTo(0.4));
            Assert.That(recorder.AllocatedBytes, Is.EqualTo(256));
            Assert.That(recorder.Invocations[0].Mode, Is.EqualTo("strict"));
            Assert.That(recorder.Invocations[0].Vertices, Is.EqualTo(3));
            Assert.That(recorder.Invocations[0].Edges, Is.EqualTo(4));
            Assert.That(recorder.Invocations[0].CandidateEvidenceCount, Is.EqualTo(5));
            Assert.That(recorder.Invocations[0].UniqueCandidateEdgePairs, Is.EqualTo(2));
            Assert.That(recorder.Invocations[0].EmittedCandidateCount, Is.EqualTo(3));
            Assert.That(recorder.Invocations[1].Mode, Is.EqualTo("audit"));
            Assert.That(recorder.Invocations[1].Work.ComponentIndexBuildCount, Is.Zero);
            Assert.That(recorder.Invocations[1].Work.CandidateEvidenceChecks, Is.Zero);
        });
    }

    [Test]
    public void Build_CycleRecorderMeasurements_AreOnlyPresentForExplicitProfileObserver()
    {
        AnalysisProfile profileObserver = AnalysisProfileBuilder.Build(
            Counters(), ValidationTiming.CreateForAnalysisProfile(), renderedSinkCount: 0, outputSinkCount: 0,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);
        AnalysisProfile timingOnly = AnalysisProfileBuilder.Build(
            Counters(), new ValidationTiming(), renderedSinkCount: 0, outputSinkCount: 0,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        Assert.Multiple(() =>
        {
            Assert.That(profileObserver.Measurements!.CycleRecorder!.InvocationCount, Is.Zero);
            Assert.That(profileObserver.Measurements.CycleRecorder.Invocations, Is.Empty);
            Assert.That(timingOnly.Measurements, Is.Null);
        });
    }

    [Test]
    public void Build_OutputAndProcessorTime_ArePreserved()
    {
        AnalysisProfileOutput output = new()
        {
            CommittedSinkCount = 1,
            FailedSinkCount = 1,
            StagedSinkCount = 2,
            UncommittedSinkCount = 1,
            OutputFailed = true,
        };

        AnalysisProfile profile = AnalysisProfileBuilder.Build(
            Counters(), TimingWithContractFamilies(), renderedSinkCount: 2, outputSinkCount: 2,
            AnalysisProfileCompletionStatus.ValidationFailure, cancellationObserved: false,
            options: new AnalysisProfileBuildOptions { Output = output });

        Assert.Multiple(() =>
        {
            Assert.That(profile.Output, Is.EqualTo(output));
            Assert.That(profile.Phases, Is.Not.Empty);
            Assert.That(profile.Phases, Has.All.Matches<AnalysisProfilePhaseMeasurement>(
                phase => phase.ProcessorTimeMs is >= 0));
        });
    }

    [Test]
    public void Build_SelectorPhase_UsesMeasuredTimingAndNullWithoutTiming()
    {
        ArchitectureAnalysisSnapshotCounters counters = Counters() with
        {
            SelectorPredicateEvaluations = 3,
        };
        var timing = new ValidationTiming();
        using (timing.Measure("total")) { }
        timing.RecordSelectorPredicateWallTime(Stopwatch.Frequency / 100);

        AnalysisProfile measured = AnalysisProfileBuilder.Build(
            counters, timing, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);
        AnalysisProfile unmeasured = AnalysisProfileBuilder.Build(
            counters, timing: null, renderedSinkCount: 1, outputSinkCount: 1,
            AnalysisProfileCompletionStatus.Success, cancellationObserved: false);

        AnalysisProfilePhaseMeasurement measuredSelector = measured.Phases.Single(
            phase => phase.Name == "selector_predicate_evaluation");
        AnalysisProfilePhaseMeasurement unmeasuredSelector = unmeasured.Phases.Single(
            phase => phase.Name == "selector_predicate_evaluation");
        Assert.Multiple(() =>
        {
            Assert.That(measuredSelector.Count, Is.EqualTo(3));
            Assert.That(measuredSelector.ElapsedMs, Is.GreaterThan(0));
            Assert.That(measuredSelector.ProcessorTimeMs, Is.Null);
            Assert.That(unmeasuredSelector.Count, Is.EqualTo(3));
            Assert.That(unmeasuredSelector.ElapsedMs, Is.Null);
            Assert.That(unmeasuredSelector.ProcessorTimeMs, Is.Null);
        });
    }
}
