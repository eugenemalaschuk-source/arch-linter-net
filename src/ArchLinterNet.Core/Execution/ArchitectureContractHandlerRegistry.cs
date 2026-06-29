using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureContractHandlerRegistry
{
    private readonly Dictionary<string, IArchitectureContractHandler> _handlersByFamily;

    public ArchitectureContractHandlerRegistry(IEnumerable<IArchitectureContractHandler> handlers)
    {
        _handlersByFamily = new Dictionary<string, IArchitectureContractHandler>(StringComparer.Ordinal);

        foreach (IArchitectureContractHandler handler in handlers)
        {
            _handlersByFamily[handler.Family] = handler;
        }

        // "layer_template" contracts are expanded into ArchitectureLayerContract instances before
        // execution, so they share the "layer" family's handler. The handler interface only reports
        // one Family, so the alias is added here rather than duplicating the handler.
        if (_handlersByFamily.TryGetValue("layer", out IArchitectureContractHandler? layerHandler))
        {
            _handlersByFamily["layer_template"] = layerHandler;
        }
    }

    public bool TryGetHandler(string family, out IArchitectureContractHandler? handler)
    {
        return _handlersByFamily.TryGetValue(family, out handler);
    }

    public ArchitectureHandlerResult Execute(string family, ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        if (!_handlersByFamily.TryGetValue(family, out IArchitectureContractHandler? handler))
        {
            throw new InvalidOperationException($"No contract handler registered for family '{family}'.");
        }

        return handler.Execute(runner, contract);
    }
}
