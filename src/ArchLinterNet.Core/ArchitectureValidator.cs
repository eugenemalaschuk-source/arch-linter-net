using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core;

public sealed class ArchitectureValidator
{
    public bool Validate(string policyPath)
    {
        return Validate(policyPath, out _, out _);
    }

    public bool Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        return Validate(policyPath, out violations, out _);
    }

    public bool Validate(
        string policyPath,
        out IReadOnlyCollection<ArchitectureViolation> violations,
        out IReadOnlyCollection<string> cycles)
    {
        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(policyPath);

        string repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);

        ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

        ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> allViolations = new();
        List<string> allCycles = new();

        allViolations.AddRange(runner.CheckConfiguration());

        foreach (ArchitectureDependencyContract contract in runner.StrictContracts())
        {
            allViolations.AddRange(runner.CheckContract(contract));
        }

        foreach (ArchitectureLayerContract contract in runner.StrictLayerContracts())
        {
            allViolations.AddRange(runner.CheckLayerContract(contract));
        }

        foreach (ArchitectureAllowOnlyContract contract in runner.StrictAllowOnlyContracts())
        {
            allViolations.AddRange(runner.CheckAllowOnlyContract(contract));
        }

        foreach (ArchitectureCycleContract contract in runner.StrictCycleContracts())
        {
            IReadOnlyCollection<string> contractCycles = runner.CheckCycleContract(contract);
            allCycles.AddRange(contractCycles);
        }

        foreach (ArchitectureMethodBodyContract contract in runner.StrictMethodBodyContracts())
        {
            allViolations.AddRange(runner.CheckMethodBodyContract(contract));
        }

        foreach (ArchitectureAsmdefContract contract in runner.StrictAsmdefContracts())
        {
            allViolations.AddRange(runner.CheckAsmdefContract(contract));
        }

        foreach (ArchitectureIndependenceContract contract in runner.StrictIndependenceContracts())
        {
            allViolations.AddRange(runner.CheckIndependenceContract(contract));
        }

        violations = allViolations;
        cycles = allCycles;
        return allViolations.Count == 0 && allCycles.Count == 0;
    }

}
