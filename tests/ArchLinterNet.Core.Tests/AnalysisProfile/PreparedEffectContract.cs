using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests;

internal sealed record PreparedEffectScalePoint
{
    public required string Label { get; init; }

    public required string WorkloadId { get; init; }

    public required string WorkloadIdentity { get; init; }

    public required int ProjectCount { get; init; }

    public required int TypeCount { get; init; }

    public required int SourceFileCount { get; init; }

    public required int ReferenceEdgeCount { get; init; }

    public required int CommandCount { get; init; }

    public required decimal IndependentPreparationWork { get; init; }

    public required decimal IndependentProjectionWork { get; init; }

    public required decimal PerConsumerLoadAuthorizationCost { get; init; }

    public required decimal ColdPrepareCost { get; init; }

    public required decimal ExpectedLocalSpeedup { get; init; }

    public required string MeasurementBasis { get; init; }

    public void Validate(string fieldName, int representativeProcessCount)
    {
        if (string.IsNullOrWhiteSpace(Label) || string.IsNullOrWhiteSpace(WorkloadId) ||
            string.IsNullOrWhiteSpace(WorkloadIdentity) || string.IsNullOrWhiteSpace(MeasurementBasis) ||
            representativeProcessCount < 1 || ProjectCount < 1 || TypeCount < 1 || SourceFileCount < 1 || ReferenceEdgeCount < 0 ||
            CommandCount < 1 || IndependentPreparationWork < 0 || IndependentProjectionWork < 0 ||
            PerConsumerLoadAuthorizationCost <= 0 || ColdPrepareCost < 0 || ExpectedLocalSpeedup <= 0)
        {
            throw new InvalidOperationException($"Scale evidence '{fieldName}' contains invalid dimensions or measurements.");
        }

        if (WorkloadIdentity.Length != 64 ||
            WorkloadIdentity.Any(character => !Uri.IsHexDigit(character)) ||
            WorkloadIdentity != WorkloadIdentity.ToLowerInvariant())
        {
            throw new InvalidOperationException($"Scale evidence '{fieldName}' must retain a lowercase workload SHA-256 identity.");
        }

        decimal expectedColdPrepareCost = IndependentPreparationWork / CommandCount;
        if (ColdPrepareCost != expectedColdPrepareCost)
        {
            throw new InvalidOperationException(
                $"Scale evidence '{fieldName}' must derive cold preparation from measured preparation counters and command count.");
        }

        decimal independentWork = IndependentPreparationWork + IndependentProjectionWork;
        decimal persistedWork = ColdPrepareCost +
            representativeProcessCount * PerConsumerLoadAuthorizationCost +
            IndependentProjectionWork;
        decimal expectedLocalSpeedup = persistedWork > 0
            ? independentWork / persistedWork
            : 0;
        if (ExpectedLocalSpeedup != expectedLocalSpeedup)
        {
            throw new InvalidOperationException(
                $"Scale evidence '{fieldName}' must derive expected local speedup from its own measured load/authorization cost.");
        }
    }
}

internal sealed record PreparedEffectContract
{
    public required string IssueReference { get; init; }

    public required int RepresentativeProcessCount { get; init; }

    public required decimal RepeatedWorkShare { get; init; }

    public required long CandidatePreparedBoundaryWork { get; init; }

    public required long CacheAvoidableWork { get; init; }

    public required long PreparedStateAvoidableWork { get; init; }

    public required decimal ColdPrepareCost { get; init; }

    public required decimal PerConsumerLoadAuthorizationCost { get; init; }

    public required decimal PerConsumerLoadAuthorizationCostLowerBound { get; init; }

    public required decimal PerConsumerLoadAuthorizationCostUpperBound { get; init; }

    public required string PerConsumerLoadAuthorizationCostBasis { get; init; }

    public required decimal UnavoidableProjectionWork { get; init; }

    public required decimal MeasuredIndependentWorkflowWork { get; init; }

    public required decimal? MeasuredOneProcessAlternativeWork { get; init; }

    public required bool OneProcessWorkEvidenceComplete { get; init; }

    public required IReadOnlyList<string> MissingOneProcessWorkEvidenceFamilies { get; init; }

    public required decimal ExpectedPersistedReuseWork { get; init; }

    public required bool DistinctCrossProcessValue { get; init; }

    public required decimal MaterialSavingsThreshold { get; init; }

