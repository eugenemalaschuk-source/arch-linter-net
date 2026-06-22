using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureContractHandlerRegistry
{
    private readonly Dictionary<string, IArchitectureContractHandler> _handlersByFamily;

    private ArchitectureContractHandlerRegistry(Dictionary<string, IArchitectureContractHandler> handlersByFamily)
    {
        _handlersByFamily = handlersByFamily;
    }

    public static ArchitectureContractHandlerRegistry CreateDefault()
    {
        IArchitectureContractHandler dependencyHandler = new DependencyContractHandler();
        IArchitectureContractHandler layerHandler = new LayerContractHandler();
        IArchitectureContractHandler cycleHandler = new CycleContractHandler();

        return new ArchitectureContractHandlerRegistry(new Dictionary<string, IArchitectureContractHandler>(StringComparer.Ordinal)
        {
            ["dependency"] = dependencyHandler,
            ["layer"] = layerHandler,
            ["layer_template"] = layerHandler,
            ["cycle"] = cycleHandler,
        });
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
