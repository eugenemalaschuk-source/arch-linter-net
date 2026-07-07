using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;

namespace ArchLinterNet.Core.Execution;

internal sealed class DependencyContractHandler : IArchitectureContractHandler
{
    public string Family => "dependency";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckContract((ArchitectureDependencyContract)contract));
    }
}

internal sealed class LayerContractHandler : IArchitectureContractHandler
{
    public string Family => "layer";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckLayerContract((ArchitectureLayerContract)contract));
    }
}

internal sealed class CycleContractHandler : IArchitectureContractHandler
{
    public string Family => "cycle";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        var cycleContract = (ArchitectureCycleContract)contract;
        IReadOnlyCollection<string> cycles = session.CheckCycleContract(cycleContract);
        string idPrefix = cycleContract.Id != null ? $"[{cycleContract.Id}] " : string.Empty;
        return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
    }
}

internal sealed class CoverageContractHandler : IArchitectureContractHandler
{
    public string Family => "coverage";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckCoverageContract((ArchitectureCoverageContract)contract));
    }
}

internal sealed class AllowOnlyContractHandler : IArchitectureContractHandler
{
    public string Family => "allow_only";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAllowOnlyContract((ArchitectureAllowOnlyContract)contract));
    }
}

internal sealed class MethodBodyContractHandler : IArchitectureContractHandler
{
    public string Family => "method_body";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckMethodBodyContract((ArchitectureMethodBodyContract)contract));
    }
}

internal sealed class AsmdefContractHandler : IArchitectureContractHandler
{
    public string Family => "asmdef";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAsmdefContract((ArchitectureAsmdefContract)contract));
    }
}

internal sealed class IndependenceContractHandler : IArchitectureContractHandler
{
    public string Family => "independence";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckIndependenceContract((ArchitectureIndependenceContract)contract));
    }
}

internal sealed class AssemblyIndependenceContractHandler : IArchitectureContractHandler
{
    public string Family => "assembly_independence";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAssemblyIndependenceContract((ArchitectureAssemblyIndependenceContract)contract));
    }
}

internal sealed class AssemblyDependencyContractHandler : IArchitectureContractHandler
{
    public string Family => "assembly_dependency";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAssemblyDependencyContract((ArchitectureAssemblyDependencyContract)contract));
    }
}

internal sealed class AssemblyAllowOnlyContractHandler : IArchitectureContractHandler
{
    public string Family => "assembly_allow_only";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAssemblyAllowOnlyContract((ArchitectureAssemblyAllowOnlyContract)contract));
    }
}

internal sealed class PackageDependencyContractHandler : IArchitectureContractHandler
{
    public string Family => "package_dependency";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckPackageDependencyContract((ArchitecturePackageDependencyContract)contract));
    }
}

internal sealed class PackageAllowOnlyContractHandler : IArchitectureContractHandler
{
    public string Family => "package_allow_only";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckPackageAllowOnlyContract((ArchitecturePackageAllowOnlyContract)contract));
    }
}

internal sealed class ProtectedContractHandler : IArchitectureContractHandler
{
    public string Family => "protected";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckProtectedContract((ArchitectureProtectedContract)contract));
    }
}

internal sealed class ExternalContractHandler : IArchitectureContractHandler
{
    public string Family => "external";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckExternalContract((ArchitectureExternalDependencyContract)contract));
    }
}

internal sealed class ExternalAllowOnlyContractHandler : IArchitectureContractHandler
{
    public string Family => "external_allow_only";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckExternalAllowOnlyContract((ArchitectureExternalAllowOnlyContract)contract));
    }
}

internal sealed class TypePlacementContractHandler : IArchitectureContractHandler
{
    public string Family => "type_placement";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckTypePlacementContract((ArchitectureTypePlacementContract)contract));
    }
}

internal sealed class PublicApiSurfaceContractHandler : IArchitectureContractHandler
{
    public string Family => "public_api_surface";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckPublicApiSurfaceContract((ArchitecturePublicApiSurfaceContract)contract));
    }
}

internal sealed class AttributeUsageContractHandler : IArchitectureContractHandler
{
    public string Family => "attribute_usage";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        return ArchitectureHandlerResult.FromViolations(
            session.CheckAttributeUsageContract((ArchitectureAttributeUsageContract)contract));
    }
}

internal sealed class AcyclicSiblingContractHandler : IArchitectureContractHandler
{
    public string Family => "acyclic_sibling";

    public ArchitectureHandlerResult Execute(ArchitectureAnalysisSession session, IArchitectureContract contract)
    {
        var acyclicSiblingContract = (ArchitectureAcyclicSiblingContract)contract;
        IReadOnlyCollection<string> cycles = session.CheckAcyclicSiblingContract(acyclicSiblingContract);
        string idPrefix = acyclicSiblingContract.Id != null ? $"[{acyclicSiblingContract.Id}] " : string.Empty;
        return ArchitectureHandlerResult.FromCycles(cycles.Select(c => $"{idPrefix}{c}").ToList());
    }
}
