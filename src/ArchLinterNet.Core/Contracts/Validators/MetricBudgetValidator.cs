using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class MetricBudgetValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> metricIds = document.Metrics
            .Select(metric => metric.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> budgetIds = new(StringComparer.OrdinalIgnoreCase);

        ValidateGroup(document, "strict_metric_budgets", document.Contracts.StrictMetricBudgets, metricIds, budgetIds);
        ValidateGroup(document, "audit_metric_budgets", document.Contracts.AuditMetricBudgets, metricIds, budgetIds);
    }

    private static void ValidateGroup(
        ArchitectureContractDocument document,
        string group,
        IReadOnlyList<ArchitectureMetricBudgetContract> budgets,
        IReadOnlySet<string> metricIds,
        ISet<string> budgetIds)
    {
        for (int index = 0; index < budgets.Count; index++)
        {
            ArchitectureMetricBudgetContract budget = budgets[index];
            document.Provenance.SetValidationSubject(
                ArchitecturePolicyProvenancePath.AppendIndex(
                    ArchitecturePolicyProvenancePath.AppendProperty(
                        ArchitecturePolicyProvenancePath.Property("contracts"), group),
                    index));

            if (string.IsNullOrWhiteSpace(budget.Id))
            {
                throw new InvalidOperationException("Every metric budget must declare a non-empty id.");
            }

            if (!budgetIds.Add(budget.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate metric budget id '{budget.Id}'. Each metric budget ID must be unique across strict and audit modes.");
            }

            if (string.IsNullOrWhiteSpace(budget.Metric))
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' must reference a non-empty metric ID.");
            }

            if (!metricIds.Contains(budget.Metric))
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' references unknown metric '{budget.Metric}'.");
            }

            if (budget.BaselineMode is not null
                && budget.BaselineMode is not ("no_worse_than_baseline" or "max_delta"))
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' has unsupported baseline_mode '{budget.BaselineMode}'. " +
                    "Supported values are 'no_worse_than_baseline' and 'max_delta'.");
            }

            if (budget.BaselineMode is null)
            {
                if (budget.Minimum is null && budget.Maximum is null)
                {
                    throw new InvalidOperationException(
                        $"Metric budget '{budget.Id}' must declare at least one of 'minimum' or 'maximum'.");
                }

                if (budget.MaxDelta is not null)
                {
                    throw new InvalidOperationException(
                        $"Metric budget '{budget.Id}' may declare 'max_delta' only with baseline_mode 'max_delta'.");
                }
            }
            else
            {
                if (budget.Minimum is not null)
                {
                    throw new InvalidOperationException(
                        $"Metric budget '{budget.Id}' cannot declare 'minimum' with baseline_mode '{budget.BaselineMode}'.");
                }

                if (budget.BaselineMode == "max_delta" && budget.MaxDelta is null)
                {
                    throw new InvalidOperationException(
                        $"Metric budget '{budget.Id}' requires 'max_delta' with baseline_mode 'max_delta'.");
                }

                if (budget.BaselineMode == "no_worse_than_baseline" && budget.MaxDelta is not null)
                {
                    throw new InvalidOperationException(
                        $"Metric budget '{budget.Id}' must not declare 'max_delta' with baseline_mode 'no_worse_than_baseline'.");
                }
            }

            if (budget.Minimum is < 0)
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' minimum must be non-negative.");
            }

            if (budget.Maximum is < 0)
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' maximum must be non-negative.");
            }

            if (budget.MaxDelta is < 0)
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' max_delta must be non-negative.");
            }

            if (budget.Minimum is { } minimum && budget.Maximum is { } maximum && minimum > maximum)
            {
                throw new InvalidOperationException(
                    $"Metric budget '{budget.Id}' minimum must be less than or equal to maximum.");
            }
        }
    }
}
