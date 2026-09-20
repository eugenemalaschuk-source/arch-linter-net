using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class PreparedAnalysisReuseBenchmarkHarness
{
    private static PreparedEffectContract CreateEffect(
        BenchmarkWorkloadDefinition workload,
        IReadOnlyCollection<CrossProcessProcessEvidence> processes)
    {
        int representativeProcessCount = processes.Count(process =>
            process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
            process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess);
        IReadOnlyList<CounterWorkMeasurement> independentMeasurements = processes
            .Where(process => process.Identity.RevisionRole == PreparationRevisionRole.Candidate &&
                              process.Identity.ExecutionKind == PreparationExecutionKind.IndependentProcess)
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

        decimal independentPreparationWork = Median(independentMeasurements.Select(measurement => measurement.PreparationWork));
        decimal independentProjectionWork = Median(independentMeasurements.Select(measurement => measurement.ProjectionWork));
        decimal independentTotalWork = independentPreparationWork + independentProjectionWork;
        decimal repeatedWorkShare = independentTotalWork <= 0
            ? 0
            : independentPreparationWork / independentTotalWork;
        decimal coldPrepareCost = Math.Max(1, Median(sharedMeasurements.Select(measurement => measurement.PreparationWork)));
        decimal loadAuthorizationCost = Math.Max(1, Median(sharedMeasurements.Select(measurement => measurement.ProjectionWork)));
        decimal loadCostLowerBound = Math.Max(0, sharedMeasurements.Min(measurement => measurement.ProjectionWork));
        decimal loadCostUpperBound = Math.Max(loadCostLowerBound, sharedMeasurements.Max(measurement => measurement.ProjectionWork));
        long candidateWork = Math.Max(1, (long)Math.Ceiling(independentTotalWork));
        long cacheAvoidableWork = Math.Min(
            candidateWork,
            independentMeasurements.Max(measurement => measurement.CacheAvoidableWork));
        long preparedStateAvoidableWork = candidateWork - cacheAvoidableWork;
        decimal measuredIndependentWorkflowWork = independentTotalWork * representativeProcessCount;
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
        decimal? measuredOneProcessAlternativeWork = oneProcessWorkEvidenceComplete
            ? coldPrepareCost + loadAuthorizationCost * sharedMeasurements.Count +
              processBoundFamilies
                  .Values
                  .SelectMany(family => family)
                  .Select(process => TryReadCounterWork(process.Sample.RawAnalysisProfile))
                  .Where(measurement => measurement is not null)
                  .Select(measurement => (decimal)(measurement!.PreparationWork + measurement.ProjectionWork))
                  .Sum()
            : null;
        string workMeasurementBasis = oneProcessWorkEvidenceComplete
            ? "Median preparation/projection counters from candidate independent profiles, shared one-process projections, and all required process-bound projections."
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
            MeasuredIndependentWorkflowWork = measuredIndependentWorkflowWork,
            MeasuredOneProcessAlternativeWork = measuredOneProcessAlternativeWork,
            OneProcessWorkEvidenceComplete = oneProcessWorkEvidenceComplete,
            MissingOneProcessWorkEvidenceFamilies = missingOneProcessWorkEvidenceFamilies,
            ExpectedPersistedReuseWork = 0,
            DistinctCrossProcessValue = false,
            WorkMeasurementBasis = workMeasurementBasis,
            BreakEvenProcessCount = null,
            CacheModesMeasured = ["disabled", "miss", "hit"],
            Resources = new PreparationResourceEvidence
            {
                StorageBytes = BenchmarkResourceMeasurement.Unavailable("No persisted prepared-state store exists before implementation."),
                IoOperations = BenchmarkResourceMeasurement.NotApplicable("No persisted prepared-state I/O exists before implementation."),
                AllocatedBytes = BenchmarkResourceMeasurement.Unavailable("Prepared-state allocation is not isolated."),
                PeakManagedMemory = BenchmarkResourceMeasurement.Unavailable("Prepared-state memory is not isolated."),
            },
            ExpectedEffect = CreateExpectedEffect(representativeProcessCount, coldPrepareCost, loadAuthorizationCost, repeatedWorkShare),
            ExactCacheHitSavingsExcluded = true,
        };
        int? breakEven = effect.CalculateBreakEvenProcessCount();
        decimal expectedPersistedReuseWork = effect.PreparedReuseCost(representativeProcessCount);
        return effect with
        {
            BreakEvenProcessCount = breakEven,
            ExpectedPersistedReuseWork = expectedPersistedReuseWork,
            DistinctCrossProcessValue = oneProcessWorkEvidenceComplete &&
                expectedPersistedReuseWork < measuredOneProcessAlternativeWork!.Value,
        };
    }

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
            effect.ColdPrepareCost * effect.RepeatedWorkShare;
        if (effect.DistinctCrossProcessValue && breakEvenObserved)
        {
            return new PreparationDecision
            {
                Outcome = PreparationDecisionOutcome.A,
                Route = "authorize-prepared-analysis",
                Reason = "Measured counter work shows persisted reuse is cheaper than the measured one-process alternative at the representative process count, with a strict break-even under the measured load-cost range.",
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
                Reason = "The complete one-process alternative was measured, but its measured work is no more expensive than the persisted reuse model or the load-cost range removes a strict representative crossover.",
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

    private static decimal Median(IEnumerable<long> values)
    {
        long[] ordered = values.OrderBy(value => value).ToArray();
        Assert.That(ordered, Is.Not.Empty);
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;
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
