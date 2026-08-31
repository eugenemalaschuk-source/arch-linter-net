using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Evaluates all selected metric budgets in one pass. A metric is measured once for the whole
// family, then its immutable measurement/evidence is reused by every budget that references it.
internal static class ArchitectureMetricBudgetAnalysisService
{
    internal const string Family = "metric_budgets";

    internal static ArchitectureMetricBudgetEvaluationResult Evaluate(
        ArchitectureAnalysisSession session,
        IReadOnlyCollection<ArchitectureMetricBudgetContract> contracts)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(contracts);

        ArchitectureMetricBudgetContract[] budgets = contracts
            .Where(session.IsContractSelected)
            .OrderBy(contract => contract.Id ?? contract.Name, StringComparer.Ordinal)
            .ToArray();
        if (budgets.Length == 0)
        {
            return ArchitectureMetricBudgetEvaluationResult.Empty;
        }

        HashSet<string> declaredMetricIds = session.Document.Metrics
            .Select(metric => metric.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] selectedMetricIds = budgets
            .Select(contract => contract.Metric)
            .Where(declaredMetricIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        ArchitectureMetricMeasurementOutcome measurements = selectedMetricIds.Length == 0
            ? new ArchitectureMetricMeasurementOutcome(
                Array.Empty<ArchitectureMetricMeasurement>(), null, null)
            : ArchitectureMetricEvaluator.Evaluate(session, session.Document.Metrics, selectedMetricIds);
        Dictionary<string, ArchitectureMetricMeasurement> measurementsById = measurements.Measurements
            .ToDictionary(measurement => measurement.Id, StringComparer.Ordinal);
        Dictionary<string, ArchitectureApplicabilityRecord> recordsById = measurements.ApplicabilityRecords
            .ToDictionary(record => record.ControlIdentity, StringComparer.Ordinal);

        var violations = new List<ArchitectureViolation>();
        var expected = new List<ArchitectureApplicabilityExpectedEntry>(budgets.Length);
        var records = new List<ArchitectureApplicabilityRecord>(budgets.Length);
        Dictionary<ArchitectureMetricBudgetContract, ArchitectureContractExecutionContext> executionContexts =
            budgets.ToDictionary(
                budget => budget,
                budget => session.CreateExecutionContext(budget, budget.IgnoredViolations));
        foreach (ArchitectureMetricBudgetContract budget in budgets)
        {
            string budgetId = budget.Id ?? budget.Name;
            ArchitectureApplicabilityProvenance provenance =
                new(Family, budgetId, session.Document.Name);
            expected.Add(new ArchitectureApplicabilityExpectedEntry(
                budgetId, Family, ArchitectureApplicabilityMembership.Required, provenance));

            if (!measurementsById.TryGetValue(budget.Metric, out ArchitectureMetricMeasurement? measurement))
            {
                records.Add(new ArchitectureApplicabilityRecord(
                    budgetId,
                    Family,
                    ArchitectureApplicabilityRecordState.Unassessable,
                    [new ArchitectureApplicabilityReason(
                        ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance)],
                    provenance));
                continue;
            }

            ArchitectureApplicabilityRecord? metricRecord = recordsById.GetValueOrDefault(budget.Metric);
            if (!measurement.IsEvaluable || metricRecord is null)
            {
                IReadOnlyList<ArchitectureApplicabilityReason> reasons = metricRecord?.Reasons
                    .Select(reason => new ArchitectureApplicabilityReason(reason.Code, provenance))
                    .ToArray()
                    ?? [new ArchitectureApplicabilityReason(
                        ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance)];
                records.Add(new ArchitectureApplicabilityRecord(
                    budgetId, Family, ArchitectureApplicabilityRecordState.Unassessable, reasons, provenance)
                {
                    MetricEvidence = metricRecord?.MetricEvidence,
                });
                continue;
            }

            records.Add(new ArchitectureApplicabilityRecord(
                budgetId, Family, ArchitectureApplicabilityRecordState.Evaluable, provenance)
            {
                MetricEvidence = metricRecord.MetricEvidence,
            });

            if (budget.IsRelative)
            {
                // Capture is an explicit baseline-generation concern. The session only retains
                // complete scalar measurements, and this list is intentionally separate from the
                // finding candidate stream used for ignore matching.
                session.AddMetricBaselineCandidate(new ArchitectureMetricBaselineEntry
                {
                    MetricIdentityVersion = ArchitectureMetricBaselineIdentity.CurrentVersion,
                    MetricId = measurement.Id,
                    MetricKind = measurement.Kind,
                    NativeSubject = measurement.NativeSubject ?? string.Empty,
                    Unit = measurement.Unit,
                    EffectiveScope = measurement.EffectiveScope,
                    Value = measurement.Value!.Value,
                });

                ArchitectureMetricBaselineEntry? reviewed = session.Document.MetricBaselines
                    .FirstOrDefault(entry => string.Equals(entry.MetricId, measurement.Id, StringComparison.Ordinal));
                if (reviewed is null)
                {
                    records[^1] = UnassessableBudgetRecord(
                        budgetId, provenance, metricRecord.MetricEvidence, ArchitectureApplicabilityReasonCodes.MissingMetricBaseline);
                    continue;
                }

                ArchitectureMetricBaselineIdentity currentIdentity = new(
                    ArchitectureMetricBaselineIdentity.CurrentVersion,
                    measurement.Id,
                    measurement.Kind,
                    measurement.NativeSubject ?? string.Empty,
                    measurement.Unit,
                    measurement.EffectiveScope);
                if (!reviewed.Identity.Equals(currentIdentity))
                {
                    records[^1] = UnassessableBudgetRecord(
                        budgetId, provenance, metricRecord.MetricEvidence, ArchitectureApplicabilityReasonCodes.StaleMetricBaseline);
                    continue;
                }

                int baselineValue = reviewed.Value
                    ?? throw new InvalidOperationException("A validated metric baseline must have a value.");
                int currentValue = measurement.Value!.Value;
                int delta = currentValue - baselineValue;
                int allowedDelta = budget.AllowedDelta;
                long relativeThreshold = (long)baselineValue + allowedDelta;
                long effectiveThreshold = budget.Maximum is { } absoluteCap
                    ? Math.Min(relativeThreshold, absoluteCap)
                    : relativeThreshold;
                if (currentValue > effectiveThreshold)
                {
                    ArchitectureViolation violation = CreateRelativeViolation(
                        budget,
                        measurement,
                        baselineValue,
                        delta,
                        allowedDelta,
                        effectiveThreshold,
                        budget.Maximum);
                    if (!IsIgnored(executionContexts[budget], violation))
                    {
                        violations.Add(violation);
                    }
                }

                continue;
            }

            if (budget.Minimum is { } minimum && measurement.Value < minimum)
            {
                ArchitectureViolation violation = CreateViolation(budget, measurement, "minimum", minimum);
                if (!IsIgnored(executionContexts[budget], violation))
                {
                    violations.Add(violation);
                }
            }

            if (budget.Maximum is { } maximum && measurement.Value > maximum)
            {
                ArchitectureViolation violation = CreateViolation(budget, measurement, "maximum", maximum);
                if (!IsIgnored(executionContexts[budget], violation))
                {
                    violations.Add(violation);
                }
            }
        }

        foreach (ArchitectureContractExecutionContext executionContext in executionContexts.Values)
        {
            session.CollectUnmatchedIgnores(executionContext);
        }

        return new ArchitectureMetricBudgetEvaluationResult(violations, expected, records);
    }

