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
            Assert.That(profile.Counters.Concurrency.Workers, Is.EqualTo(0));
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
            AnalysisProfileCompletionStatus.ValidationFailure, cancellationObserved: false, output: output);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Output, Is.EqualTo(output));
            Assert.That(profile.Phases, Is.Not.Empty);
            Assert.That(profile.Phases, Has.All.Matches<AnalysisProfilePhaseMeasurement>(
                phase => phase.ProcessorTimeMs is >= 0));
        });
    }
}
