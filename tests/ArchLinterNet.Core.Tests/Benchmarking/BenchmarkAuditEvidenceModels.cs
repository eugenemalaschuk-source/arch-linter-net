using System.Text.Json.Serialization;
using ArchLinterNet.Core.Profiling;

namespace ArchLinterNet.Core.Tests;

internal sealed record BenchmarkWorkCounterEvidence
{
    public const string InstrumentationId = "benchmark-work-observer/v1";

    public required string Instrumentation { get; init; }

    public required BenchmarkResourceMeasurement ProjectFileParses { get; init; }

    public required BenchmarkResourceMeasurement SourceFactFileParses { get; init; }

    public required BenchmarkResourceMeasurement MethodBodyCompilationCalls { get; init; }

    public required BenchmarkResourceMeasurement MethodBodySyntaxTrees { get; init; }

    public required BenchmarkResourceMeasurement AstSemanticLookups { get; init; }

    public required BenchmarkResourceMeasurement LayerMembershipProbes { get; init; }

    public required BenchmarkResourceMeasurement CelContextConstructions { get; init; }

    public static BenchmarkWorkCounterEvidence FromObserver(BenchmarkWorkObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        IReadOnlyDictionary<string, long> observed = observer.Snapshot();

        return new BenchmarkWorkCounterEvidence
        {
            Instrumentation = InstrumentationId,
            ProjectFileParses = Read(observed, nameof(BenchmarkWorkObserver.ProjectFileParses)),
            SourceFactFileParses = Read(observed, nameof(BenchmarkWorkObserver.SourceFactFileParses)),
            MethodBodyCompilationCalls = Read(observed, nameof(BenchmarkWorkObserver.MethodBodyCompilationCalls)),
            MethodBodySyntaxTrees = Read(observed, nameof(BenchmarkWorkObserver.MethodBodySyntaxTrees)),
            AstSemanticLookups = Read(observed, nameof(BenchmarkWorkObserver.AstSemanticLookups)),
            LayerMembershipProbes = Read(observed, nameof(BenchmarkWorkObserver.LayerMembershipProbes)),
            CelContextConstructions = Read(observed, nameof(BenchmarkWorkObserver.CelContextConstructions)),
        };
    }

    public static BenchmarkWorkCounterEvidence ObserverDisabled(string reason)
    {
        return new BenchmarkWorkCounterEvidence
        {
            Instrumentation = InstrumentationId,
            ProjectFileParses = BenchmarkResourceMeasurement.NotApplicable(reason),
            SourceFactFileParses = BenchmarkResourceMeasurement.NotApplicable(reason),
            MethodBodyCompilationCalls = BenchmarkResourceMeasurement.NotApplicable(reason),
            MethodBodySyntaxTrees = BenchmarkResourceMeasurement.NotApplicable(reason),
            AstSemanticLookups = BenchmarkResourceMeasurement.NotApplicable(reason),
            LayerMembershipProbes = BenchmarkResourceMeasurement.NotApplicable(reason),
            CelContextConstructions = BenchmarkResourceMeasurement.NotApplicable(reason),
        };
    }

