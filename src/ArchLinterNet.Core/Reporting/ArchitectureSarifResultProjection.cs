using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

internal sealed record ArchitectureSarifResultEntry(
    string RuleId,
    string ContractName,
    string SourceIdentifier,
    string Category,
    Dictionary<string, object?> Json);

// A single comparer preserves the former RuleId/SourceIdentifier/Category ordering while
// observing cancellation throughout the sort.
internal sealed class ArchitectureSarifResultEntryOrderComparer : IComparer<ArchitectureSarifResultEntry>
{
    private readonly CancellationToken _cancellationToken;

    internal ArchitectureSarifResultEntryOrderComparer(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public int Compare(ArchitectureSarifResultEntry? x, ArchitectureSarifResultEntry? y)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        int result = StringComparer.Ordinal.Compare(x!.RuleId, y!.RuleId);
        if (result != 0)
        {
            return result;
        }

        result = StringComparer.Ordinal.Compare(x.SourceIdentifier, y.SourceIdentifier);
        if (result != 0)
        {
            return result;
        }

        return StringComparer.Ordinal.Compare(x.Category, y.Category);
    }
}

internal static class ArchitectureSarifCoverageSummaryProjector
{
    internal static object[] Format(IReadOnlyCollection<ArchitectureCoverageSummary> summaries)
    {
        return summaries.OrderBy(summary => summary.ContractId ?? summary.ContractName, StringComparer.Ordinal)
            .Select(summary => (object)new Dictionary<string, object?>
            {
                ["contract"] = summary.ContractName,
                ["contract_id"] = summary.ContractId,
                ["scope"] = summary.Scope,
                ["optional_empty_items"] = summary.OptionalEmptyItems
                    .OrderBy(item => item.Item, StringComparer.Ordinal)
                    .Select(item => (object)new Dictionary<string, object?>
                    {
                        ["item"] = item.Item,
                        ["contract_id"] = item.ContractId,
                        ["input"] = item.Input,
                        ["layer"] = item.Layer,
                        ["reason"] = item.Reason,
                        ["evidence"] = item.Evidence,
                        ["policy_location"] = item.PolicyLocation is null ? null : new Dictionary<string, object?>
                        {
                            ["source_path"] = item.PolicyLocation.SourcePath,
                            ["yaml_path"] = item.PolicyLocation.YamlPath,
                            ["line"] = item.PolicyLocation.Line,
                            ["column"] = item.PolicyLocation.Column,
                        },
                    }).ToArray(),
            }).ToArray();
    }
}
