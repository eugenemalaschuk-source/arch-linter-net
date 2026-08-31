using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Metric budgets are a closed contract shape. Keep unknown properties on the normal policy
// configuration path instead of allowing IgnoreUnmatchedProperties to erase an author typo.
internal sealed class RawMetricBudgetValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _budgetKeys = ["id", "metric", "minimum", "maximum"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        ValidateGroup(document, "strict_metric_budgets");
        ValidateGroup(document, "audit_metric_budgets");
    }

    private static void ValidateGroup(ArchitecturePolicyRawDocument document, string groupKey)
    {
        RawYamlNodes.ForEachContract(document, groupKey,
            (contractNode, _, _) => RawYamlNodes.ValidateKnownKeys(
                contractNode,
                RawYamlNodes.ContractName(contractNode),
                "metric budget contract",
                _budgetKeys));
    }
}
