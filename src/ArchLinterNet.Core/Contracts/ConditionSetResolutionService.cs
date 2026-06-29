namespace ArchLinterNet.Core.Contracts;

public interface IConditionSetResolutionService
{
    bool TryResolve(
        ArchitectureContractDocument document,
        string? explicitName,
        out IReadOnlyList<string> symbols,
        out string? error);
}

public sealed class ConditionSetResolutionService : IConditionSetResolutionService
{
    public bool TryResolve(
        ArchitectureContractDocument document,
        string? explicitName,
        out IReadOnlyList<string> symbols,
        out string? error)
    {
        return ConditionSetResolver.TryResolve(document, explicitName, out symbols, out error);
    }
}