    public void Validate()
    {
        if (!string.Equals(Instrumentation, InstrumentationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported benchmark work instrumentation '{Instrumentation}'.");
        }

        ValidateOperations(ProjectFileParses, nameof(ProjectFileParses));
        ValidateOperations(SourceFactFileParses, nameof(SourceFactFileParses));
        ValidateOperations(MethodBodyCompilationCalls, nameof(MethodBodyCompilationCalls));
        ValidateOperations(MethodBodySyntaxTrees, nameof(MethodBodySyntaxTrees));
        ValidateOperations(AstSemanticLookups, nameof(AstSemanticLookups));
        ValidateOperations(LayerMembershipProbes, nameof(LayerMembershipProbes));
        ValidateOperations(CelContextConstructions, nameof(CelContextConstructions));
    }

    private static BenchmarkResourceMeasurement Read(IReadOnlyDictionary<string, long> observed, string key) =>
        observed.TryGetValue(key, out long value)
            ? BenchmarkResourceMeasurement.Available(value, "operations")
            : BenchmarkResourceMeasurement.Available(0, "operations");

    private static void ValidateOperations(BenchmarkResourceMeasurement measurement, string fieldName)
    {
        measurement.Validate($"work_counters.{fieldName}");
        if (measurement.Status == BenchmarkMeasurementStatus.Available && !string.Equals(measurement.Unit, "operations", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Benchmark work counter '{fieldName}' must use the operations unit.");
        }
    }
}

/// <summary>Fixed-key work boundaries reserved for the sibling History workloads.</summary>
internal sealed record BenchmarkHistoryWorkCounterEvidence
{
    public const string InstrumentationId = "benchmark-history-work/v1";

    public required string Instrumentation { get; init; }

    public required BenchmarkResourceMeasurement LineChurnAlgorithmWork { get; init; }

    public required BenchmarkResourceMeasurement TaskCommitOccurrencesExamined { get; init; }

    public required BenchmarkResourceMeasurement CoChangeEdgesAssigned { get; init; }

    public required BenchmarkResourceMeasurement AncestryAncestorsVisited { get; init; }

    public required BenchmarkResourceMeasurement AncestryRetainedEntries { get; init; }

    public void Validate()
    {
        if (!string.Equals(Instrumentation, InstrumentationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported History instrumentation '{Instrumentation}'.");
        }

        ValidateCount(LineChurnAlgorithmWork, nameof(LineChurnAlgorithmWork), "operations");
        ValidateCount(TaskCommitOccurrencesExamined, nameof(TaskCommitOccurrencesExamined), "operations");
        ValidateCount(CoChangeEdgesAssigned, nameof(CoChangeEdgesAssigned), "operations");
        ValidateCount(AncestryAncestorsVisited, nameof(AncestryAncestorsVisited), "operations");
        ValidateCount(AncestryRetainedEntries, nameof(AncestryRetainedEntries), "entries");
    }

    public static BenchmarkHistoryWorkCounterEvidence NotApplicableToCoreWorkload() => new()
    {
        Instrumentation = InstrumentationId,
        LineChurnAlgorithmWork = BenchmarkResourceMeasurement.NotApplicable("This workload does not execute History line-churn analysis."),
        TaskCommitOccurrencesExamined = BenchmarkResourceMeasurement.NotApplicable("This workload does not execute History bottleneck analysis."),
        CoChangeEdgesAssigned = BenchmarkResourceMeasurement.NotApplicable("This workload does not execute History co-change analysis."),
        AncestryAncestorsVisited = BenchmarkResourceMeasurement.NotApplicable("This workload does not execute History ancestry queries."),
        AncestryRetainedEntries = BenchmarkResourceMeasurement.NotApplicable("This workload does not retain History ancestry sets."),
    };

    private static void ValidateCount(BenchmarkResourceMeasurement measurement, string name, string expectedUnit)
    {
        measurement.Validate($"history_work_counters.{name}");
        if (measurement.Status == BenchmarkMeasurementStatus.Available &&
            !string.Equals(measurement.Unit, expectedUnit, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"History work counter '{name}' must use the '{expectedUnit}' unit.");
        }
    }
}

/// <summary>
/// Timer intervals are retained independently. Values may overlap and must never be subtracted
/// from one another to infer process startup or analyzer time.
/// </summary>
internal sealed record BenchmarkTimingBoundaryEvidence
{
    public const string BoundaryContract = "independent-stopwatch-intervals/v1";

    public required string Contract { get; init; }

    public required BenchmarkResourceMeasurement FixtureMaterialization { get; init; }

    public required BenchmarkResourceMeasurement RestoreAndBuild { get; init; }

    public required BenchmarkResourceMeasurement ProcessHostStartupAndShutdown { get; init; }

    public required BenchmarkResourceMeasurement CliProcess { get; init; }

    public required BenchmarkResourceMeasurement CoreAnalysisSession { get; init; }

    public required BenchmarkResourceMeasurement ResultPublication { get; init; }

    public required BenchmarkResourceMeasurement TotalEndToEnd { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BenchmarkProfilerOverheadEvidence? ProfilerOverhead { get; init; }

    public void Validate()
    {
        if (!string.Equals(Contract, BoundaryContract, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported benchmark timing boundary contract '{Contract}'.");
        }

        FixtureMaterialization.Validate("timing_boundaries.fixture_materialization");
        RestoreAndBuild.Validate("timing_boundaries.restore_and_build");
        ProcessHostStartupAndShutdown.Validate("timing_boundaries.process_host_startup_and_shutdown");
        CliProcess.Validate("timing_boundaries.cli_process");
        CoreAnalysisSession.Validate("timing_boundaries.core_analysis_session");
        ResultPublication.Validate("timing_boundaries.result_publication");
        TotalEndToEnd.Validate("timing_boundaries.total_end_to_end");
        ProfilerOverhead?.Validate();
    }
}

internal sealed record BenchmarkProfilerOverheadEvidence
{
    public const string InstrumentationId = "benchmark-work-observer/v1";

    public required string Instrumentation { get; init; }

    public required BenchmarkResourceMeasurement ObserverDisabledAnalysis { get; init; }

    public required BenchmarkResourceMeasurement ObserverEnabledAnalysis { get; init; }

    public required BenchmarkResourceMeasurement ObserverDisabledAllocatedBytes { get; init; }

    public required BenchmarkResourceMeasurement ObserverEnabledAllocatedBytes { get; init; }

    public required BenchmarkResourceMeasurement ObserverDisabledPeakRetainedBytes { get; init; }

    public required BenchmarkResourceMeasurement ObserverEnabledPeakRetainedBytes { get; init; }

    public required bool CanonicalResultsMatch { get; init; }

    public void Validate()
    {
        if (!string.Equals(Instrumentation, InstrumentationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported profiler-overhead instrumentation '{Instrumentation}'.");
        }

        ObserverDisabledAnalysis.Validate("profiler_overhead.observer_disabled_analysis");
        ObserverEnabledAnalysis.Validate("profiler_overhead.observer_enabled_analysis");
        ObserverDisabledAllocatedBytes.Validate("profiler_overhead.observer_disabled_allocated_bytes");
        ObserverEnabledAllocatedBytes.Validate("profiler_overhead.observer_enabled_allocated_bytes");
        ObserverDisabledPeakRetainedBytes.Validate("profiler_overhead.observer_disabled_peak_retained_bytes");
        ObserverEnabledPeakRetainedBytes.Validate("profiler_overhead.observer_enabled_peak_retained_bytes");
    }
}

internal static class BenchmarkAuditMeasurement
{
    public static BenchmarkResourceMeasurement Unavailable(string boundary) =>
        BenchmarkResourceMeasurement.Unavailable(boundary);

    public static BenchmarkResourceMeasurement NotApplicable(string boundary) =>
        BenchmarkResourceMeasurement.NotApplicable(boundary);
}
