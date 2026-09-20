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
        decimal IndependentProjectionWork);

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

        IReadOnlyList<CounterWorkMeasurement> sharedMeasurements = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind == PreparationExecutionKind.InProcessProjection)
            .Select(process => TryReadCounterWork(process.Sample.RawAnalysisProfile))
            .Where(measurement => measurement is not null)
            .Select(measurement => measurement!)
            .ToList();
        Assert.That(sharedMeasurements, Is.Not.Empty,
            "Expected-effect calculations require real profile counters from the one-process projections.");
        IReadOnlyList<string> oneProcessFamilies = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind is PreparationExecutionKind.InProcessProjection or
                                  PreparationExecutionKind.ProcessBoundProjection)
            .Select(process => process.Identity.Projection.CommandFamily)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToList();
        Assert.That(oneProcessFamilies, Is.EquivalentTo(_measuredCommandFamilies),
            "Independent and one-process effect calculations must use the same representative command-family workload.");

        decimal independentPreparationWork = independentMeasurements.Sum(measurement => measurement.PreparationWork);
        decimal independentProjectionWork = independentMeasurements.Sum(measurement => measurement.ProjectionWork);
        decimal independentTotalWork = independentPreparationWork + independentProjectionWork;
        decimal repeatedWorkShare = independentTotalWork <= 0
            ? 0
            : independentPreparationWork / independentTotalWork;
        decimal averageIndependentPreparationWork = independentPreparationWork / representativeProcessCount;
        decimal coldPrepareCost = averageIndependentPreparationWork;
        decimal loadAuthorizationCost = Math.Max(
            1,
            sharedMeasurements.Sum(measurement => measurement.ProjectionWork) / sharedMeasurements.Count);
        decimal loadCostLowerBound = Math.Max(0, sharedMeasurements.Min(measurement => measurement.ProjectionWork));
        decimal loadCostUpperBound = Math.Max(loadCostLowerBound, sharedMeasurements.Max(measurement => measurement.ProjectionWork));
        long candidateWork = Math.Max(0, (long)Math.Ceiling(averageIndependentPreparationWork));
        long cacheAvoidableWork = Math.Min(
            candidateWork,
            (long)Math.Ceiling(
                independentMeasurements.Sum(measurement => measurement.CacheAvoidableWork) /
                (decimal)representativeProcessCount));
        long preparedStateAvoidableWork = candidateWork - cacheAvoidableWork;
        decimal measuredIndependentWorkflowWork = independentTotalWork;
        IReadOnlyDictionary<string, IReadOnlyList<CrossProcessProcessEvidence>> processBoundFamilies = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind == PreparationExecutionKind.ProcessBoundProjection)
            .GroupBy(process => process.Identity.Projection.CommandFamily)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CrossProcessProcessEvidence>)group.ToList(), StringComparer.Ordinal);
        IReadOnlyList<string> requiredProcessBoundFamilies = _measuredCommandFamilies
            .Except(_sharedProjectionFamilies, StringComparer.Ordinal)
            .ToList();
        IReadOnlyList<string> missingOneProcessWorkEvidenceFamilies = requiredProcessBoundFamilies
            .Where(family => !processBoundFamilies.ContainsKey(family) || !processBoundFamilies[family]
                .Select(process => TryReadCounterWork(process.Sample.RawAnalysisProfile))
                .Any(measurement => measurement is not null))
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToList();
        bool oneProcessWorkEvidenceComplete = missingOneProcessWorkEvidenceFamilies.Count == 0;
        IReadOnlyList<CounterWorkMeasurement> processBoundMeasurements = requiredProcessBoundFamilies
            .Where(processBoundFamilies.ContainsKey)
            .SelectMany(family => processBoundFamilies[family])
            .Select(process => TryReadCounterWork(process.Sample.RawAnalysisProfile))
            .Where(measurement => measurement is not null)
            .Select(measurement => measurement!)
            .ToList();
        decimal? measuredOneProcessAlternativeWork = oneProcessWorkEvidenceComplete
            ? coldPrepareCost +
              sharedMeasurements.Sum(measurement => measurement.ProjectionWork) +
              processBoundMeasurements
                  .Sum(measurement => (decimal)measurement.PreparationWork + measurement.ProjectionWork)
            : null;
        decimal unavoidableProjectionWork = oneProcessWorkEvidenceComplete
            ? sharedMeasurements.Sum(measurement => measurement.ProjectionWork) +
              processBoundMeasurements.Sum(measurement => measurement.ProjectionWork)
            : 0;
        IReadOnlyList<PreparedEffectScalePoint> scaleEvidence = CreateScaleEvidence(
            measuredScalePoints,
            representativeProcessCount,
            loadAuthorizationCost);
        string workMeasurementBasis = oneProcessWorkEvidenceComplete
            ? $"Summed preparation/projection counters for one disabled-cache independent process per required command family ({string.Join(", ", _measuredCommandFamilies)}), with cold preparation derived only from independent preparation counters. The persisted comparison adds the same measured unavoidable projection/command work to its total. Cache miss/hit samples remain supplemental and are excluded from the comparable workload."
            : $"One-process comparison is incomplete; real profile counters/work evidence are missing for: {string.Join(", ", missingOneProcessWorkEvidenceFamilies)}. Missing work is not treated as zero.";

        PreparedEffectContract effect = new()
        {
            IssueReference = "#493",
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
                "Measured one-process projection work; used as the explicit load/authorization proxy until persisted storage exists.",
            UnavoidableProjectionWork = unavoidableProjectionWork,
            MeasuredIndependentWorkflowWork = measuredIndependentWorkflowWork,
            MeasuredOneProcessAlternativeWork = measuredOneProcessAlternativeWork,
            OneProcessWorkEvidenceComplete = oneProcessWorkEvidenceComplete,
            MissingOneProcessWorkEvidenceFamilies = missingOneProcessWorkEvidenceFamilies,
            ExpectedPersistedReuseWork = 0,
            DistinctCrossProcessValue = false,
            MaterialSavingsThreshold = 0.10m,
            MeasuredMaterialSavingsRatio = 0,
            WorkMeasurementBasis = workMeasurementBasis,
            BreakEvenProcessCount = null,
            CacheModesMeasured = ["disabled", "miss", "hit"],
            Resources = CreatePreparationResourceEvidence(workload, representativeIndependentProcesses, representativeProcessCount),
            ScaleEvidenceBasis =
                $"Measured analysis-profile/v1 counters for small, medium, and large {workload.CompilationMode} workloads; each point runs one cache-disabled process for every representative command family ({string.Join(", ", _measuredCommandFamilies)}).",
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
            DistinctCrossProcessValue = oneProcessWorkEvidenceComplete &&
                expectedPersistedReuseWork < measuredOneProcessAlternativeWork!.Value,
        };
    }

    private static IReadOnlyList<PreparedEffectScalePoint> CreateScaleEvidence(
        IReadOnlyList<MeasuredScalePoint> measuredScalePoints,
        int representativeProcessCount,
        decimal loadAuthorizationCost)
    {
        return measuredScalePoints
            .Select(point =>
            {
                decimal coldPrepareCost = point.IndependentPreparationWork / point.CommandCount;
                decimal independentWork = point.IndependentPreparationWork + point.IndependentProjectionWork;
                decimal persistedWork = coldPrepareCost +
                    representativeProcessCount * loadAuthorizationCost +
                    point.IndependentProjectionWork;
                decimal expectedLocalSpeedup = persistedWork > 0
                    ? independentWork / persistedWork
                    : 0;
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
                    ColdPrepareCost = coldPrepareCost,
                    ExpectedLocalSpeedup = expectedLocalSpeedup,
                    MeasurementBasis = "Measured analysis-profile/v1 counters from one cache-disabled CLI process per command family.",
                };
            })
            .ToList();
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
        if (!effect.OneProcessWorkEvidenceComplete)
        {
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.C,
                Route = "route-instrumentation-gap",
                Reason = $"Required process-bound work evidence is missing for: {string.Join(", ", effect.MissingOneProcessWorkEvidenceFamilies)}. The one-process alternative is not decision-capable, and missing work is not treated as zero.",
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
                Reason = $"Measured counter work shows persisted reuse is materially cheaper than the measured one-process alternative ({effect.MeasuredMaterialSavingsRatio.ToString("P1", CultureInfo.InvariantCulture)} savings versus a {effect.MaterialSavingsThreshold.ToString("P1", CultureInfo.InvariantCulture)} threshold), with a strict break-even under the measured load-cost range.",
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
        long cacheAvoidableWork = SumObject(
            counters.TryGetProperty("Cache", out JsonElement cache) ? cache : default,
            "AvoidedAssemblyLoads",
            "AvoidedFactIndexMaterializations",
            "AvoidedSourceScanPasses",
            "AvoidedContractExecutions");
        if (preparationWork == 0 && projectionWork == 0)
        {
            return null;
        }

        return new CounterWorkMeasurement(preparationWork, projectionWork, cacheAvoidableWork);
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
        long CacheAvoidableWork);
}