    public required decimal MeasuredMaterialSavingsRatio { get; init; }

    public required string WorkMeasurementBasis { get; init; }

    public required int? BreakEvenProcessCount { get; init; }

    public required IReadOnlyList<string> CacheModesMeasured { get; init; }

    public required PreparationResourceEvidence Resources { get; init; }

    public required string ScaleEvidenceBasis { get; init; }

    public required IReadOnlyList<PreparedEffectScalePoint> ScaleEvidence { get; init; }

    public required BenchmarkExpectedEffectEvidence ExpectedEffect { get; init; }

    public required bool ExactCacheHitSavingsExcluded { get; init; }

    [JsonIgnore]
    public long PreparedStateOnlyWork => CandidatePreparedBoundaryWork - CacheAvoidableWork;

    [JsonIgnore]
    public bool MateriallyCheaper => OneProcessWorkEvidenceComplete &&
        MeasuredOneProcessAlternativeWork is > 0 &&
        MeasuredMaterialSavingsRatio >= MaterialSavingsThreshold;

    public decimal IndependentOneShotCost(int processCount) =>
        ValidateProcessCount(processCount) * ColdPrepareCost;

    public decimal PreparedReuseCost(int processCount) =>
        ColdPrepareCost + ValidateProcessCount(processCount) * PerConsumerLoadAuthorizationCost + UnavoidableProjectionWork;

    public decimal ExpectedSavings(int processCount) =>
        IndependentOneShotCost(processCount) - PreparedReuseCost(processCount);

    public int? CalculateBreakEvenProcessCount()
    {
        decimal repeatedCostPerConsumer = ColdPrepareCost;
        decimal differencePerConsumer = repeatedCostPerConsumer - PerConsumerLoadAuthorizationCost;
        if (differencePerConsumer <= 0 || ColdPrepareCost <= 0)
        {
            return null;
        }

        decimal firstStrictlyCheaper = decimal.Floor((ColdPrepareCost + UnavoidableProjectionWork) / differencePerConsumer) + 1;
        return firstStrictlyCheaper > int.MaxValue ? null : (int)firstStrictlyCheaper;
    }

