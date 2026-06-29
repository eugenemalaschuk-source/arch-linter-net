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

internal sealed class CoverageContractHandler : IArchitectureContractHandler
{
    public string Family => "coverage";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckCoverageContract((ArchitectureCoverageContract)contract));
    }
}

internal sealed class AllowOnlyContractHandler : IArchitectureContractHandler
{
    public string Family => "allow_only";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckAllowOnlyContract((ArchitectureAllowOnlyContract)contract));
    }
}

internal sealed class MethodBodyContractHandler : IArchitectureContractHandler
{
    public string Family => "method_body";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckMethodBodyContract((ArchitectureMethodBodyContract)contract));
    }
}

internal sealed class AsmdefContractHandler : IArchitectureContractHandler
{
    public string Family => "asmdef";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckAsmdefContract((ArchitectureAsmdefContract)contract));
    }
}

internal sealed class IndependenceContractHandler : IArchitectureContractHandler
{
    public string Family => "independence";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckIndependenceContract((ArchitectureIndependenceContract)contract));
    }
}

internal sealed class ProtectedContractHandler : IArchitectureContractHandler
{
    public string Family => "protected";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckProtectedContract((ArchitectureProtectedContract)contract));
    }
}

internal sealed class ExternalContractHandler : IArchitectureContractHandler
{
    public string Family => "external";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            runner.CheckExternalContract((ArchitectureExternalDependencyContract)contract));
    }
}

internal sealed class AcyclicSiblingContractHandler : IArchitectureContractHandler
{
    public string Family => "acyclic_sibling";

    public ArchitectureHandlerResult Execute(ArchitectureContractRunner runner, IArchitectureContract contract)
    {
        var acyclicSiblingContract = (ArchitectureAcyclicSiblingContract)contract;
        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(acyclicSiblingContract);
        string idPrefix = acyclicSiblingContract.Id != null ? $"[{acyclicSiblingContract.Id}] " : string.Empty;
        return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
    }
}