    private static ArchitectureApplicabilityRecord UnassessableBudgetRecord(
        string budgetId,
        ArchitectureApplicabilityProvenance provenance,
        ArchitectureMetricEvidence? metricEvidence,
        string reasonCode) => new(
            budgetId,
            Family,
            ArchitectureApplicabilityRecordState.Unassessable,
            [new ArchitectureApplicabilityReason(reasonCode, provenance)],
            provenance)
        {
            MetricEvidence = metricEvidence,
        };

    private static bool IsIgnored(
        ArchitectureContractExecutionContext executionContext,
        ArchitectureViolation violation)
    {
        ArchitectureViolationIdentity identity = violation.Identity
            ?? throw new InvalidOperationException("Metric-budget violations must carry a baseline identity.");
        return executionContext.IsIgnored(
            violation.SourceType,
            violation.ForbiddenReferences.Single(),
            identity.SourceAssembly,
            identity.TargetAssembly,
            identity.TargetType,
            identity.SourceMember,
            identity.TargetMember,
            identity.Configuration);
    }

    private static ArchitectureViolation CreateViolation(
        ArchitectureMetricBudgetContract budget,
        ArchitectureMetricMeasurement measurement,
        string bound,
        int configuredLimit)
    {
        string budgetId = budget.Id ?? budget.Name;
        string subject = measurement.NativeSubject
            ?? measurement.EffectiveScope
            ?? measurement.Id;
        string identityReference =
            $"metric={measurement.Id};subject={subject};bound={bound};limit={configuredLimit}";
        var identity = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            Family,
            "metric_budget",
            budgetId,
            null,
            subject,
            measurement.Id,
            null,
            measurement.EffectiveScope,
            $"{bound}:{configuredLimit}",
            0,
            measurement.Kind);
        var violation = new ArchitectureViolation(
            budget.Name,
            budget.Id,
            subject,
            Family,
            [identityReference])
        {
            Identity = identity,
            Payload = new MetricBudgetPayload(
                budgetId,
                measurement.Id,
                measurement.Kind,
                measurement.NativeSubject,
                measurement.EffectiveScope ?? string.Empty,
                measurement.Value!.Value,
                bound,
                configuredLimit,
                measurement.Contributors ?? Array.Empty<string>()),
        };

