using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureContractHandlerRegistry : IArchitectureContractHandlerRegistry
{
    private readonly Dictionary<string, ArchitectureContractChecker> _checkersByFamily;

    public ArchitectureContractHandlerRegistry()
    {
        _checkersByFamily = new Dictionary<string, ArchitectureContractChecker>(StringComparer.Ordinal);

        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            _checkersByFamily[descriptor.FamilyId] = descriptor.Checker;
        }
    }

    public bool TryGetHandler(string family, out ArchitectureContractChecker? checker)
    {
        return _checkersByFamily.TryGetValue(family, out checker);
    }

    public ArchitectureHandlerResult Execute(string family, ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        if (!_checkersByFamily.TryGetValue(family, out ArchitectureContractChecker? checker))
        {
            throw new InvalidOperationException($"No contract handler registered for family '{family}'.");
        }

        return checker(session, contract);
    }
}
