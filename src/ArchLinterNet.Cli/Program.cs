using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

string policyPath = "architecture/dependencies.arch.yml";
string mode = "strict";
string format = "human";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--policy" when i + 1 < args.Length:
            policyPath = args[++i];
            break;
        case "--mode" when i + 1 < args.Length:
            mode = args[++i];
            break;
        case "--format" when i + 1 < args.Length:
            format = args[++i];
            break;
    }
}

try
{
    ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(policyPath);

    IReadOnlyCollection<System.Reflection.Assembly> assemblies =
        ArchitectureAssemblyResolver.ResolveFromDocument(document);

    string repositoryRoot = Path.GetDirectoryName(policyPath) ?? Directory.GetCurrentDirectory();
    ArchitectureAnalysisContext context = new(repositoryRoot, assemblies);
    ArchitectureContractRunner runner = new(context, document);

    List<ArchitectureViolation> allViolations = new();

    IEnumerable<ArchitectureDependencyContract> dependencyContracts = mode == "audit"
        ? runner.AuditContracts()
        : runner.StrictContracts();

    foreach (ArchitectureDependencyContract contract in dependencyContracts)
    {
        allViolations.AddRange(runner.CheckContract(contract));
    }

    IEnumerable<ArchitectureLayerContract> layerContracts = mode == "audit"
        ? runner.AuditLayerContracts()
        : runner.StrictLayerContracts();

    foreach (ArchitectureLayerContract contract in layerContracts)
    {
        allViolations.AddRange(runner.CheckLayerContract(contract));
    }

    IEnumerable<ArchitectureAllowOnlyContract> allowOnlyContracts = mode == "audit"
        ? runner.AuditAllowOnlyContracts()
        : runner.StrictAllowOnlyContracts();

    foreach (ArchitectureAllowOnlyContract contract in allowOnlyContracts)
    {
        allViolations.AddRange(runner.CheckAllowOnlyContract(contract));
    }

    IEnumerable<ArchitectureCycleContract> cycleContracts = mode == "audit"
        ? runner.AuditCycleContracts()
        : runner.StrictCycleContracts();

    foreach (ArchitectureCycleContract contract in cycleContracts)
    {
        IReadOnlyCollection<string> cycles = runner.CheckCycleContract(contract);
        if (cycles.Count > 0 && format == "human")
        {
            Console.WriteLine(ArchitectureDiagnosticFormatter.FormatCyclesForHumans(cycles));
        }
        else if (cycles.Count > 0 && format == "json")
        {
            Console.WriteLine(ArchitectureDiagnosticFormatter.FormatCyclesForCiArtifacts(contract.Name, cycles));
        }
    }

    IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = mode == "audit"
        ? runner.AuditMethodBodyContracts()
        : runner.StrictMethodBodyContracts();

    foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
    {
        allViolations.AddRange(runner.CheckMethodBodyContract(contract));
    }

    IEnumerable<ArchitectureIndependenceContract> independenceContracts = mode == "audit"
        ? runner.AuditIndependenceContracts()
        : runner.StrictIndependenceContracts();

    foreach (ArchitectureIndependenceContract contract in independenceContracts)
    {
        allViolations.AddRange(runner.CheckIndependenceContract(contract));
    }

    if (allViolations.Count == 0)
    {
        Console.WriteLine("Architecture validation passed.");
        return 0;
    }

    if (format == "json")
    {
        Console.WriteLine(ArchitectureDiagnosticFormatter.FormatViolationsForCiArtifacts(
            $"arch-linter-{mode}", allViolations));
    }
    else
    {
        Console.WriteLine(ArchitectureDiagnosticFormatter.FormatViolationsForHumans(allViolations));
    }

    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Architecture validation error: {ex.Message}");
    return 2;
}