        return violation;
    }

    private static ArchitectureViolation CreateRelativeViolation(
        ArchitectureMetricBudgetContract budget,
        ArchitectureMetricMeasurement measurement,
        int baselineValue,
        int delta,
        int allowedDelta,
        long effectiveThreshold,
        int? absoluteCap)
    {
        string budgetId = budget.Id ?? budget.Name;
        string subject = measurement.NativeSubject ?? measurement.EffectiveScope ?? measurement.Id;
        bool absoluteCapIsEffective = absoluteCap is { } cap && cap <= (long)baselineValue + allowedDelta;
        string bound = absoluteCapIsEffective
            ? "maximum"
            : budget.BaselineMode == "max_delta" ? "max_delta" : "baseline";
        int configuredLimit = absoluteCapIsEffective
            ? absoluteCap!.Value
            : budget.BaselineMode == "max_delta" ? allowedDelta : baselineValue;
        string identityReference =
            $"metric={measurement.Id};subject={subject};bound={bound};limit={effectiveThreshold}";
        var identity = new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            Family,
            "metric_budget",
            budgetId,
            null,
            subject,
            measurement.Id,
            null,
            measurement.EffectiveScope,
            $"{bound}:{effectiveThreshold}",
            0,
            measurement.Kind);
        var violation = new ArchitectureViolation(
            budget.Name,
            budget.Id,
            subject,
            Family,
            [identityReference])
        {
            Identity = identity,
            Payload = new MetricBudgetPayload(
                budgetId,
                measurement.Id,
                measurement.Kind,
                measurement.NativeSubject,
                measurement.EffectiveScope ?? string.Empty,
                measurement.Value!.Value,
                bound,
                configuredLimit,
                measurement.Contributors ?? Array.Empty<string>())
            {
                BaselineMode = budget.BaselineMode,
                BaselineValue = baselineValue,
                Delta = delta,
                AllowedDelta = allowedDelta,
                EffectiveThreshold = effectiveThreshold,
                AbsoluteCap = absoluteCap,
            },
        };

        return violation;
    }
}

internal sealed record ArchitectureMetricBudgetEvaluationResult(
    IReadOnlyList<ArchitectureViolation> Violations,
    IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpectedEntries,
    IReadOnlyList<ArchitectureApplicabilityRecord> ApplicabilityRecords)
{
    internal static ArchitectureMetricBudgetEvaluationResult Empty { get; } =
        new(
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureApplicabilityExpectedEntry>(),
            Array.Empty<ArchitectureApplicabilityRecord>());
}
