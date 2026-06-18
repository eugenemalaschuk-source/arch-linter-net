using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core;

public sealed class ArchitectureValidator
{
    public bool Validate(string policyPath)
    {
        return Validate(policyPath, out _);
    }

    public bool Validate(string policyPath, out IReadOnlyCollection<ArchitectureViolation> violations)
    {
        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(policyPath);

        IReadOnlyCollection<System.Reflection.Assembly> assemblies =
            ArchitectureAssemblyResolver.ResolveFromDocument(document);

        ArchitectureAnalysisContext context = new(
            Path.GetDirectoryName(policyPath) ?? Directory.GetCurrentDirectory(),
            assemblies);

        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> allViolations = new();

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
            IReadOnlyCollection<string> cycles = runner.CheckCycleContract(contract);
            if (cycles.Count > 0)
            {
                string details = ArchitectureDiagnosticFormatter.FormatCyclesForHumans(cycles);
                allViolations.Add(new ArchitectureViolation(
                    contract.Name,
                    "(cycle-detection)",
                    "cycles-detected",
                    new[] { details }));
            }
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
        return allViolations.Count == 0;
    }
}
