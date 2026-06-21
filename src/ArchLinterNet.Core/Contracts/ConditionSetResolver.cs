namespace ArchLinterNet.Core.Contracts;

public static class ConditionSetResolver
{
    public static bool TryResolve(
        ArchitectureContractDocument document,
        string? explicitName,
        out IReadOnlyList<string> symbols,
        out string? error)
    {
        string name = explicitName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = document.Analysis.DefaultConditionSet;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            symbols = Array.Empty<string>();
            error = null;
            return true;
        }

        if (document.Analysis.ConditionSets.TryGetValue(name, out List<string>? found))
        {
            symbols = found;
            error = null;
            return true;
        }

        string available = document.Analysis.ConditionSets.Keys.Count > 0
            ? $" Available condition sets: {string.Join(", ", document.Analysis.ConditionSets.Keys.OrderBy(x => x))}."
            : string.Empty;
        error = $"Unknown condition set: '{name}'.{available}";
        symbols = Array.Empty<string>();
        return false;
    }
}
