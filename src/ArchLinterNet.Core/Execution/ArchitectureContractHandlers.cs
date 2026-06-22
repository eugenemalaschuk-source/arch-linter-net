using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

internal sealed class DependencyContractHandler : IArchitectureContractHandler
{
    public string Family => "dependency";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckContract((ArchitectureDependencyContract)contract));
    }
}

internal sealed class LayerContractHandler : IArchitectureContractHandler
{
    public string Family => "layer";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckLayerContract((ArchitectureLayerContract)contract));
    }
}

internal sealed class CycleContractHandler : IArchitectureContractHandler
{
    public string Family => "cycle";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        var cycleContract = (ArchitectureCycleContract)contract;
        IReadOnlyCollection<string> cycles = runner.CheckCycleContract(cycleContract);
        string idPrefix = cycleContract.Id != null ? $"[{cycleContract.Id}] " : string.Empty;
        return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
    }
}
