using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class PreparedAnalysisReuseBenchmarkHarness
{
    private sealed record MeasuredScalePoint(
        string Label,
        BenchmarkWorkloadDefinition Workload,
        int CommandCount,
        decimal IndependentPreparationWork,
        decimal IndependentProjectionWork,
        decimal IndependentPreparationMilliseconds,
        decimal IndependentProjectionMilliseconds,
        decimal PerConsumerLoadAuthorizationCost,
        decimal PerConsumerLoadAuthorizationMilliseconds,
        IReadOnlyList<string> MissingTimingEvidenceFamilies);

    private static PreparedEffectContract CreateEffect(
        BenchmarkWorkloadDefinition workload,
        IReadOnlyCollection<CrossProcessProcessEvidence> processes,
        IReadOnlyList<MeasuredScalePoint> measuredScalePoints)
    {
        IReadOnlyList<CrossProcessProcessEvidence> representativeIndependentProcesses =
            SelectRepresentativeIndependentProcesses(processes);
        int representativeProcessCount = representativeIndependentProcesses.Count;
        IReadOnlyList<CounterWorkMeasurement> independentMeasurements = representativeIndependentProcesses
            .Select(process => TryReadCounterWork(process.Sample.RawAnalysisProfile))
            .Where(measurement => measurement is not null)
            .Select(measurement => measurement!)
            .ToList();
        Assert.That(independentMeasurements, Is.Not.Empty,
            "Expected-effect calculations require real profile counters from candidate independent processes.");

        IReadOnlyList<CrossProcessProcessEvidence> oneProcessProcesses = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind is PreparationExecutionKind.InProcessProjection or
                                  PreparationExecutionKind.ProcessBoundProjection)
            .ToList();
        IReadOnlyList<CrossProcessProcessEvidence> sharedProcesses = oneProcessProcesses
            .Where(process => _sharedProjectionFamilies.Contains(
                process.Identity.Projection.CommandFamily,
                StringComparer.Ordinal))
            .ToList();
        IReadOnlyList<string> oneProcessFamilies = oneProcessProcesses
            .Select(process => process.Identity.Projection.CommandFamily)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToList();
        Assert.That(oneProcessFamilies, Is.EquivalentTo(_measuredCommandFamilies),
            "Independent and one-process effect calculations must use the same representative command-family workload.");

        decimal independentPreparationWork = independentMeasurements.Sum(measurement => measurement.PreparationWork);
        decimal averageIndependentPreparationWork = independentPreparationWork / representativeProcessCount;
        long candidateWork = Math.Max(0, (long)Math.Ceiling(averageIndependentPreparationWork));
        long cacheAvoidableWork = Math.Min(
            candidateWork,
            (long)Math.Ceiling(
                independentMeasurements.Sum(measurement => measurement.CacheAvoidableWork) /
                (decimal)representativeProcessCount));
        long preparedStateAvoidableWork = candidateWork - cacheAvoidableWork;
        IReadOnlyList<string> missingOneProcessWorkEvidenceFamilies = _measuredCommandFamilies
            .Where(family =>
                !representativeIndependentProcesses.Any(process =>
                    string.Equals(process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal) &&
                    TryReadCounterWork(process.Sample.RawAnalysisProfile) is not null) ||
                !oneProcessProcesses.Any(process =>
                    string.Equals(process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal) &&
                    TryReadCounterWork(process.Sample.RawAnalysisProfile) is not null))
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToList();
        bool oneProcessWorkEvidenceComplete = missingOneProcessWorkEvidenceFamilies.Count == 0;
        IReadOnlyDictionary<string, decimal> oneProcessProjectionDurations = oneProcessProcesses
            .GroupBy(process => process.Identity.Projection.CommandFamily, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(process => TryReadMeasuredDurationMilliseconds(process.Sample))
                    .FirstOrDefault(duration => duration is > 0) ?? 0,
                StringComparer.Ordinal);
        IReadOnlyList<string> missingTimingEvidenceFamilies = _measuredCommandFamilies
            .Where(family =>
            {
                bool independentAvailable = representativeIndependentProcesses
                    .Where(process => string.Equals(process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal))
                    .Select(process => TryReadMeasuredDurationMilliseconds(process.Sample))
                    .Any(duration => duration is > 0);
                bool oneProcessAvailable = processes
                    .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                                      string.Equals(process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal) &&
                                      process.Identity.ExecutionKind is PreparationExecutionKind.InProcessProjection or
                                          PreparationExecutionKind.ProcessBoundProjection)
                    .Select(process => TryReadMeasuredDurationMilliseconds(process.Sample))
                    .Any(duration => duration is > 0);
                return !independentAvailable || !oneProcessAvailable;
            })
            .Concat(_measuredCommandFamilies.Where(family =>
            {
                CrossProcessProcessEvidence? independent = representativeIndependentProcesses
                    .FirstOrDefault(process => string.Equals(
                        process.Identity.Projection.CommandFamily, family, StringComparison.Ordinal));
                return independent is not null &&
                       TryReadMeasuredDurationMilliseconds(independent.Sample) is decimal independentDuration &&
                       oneProcessProjectionDurations.TryGetValue(family, out decimal projectionDuration) &&
                       projectionDuration > 0 &&
                       independentDuration < projectionDuration;
            }))
            .OrderBy(family => family, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        bool timingEvidenceComplete = missingTimingEvidenceFamilies.Count == 0;

        decimal independentPreparationMilliseconds = timingEvidenceComplete
            ? representativeIndependentProcesses.Sum(process =>
            {
                decimal independentDuration = TryReadMeasuredDurationMilliseconds(process.Sample)!.Value;
                decimal projectionDuration = oneProcessProjectionDurations[process.Identity.Projection.CommandFamily];
                return independentDuration - projectionDuration;
            })
            : 0;
        decimal unavoidableProjectionMilliseconds = timingEvidenceComplete
            ? oneProcessProjectionDurations.Values.Sum()
            : 0;
        decimal coldPrepareCost = timingEvidenceComplete
            ? independentPreparationMilliseconds / representativeProcessCount
            : 0;
        IReadOnlyList<decimal> loadAuthorizationDurations = timingEvidenceComplete
            ? sharedProcesses.Select(process => MeasureLoadAuthorizationDuration(process.Sample.RawAnalysisProfile)).ToList()
            : [];
        decimal loadAuthorizationCost = loadAuthorizationDurations.Count == 0
            ? 0
            : loadAuthorizationDurations.Average();
        decimal loadCostLowerBound = loadAuthorizationDurations.Count == 0 ? 0 : loadAuthorizationDurations.Min();
        decimal loadCostUpperBound = loadAuthorizationDurations.Count == 0 ? 0 : loadAuthorizationDurations.Max();
        bool decisionCapableEvidence = oneProcessWorkEvidenceComplete && timingEvidenceComplete;
        decimal measuredIndependentWorkflowWork = decisionCapableEvidence
            ? representativeProcessCount * coldPrepareCost + unavoidableProjectionMilliseconds
            : 0;
        decimal? measuredOneProcessAlternativeWork = decisionCapableEvidence
            ? coldPrepareCost + unavoidableProjectionMilliseconds
            : null;
        decimal repeatedWorkShare = measuredIndependentWorkflowWork <= 0
            ? 0
            : independentPreparationMilliseconds / measuredIndependentWorkflowWork;
        IReadOnlyList<PreparedEffectScalePoint> scaleEvidence = CreateScaleEvidence(
            measuredScalePoints,
            representativeProcessCount);
        string workMeasurementBasis = decisionCapableEvidence
            ? $"Stopwatch wall-clock durations in milliseconds are the only decision-cost units. One disabled-cache independent process per required command family ({string.Join(", ", _measuredCommandFamilies)}) is paired with its measured one-process projection: cold preparation is the non-negative independent-minus-projection duration, and the projection/command duration is included in the common U component exactly once. Summed deterministic counters remain attribution evidence only and are not added as costs. Cache miss/hit samples remain supplemental and are excluded from the comparable workload."
            : $"Decision-capable timing/attribution evidence is incomplete. Stopwatch wall-clock durations in milliseconds remain the only decision-cost units; deterministic counters remain attribution evidence only. Cache miss/hit samples remain supplemental and are excluded from the comparable workload. Missing counter families: {string.Join(", ", missingOneProcessWorkEvidenceFamilies)}; missing duration families: {string.Join(", ", missingTimingEvidenceFamilies)}. Missing work is not treated as zero.";

        PreparedEffectContract effect = new()
        {
            IssueReference = "#493",
            CostModelUnit = PreparedEffectContract.CostModelUnitMilliseconds,
            CostMeasurementBasis = "Stopwatch wall-clock duration measurements retained in milliseconds; deterministic profile counters are attribution evidence and never interchangeable cost units.",
            RepresentativeProcessCount = representativeProcessCount,
            RepeatedWorkShare = repeatedWorkShare,
            CandidatePreparedBoundaryWork = candidateWork,
            CacheAvoidableWork = cacheAvoidableWork,
            PreparedStateAvoidableWork = preparedStateAvoidableWork,
            ColdPrepareCost = coldPrepareCost,
            PerConsumerLoadAuthorizationCost = loadAuthorizationCost,
            PerConsumerLoadAuthorizationCostLowerBound = loadCostLowerBound,
            PerConsumerLoadAuthorizationCostUpperBound = loadCostUpperBound,
            PerConsumerLoadAuthorizationCostBasis =
                "Measured duration of a repeated in-memory serialized-state load/authorization proxy over the shared analysis profiles; SelectedAssemblyCount is inspected for attribution only and is not a cost unit. Replace this pre-implementation proxy with persisted-store instrumentation.",
            UnavoidableProjectionWork = unavoidableProjectionMilliseconds,
            MeasuredIndependentWorkflowWork = measuredIndependentWorkflowWork,
            MeasuredOneProcessAlternativeWork = measuredOneProcessAlternativeWork,
            OneProcessWorkEvidenceComplete = oneProcessWorkEvidenceComplete,
            MissingOneProcessWorkEvidenceFamilies = missingOneProcessWorkEvidenceFamilies,
            TimingEvidenceComplete = timingEvidenceComplete,
            MissingTimingEvidenceFamilies = missingTimingEvidenceFamilies,
            ExpectedPersistedReuseWork = 0,
            DistinctCrossProcessValue = false,
            MaterialSavingsThreshold = 0.10m,
            MeasuredMaterialSavingsRatio = 0,
            WorkMeasurementBasis = workMeasurementBasis,
            BreakEvenProcessCount = null,
            CacheModesMeasured = ["disabled", "miss", "hit"],
            Resources = CreatePreparationResourceEvidence(workload, representativeIndependentProcesses, representativeProcessCount),
            ScaleEvidenceBasis =
                $"Measured Stopwatch durations in milliseconds for small, medium, and large {workload.CompilationMode} workloads; each point runs one cache-disabled process for every representative command family ({string.Join(", ", _measuredCommandFamilies)}), independently times only available projections, and repeats an in-memory serialized-state load/authorization proxy. Process-bound families without projection-only or phase timing remain explicitly incomplete; no independent process duration is reused as projection timing. Measured analysis-profile/v1 counters remain scale-specific attribution evidence only.",
            ScaleEvidence = scaleEvidence,
            ExpectedEffect = CreateExpectedEffect(
                repeatedWorkShare,
                scaleEvidence),
            ExactCacheHitSavingsExcluded = true,
        };
        int? breakEven = effect.CalculateBreakEvenProcessCount();
        decimal expectedPersistedReuseWork = effect.PreparedReuseCost(representativeProcessCount);
        decimal measuredMaterialSavingsRatio = measuredOneProcessAlternativeWork is > 0
            ? (measuredOneProcessAlternativeWork.Value - expectedPersistedReuseWork) /
              measuredOneProcessAlternativeWork.Value
            : 0;
        return effect with
        {
            BreakEvenProcessCount = breakEven,
            ExpectedPersistedReuseWork = expectedPersistedReuseWork,
            MeasuredMaterialSavingsRatio = measuredMaterialSavingsRatio,
            DistinctCrossProcessValue = decisionCapableEvidence &&
                expectedPersistedReuseWork < measuredOneProcessAlternativeWork!.Value,
        };
    }

    private static IReadOnlyList<PreparedEffectScalePoint> CreateScaleEvidence(
        IReadOnlyList<MeasuredScalePoint> measuredScalePoints,
        int representativeProcessCount)
    {
        return measuredScalePoints
            .Select(point =>
            {
                bool timingEvidenceComplete = point.MissingTimingEvidenceFamilies.Count == 0;
                decimal coldPrepareMilliseconds = timingEvidenceComplete
                    ? point.IndependentPreparationMilliseconds / point.CommandCount
                    : 0;
                decimal independentMilliseconds = timingEvidenceComplete
                    ? point.IndependentPreparationMilliseconds + point.IndependentProjectionMilliseconds
                    : 0;
                decimal persistedMilliseconds = timingEvidenceComplete
                    ? coldPrepareMilliseconds +
                      representativeProcessCount * point.PerConsumerLoadAuthorizationMilliseconds +
                      point.IndependentProjectionMilliseconds
                    : 0;
                decimal expectedLocalSpeedup = timingEvidenceComplete && persistedMilliseconds > 0
                    ? independentMilliseconds / persistedMilliseconds
                    : 1m;
                return new PreparedEffectScalePoint
                {
                    Label = point.Label,
                    WorkloadId = point.Workload.WorkloadId,
                    WorkloadIdentity = point.Workload.WorkloadIdentity,
                    ProjectCount = point.Workload.Inventory.ProjectCount,
                    TypeCount = point.Workload.Inventory.TypeCount,
                    SourceFileCount = point.Workload.Inventory.SourceFileCount,
                    ReferenceEdgeCount = point.Workload.Inventory.ReferenceEdgeCount,
                    CommandCount = point.CommandCount,
                    IndependentPreparationWork = point.IndependentPreparationWork,
                    IndependentProjectionWork = point.IndependentProjectionWork,
                    PerConsumerLoadAuthorizationCost = point.PerConsumerLoadAuthorizationCost,
                    IndependentPreparationMilliseconds = point.IndependentPreparationMilliseconds,
                    IndependentProjectionMilliseconds = point.IndependentProjectionMilliseconds,
                    PerConsumerLoadAuthorizationMilliseconds = point.PerConsumerLoadAuthorizationMilliseconds,
                    ColdPrepareCost = timingEvidenceComplete ? coldPrepareMilliseconds : 0,
                    ColdPrepareMilliseconds = timingEvidenceComplete ? coldPrepareMilliseconds : 0,
                    ExpectedLocalSpeedup = expectedLocalSpeedup,
                    TimingEvidenceComplete = timingEvidenceComplete,
                    MissingTimingEvidenceFamilies = point.MissingTimingEvidenceFamilies,
                    MeasurementBasis = timingEvidenceComplete
                        ? "scale-specific Stopwatch duration measurements in milliseconds from one cache-disabled CLI process per command family, independently timed projections, and a repeated serialized-state load/authorization proxy; analysis-profile counters remain attribution evidence."
                        : $"scale-specific Stopwatch duration measurements in milliseconds are incomplete for process-bound projection families ({string.Join(", ", point.MissingTimingEvidenceFamilies)}); no independent process duration is reused as projection timing, so no authorizing S/M/L speedup is reported. The load proxy and analysis-profile counters remain attribution evidence.",
                };
            })
            .ToList();
    }

    private static decimal DurationMilliseconds(TimeSpan elapsed) =>
        Math.Max(0.001m, (decimal)elapsed.TotalMilliseconds);

    private static decimal? TryReadMeasuredDurationMilliseconds(BenchmarkProfileSample sample)
    {
        if (sample.MeasuredWallClockMilliseconds is > 0)
        {
            return sample.MeasuredWallClockMilliseconds.Value;
        }

        return sample.WallClock.Status == BenchmarkMeasurementStatus.Available &&
            sample.WallClock.Value is > 0
            ? sample.WallClock.Value.Value
            : null;
    }

    private static decimal MeasureLoadAuthorizationDuration(JsonElement profile)
    {
        byte[] payload = Encoding.UTF8.GetBytes(profile.GetRawText());
        const int Repetitions = 32;
        long selectedAssemblyCount = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < Repetitions; index++)
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("Counters", out JsonElement counters) &&
                counters.TryGetProperty("SelectedAssemblyCount", out JsonElement selected) &&
                selected.TryGetInt64(out long value))
            {
                selectedAssemblyCount += value;
            }
        }

        stopwatch.Stop();
        GC.KeepAlive(selectedAssemblyCount);
        return Math.Max(0.001m, (decimal)stopwatch.Elapsed.TotalMilliseconds / Repetitions);
    }

    private static PreparationResourceEvidence CreatePreparationResourceEvidence(
        BenchmarkWorkloadDefinition workload,
        IReadOnlyList<CrossProcessProcessEvidence> representativeIndependentProcesses,
        int representativeProcessCount)
    {
        long deterministicRecordCount = Math.Max(
            1,
            (long)workload.Inventory.ProjectCount +
            workload.Inventory.AssemblyCount +
            workload.Inventory.SourceFileCount +
            workload.Inventory.TypeCount +
            workload.Inventory.ReferenceEdgeCount);
        long profilePayloadBytes = representativeIndependentProcesses
            .Select(process => Encoding.UTF8.GetByteCount(process.Sample.RawAnalysisProfile.GetRawText()))
            .DefaultIfEmpty(1)
            .Max();
        long stateLowerBound = Math.Max(1, Math.Max(checked(deterministicRecordCount * 16), profilePayloadBytes / 4));
        long stateUpperBound = Math.Max(
            stateLowerBound,
            Math.Max(checked(deterministicRecordCount * 256), checked(profilePayloadBytes * 4)));
        long allocationUpperBound = checked(stateUpperBound * 2);

        return new PreparationResourceEvidence
        {
            StorageBytes = BenchmarkResourceMeasurement.Unavailable(
                "Prepared-state storage is not implemented; the bounded estimate is recorded separately."),
            IoOperations = BenchmarkResourceMeasurement.NotApplicable(
                "Persisted prepared-state I/O does not exist before implementation; the bounded estimate is recorded separately."),
            AllocatedBytes = BenchmarkResourceMeasurement.Unavailable(
                "Prepared-state allocation is not isolated; the bounded estimate is recorded separately."),
            PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable(
                "Prepared-state memory is not isolated; the bounded estimate is recorded separately."),
            StorageBound = new PreparationResourceBound
            {
                LowerBound = stateLowerBound,
                UpperBound = stateUpperBound,
                Unit = "bytes",
                Basis = "Deterministic workload-record envelope plus the largest representative profile payload; this is a pre-implementation serialized-state proxy.",
                Uncertainty = "16-256 bytes per deterministic record and 0.25x-4x profile payload; replace with store instrumentation before implementation.",
            },
            IoOperationsBound = new PreparationResourceBound
            {
                LowerBound = 2,
                UpperBound = checked(2 + representativeProcessCount * 2L),
                Unit = "operations",
                Basis = "One persisted write and one authorization/read minimum, with one metadata/read allowance per representative consumer.",
                Uncertainty = "The range covers one write plus one-to-two read/metadata operations per consumer until the store protocol is selected.",
            },
            AllocatedBytesBound = new PreparationResourceBound
            {
                LowerBound = stateLowerBound,
                UpperBound = allocationUpperBound,
                Unit = "bytes",
                Basis = "Serialized-state envelope with one additional encode/decode working copy.",
                Uncertainty = "One-to-two state copies; implementation must replace this proxy with allocation measurement.",
            },
            PeakManagedMemoryBound = new PreparationResourceBound
            {
                LowerBound = stateLowerBound,
                UpperBound = allocationUpperBound,
                Unit = "bytes",
                Basis = "Serialized-state envelope held during authorization/load.",
                Uncertainty = "One-to-two state copies; excludes unrelated host memory and requires implementation instrumentation.",
            },
        };
    }

    private static IReadOnlyList<CrossProcessProcessEvidence> SelectRepresentativeIndependentProcesses(
        IReadOnlyCollection<CrossProcessProcessEvidence> processes)
    {
        List<CrossProcessProcessEvidence> selected = [];
        foreach (string family in _measuredCommandFamilies)
        {
            CrossProcessProcessEvidence? process = processes
                .Where(candidate => candidate.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                                    candidate.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess &&
                                    string.Equals(candidate.Identity.Projection.CommandFamily, family, StringComparison.Ordinal) &&
                                    string.Equals(candidate.Sample.Run.CacheMode, "disabled", StringComparison.Ordinal))
                .OrderBy(candidate => RepresentativeProcessPreference(candidate.Sample.Run.ParallelMode))
                .ThenBy(candidate => candidate.Identity.ProcessOrdinal)
                .FirstOrDefault();
            Assert.That(process, Is.Not.Null,
                $"Expected one disabled-cache independent process for representative command family '{family}'.");
            selected.Add(process!);
        }

        return selected;
    }

    private static int RepresentativeProcessPreference(string parallelMode) => parallelMode switch
    {
        "preparation-priming" => 0,
        "bounded-parallel" => 1,
        "sequential" => 2,
        _ => 3,
    };

    private static PreparationDecision CreateDecision(
        PreparedEffectContract effect,
        CrossProcessPreparationWorkflow workflow)
    {
        if (!effect.DecisionCapableEvidence)
        {
            string missingCounters = effect.MissingOneProcessWorkEvidenceFamilies.Count == 0
                ? "none"
                : string.Join(", ", effect.MissingOneProcessWorkEvidenceFamilies);
            string missingDurations = effect.MissingTimingEvidenceFamilies.Count == 0
                ? "none"
                : string.Join(", ", effect.MissingTimingEvidenceFamilies);
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.C,
                Route = "route-instrumentation-gap",
                Reason = $"Decision-capable attribution/duration evidence is missing. Counter families: {missingCounters}; duration families: {missingDurations}. The one-process alternative is not decision-capable, and missing work is not treated as zero.",
                OneProcessAlternativeEvaluated = false,
                BreakEvenObserved = false,
            };
        }

        bool breakEvenObserved = effect.BreakEvenProcessCount.HasValue &&
            effect.ExpectedSavings(effect.RepresentativeProcessCount) > 0 &&
            effect.PerConsumerLoadAuthorizationCostUpperBound <
            effect.ColdPrepareCost;
        if (!effect.Resources.HasBoundedTradeoffModel)
        {
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.B,
                Route = "defer-prepared-analysis",
                Reason = "The work crossover is measured, but Outcome A is not decision-capable without bounded storage, I/O, allocation, and peak-memory trade-off evidence.",
                OneProcessAlternativeEvaluated = workflow.OneProcessAlternativeMeasured,
                BreakEvenObserved = breakEvenObserved,
            };
        }

        if (effect.DistinctCrossProcessValue && effect.MateriallyCheaper && breakEvenObserved)
        {
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.A,
                Route = "authorize-prepared-analysis",
                Reason = $"Measured duration evidence shows persisted reuse is materially cheaper than the measured one-process alternative ({effect.MeasuredMaterialSavingsRatio.ToString("P1", CultureInfo.InvariantCulture)} savings versus a {effect.MaterialSavingsThreshold.ToString("P1", CultureInfo.InvariantCulture)} threshold), with a strict break-even under the measured load-cost range.",
                OneProcessAlternativeEvaluated = workflow.OneProcessAlternativeMeasured,
                BreakEvenObserved = true,
            };
        }

        if (workflow.OneProcessAlternativeMeasured)
        {
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.B,
                Route = "defer-prepared-analysis",
                Reason = $"The complete one-process alternative was measured, but persisted reuse is not materially cheaper ({effect.MeasuredMaterialSavingsRatio.ToString("P1", CultureInfo.InvariantCulture)} savings versus a {effect.MaterialSavingsThreshold.ToString("P1", CultureInfo.InvariantCulture)} threshold), its work is no more expensive, or the load-cost range removes a strict representative crossover.",
                OneProcessAlternativeEvaluated = true,
                BreakEvenObserved = breakEvenObserved,
            };
        }

        return new PreparationDecision
        {
            Outcome = PreparationDecisionOutcome.C,
            Route = "route-instrumentation-gap",
            Reason = "The benchmark does not yet contain a complete one-process representative workflow, so the evidence cannot authorize or reject cross-process reuse.",
            OneProcessAlternativeEvaluated = false,
            BreakEvenObserved = breakEvenObserved,
        };
    }

    private static CounterWorkMeasurement? TryReadCounterWork(JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("Counters", out JsonElement counters) ||
            counters.ValueKind != JsonValueKind.Object ||
            counters.TryGetProperty("Unavailable", out _))
        {
            return null;
        }

        long preparationWork = SumProperties(
            counters,
            "PolicyCompositions",
            "ProjectGraphEvaluations",
            "AssemblyLoads",
            "SnapshotMaterializations",
            "FactIndexMaterializations",
            "SourceScanPasses",
            "SourceFilesScanned");
        long projectionWork = SumProperties(counters, "ModesEvaluated", "RenderedSinkCount", "OutputSinkCount") +
            SumObject(counters, "ContractFamilyCounts");
        // SelectedAssemblyCount is a resulting prepared-state inventory cardinality. It is used
        // as the explicit pre-implementation load/authorization proxy and is intentionally kept
        // separate from projection/consume counters below.
        long loadAuthorizationWork = SumProperties(counters, "SelectedAssemblyCount");
        long cacheAvoidableWork = SumObject(
            counters.TryGetProperty("Cache", out JsonElement cache) ? cache : default,
            "AvoidedAssemblyLoads",
            "AvoidedFactIndexMaterializations",
            "AvoidedSourceScanPasses",
            "AvoidedContractExecutions");
        if (preparationWork == 0 && projectionWork == 0 && loadAuthorizationWork == 0)
        {
            return null;
        }

        return new CounterWorkMeasurement(preparationWork, projectionWork, loadAuthorizationWork, cacheAvoidableWork);
    }

    private static long SumProperties(JsonElement value, params string[] names) =>
        value.ValueKind == JsonValueKind.Object
            ? names.Sum(name => value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt64()
            : 0)
            : 0;

    private static long SumObject(JsonElement value, string propertyName, params string[] directNames)
    {
        if (directNames.Length > 0)
        {
            return SumProperties(value, directNames);
        }

        if (!value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return property.EnumerateObject()
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Number)
            .Sum(entry => entry.Value.GetInt64());
    }

    private sealed record CounterWorkMeasurement(
        long PreparationWork,
        long ProjectionWork,
        long LoadAuthorizationWork,
        long CacheAvoidableWork);
}