    public void Validate()
    {
        if (!string.Equals(IssueReference, "#493", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The prepared-effect contract must identify issue #493.");
        }

        if (RepresentativeProcessCount < 1 || RepeatedWorkShare is < 0 or > 1 ||
            ColdPrepareCost < 0 || PerConsumerLoadAuthorizationCost < 0 ||
            PerConsumerLoadAuthorizationCostLowerBound < 0 ||
            PerConsumerLoadAuthorizationCostUpperBound < PerConsumerLoadAuthorizationCostLowerBound ||
            PerConsumerLoadAuthorizationCost < PerConsumerLoadAuthorizationCostLowerBound ||
            PerConsumerLoadAuthorizationCost > PerConsumerLoadAuthorizationCostUpperBound ||
            UnavoidableProjectionWork < 0 ||
            MeasuredIndependentWorkflowWork < 0 || MeasuredOneProcessAlternativeWork is < 0 ||
            ExpectedPersistedReuseWork < 0 || MaterialSavingsThreshold <= 0 ||
            MaterialSavingsThreshold > 1 || MeasuredMaterialSavingsRatio > 1)
        {
            throw new InvalidOperationException("Prepared-effect counts, shares, and costs must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(PerConsumerLoadAuthorizationCostBasis) ||
            string.IsNullOrWhiteSpace(WorkMeasurementBasis))
        {
            throw new InvalidOperationException("Prepared-effect measurements require an explicit basis.");
        }

        if (MissingOneProcessWorkEvidenceFamilies.Count !=
                MissingOneProcessWorkEvidenceFamilies.Distinct(StringComparer.Ordinal).Count() ||
            MissingOneProcessWorkEvidenceFamilies.Any(family => string.IsNullOrWhiteSpace(family)))
        {
            throw new InvalidOperationException("Missing one-process work evidence families must be unique and named.");
        }

        if (OneProcessWorkEvidenceComplete != (MissingOneProcessWorkEvidenceFamilies.Count == 0))
        {
            throw new InvalidOperationException("One-process work evidence completeness must match the missing-family list.");
        }

        if (OneProcessWorkEvidenceComplete && !MeasuredOneProcessAlternativeWork.HasValue)
        {
            throw new InvalidOperationException("Complete one-process work evidence must include a measured alternative cost.");
        }

        decimal expectedMaterialSavingsRatio = OneProcessWorkEvidenceComplete &&
            MeasuredOneProcessAlternativeWork is > 0
            ? (MeasuredOneProcessAlternativeWork.Value - ExpectedPersistedReuseWork) /
              MeasuredOneProcessAlternativeWork.Value
            : 0;
        if (MeasuredMaterialSavingsRatio != expectedMaterialSavingsRatio)
        {
            throw new InvalidOperationException(
                "Measured material savings must be derived from the measured one-process and persisted work values.");
        }

        if (!OneProcessWorkEvidenceComplete &&
            (MeasuredOneProcessAlternativeWork.HasValue || DistinctCrossProcessValue ||
             MeasuredMaterialSavingsRatio != 0))
        {
            throw new InvalidOperationException(
                "Incomplete one-process work evidence cannot claim a measured alternative cost, material savings, or distinct cross-process value.");
        }

        if (CandidatePreparedBoundaryWork < 0 || CacheAvoidableWork < 0 || PreparedStateAvoidableWork < 0 ||
            CacheAvoidableWork > CandidatePreparedBoundaryWork ||
            PreparedStateAvoidableWork > CandidatePreparedBoundaryWork - CacheAvoidableWork)
        {
            throw new InvalidOperationException("Cache-avoidable and prepared-state work must be disjoint candidate work.");
        }

        if (BreakEvenProcessCount != CalculateBreakEvenProcessCount())
        {
            throw new InvalidOperationException("The recorded break-even point does not match the expected-effect calculation.");
        }

        if (ExpectedPersistedReuseWork != PreparedReuseCost(RepresentativeProcessCount))
        {
            throw new InvalidOperationException("The recorded persisted reuse work does not match the expected-effect calculation.");
        }

        if (OneProcessWorkEvidenceComplete &&
            DistinctCrossProcessValue != (ExpectedPersistedReuseWork < MeasuredOneProcessAlternativeWork!.Value))
        {
            throw new InvalidOperationException("Distinct cross-process value must be derived from measured one-process and persisted costs.");
        }

        if (string.IsNullOrWhiteSpace(ScaleEvidenceBasis) || ScaleEvidence.Count < 3)
        {
            throw new InvalidOperationException(
                "Prepared-effect evidence requires measured small, medium, and large scale points.");
        }

        if (ScaleEvidence.Select(point => point.Label).Distinct(StringComparer.Ordinal).Count() != ScaleEvidence.Count ||
            !new[] { "small", "medium", "large" }.All(label =>
                ScaleEvidence.Any(point => string.Equals(point.Label, label, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Prepared-effect scale evidence must contain distinct small, medium, and large points.");
        }

        foreach (PreparedEffectScalePoint point in ScaleEvidence)
        {
            point.Validate($"prepared_effect.scale_evidence[{point.Label}]", RepresentativeProcessCount);
        }

        IReadOnlyDictionary<string, decimal> speedups = ScaleEvidence.ToDictionary(
            point => point.Label,
            point => point.ExpectedLocalSpeedup,
            StringComparer.Ordinal);
        if (ExpectedEffect.ExpectedLocalSpeedupSmall != speedups["small"] ||
            ExpectedEffect.ExpectedLocalSpeedupMedium != speedups["medium"] ||
            ExpectedEffect.ExpectedLocalSpeedupLarge != speedups["large"])
        {
            throw new InvalidOperationException(
                "Expected local speedups must be copied from the measured small, medium, and large scale points.");
        }

        if (CacheModesMeasured.Count == 0 || CacheModesMeasured.Any(mode => !PreparationContractIdentity.IsCacheMode(mode)))
        {
            throw new InvalidOperationException("Prepared-effect evidence must declare known cache modes.");
        }

        Resources.Validate("prepared_effect.resources");
        if (!string.Equals(ExpectedEffect.IssueReference, "#493", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected-effect evidence must identify issue #493.");
        }

        if (!ExactCacheHitSavingsExcluded)
        {
            throw new InvalidOperationException("Exact analysis-cache hits must be excluded from prepared-state savings.");
        }
    }

    private static int ValidateProcessCount(int processCount) => processCount > 0
        ? processCount
        : throw new ArgumentOutOfRangeException(nameof(processCount));
}
