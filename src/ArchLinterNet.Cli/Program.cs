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

    string repositoryRoot = ResolveRepositoryRoot(policyPath);

    IReadOnlyCollection<System.Reflection.Assembly> assemblies =
        ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

    ArchitectureAnalysisContext context = new(repositoryRoot, assemblies);
    ArchitectureContractRunner runner = new(context, document);

    List<ArchitectureViolation> allViolations = new();
    List<string> allCycles = new();

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
        allCycles.AddRange(cycles);
    }

    IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = mode == "audit"
        ? runner.AuditMethodBodyContracts()
        : runner.StrictMethodBodyContracts();

    foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
    {
        allViolations.AddRange(runner.CheckMethodBodyContract(contract));
    }

    IEnumerable<ArchitectureAsmdefContract> asmdefContracts = mode == "audit"
        ? runner.AuditAsmdefContracts()
        : runner.StrictAsmdefContracts();

    foreach (ArchitectureAsmdefContract contract in asmdefContracts)
    {
        allViolations.AddRange(runner.CheckAsmdefContract(contract));
    }

    IEnumerable<ArchitectureIndependenceContract> independenceContracts = mode == "audit"
        ? runner.AuditIndependenceContracts()
        : runner.StrictIndependenceContracts();

    foreach (ArchitectureIndependenceContract contract in independenceContracts)
    {
        allViolations.AddRange(runner.CheckIndependenceContract(contract));
    }

    bool passed = allViolations.Count == 0 && allCycles.Count == 0;

    if (passed)
    {
        Console.WriteLine("Architecture validation passed.");
        return 0;
    }

    if (format == "json")
    {
        if (allViolations.Count > 0)
        {
            Console.WriteLine(ArchitectureDiagnosticFormatter.FormatViolationsForCiArtifacts(
                $"arch-linter-{mode}", allViolations));
        }

        foreach (ArchitectureCycleContract contract in cycleContracts)
        {
            IReadOnlyCollection<string> contractCycles = runner.CheckCycleContract(contract);
            if (contractCycles.Count > 0)
            {
                Console.WriteLine(ArchitectureDiagnosticFormatter.FormatCyclesForCiArtifacts(
                    contract.Name, contractCycles));
            }
        }
    }
    else
    {
        if (allViolations.Count > 0)
        {
            Console.WriteLine(ArchitectureDiagnosticFormatter.FormatViolationsForHumans(allViolations));
        }

        if (allCycles.Count > 0)
        {
            Console.WriteLine(ArchitectureDiagnosticFormatter.FormatCyclesForHumans(allCycles));
        }
    }

    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Architecture validation error: {ex.Message}");
    return 2;
}

static string ResolveRepositoryRoot(string policyPath)
{
    string? policyDir = Path.GetDirectoryName(policyPath);
    if (string.IsNullOrEmpty(policyDir))
    {
        return Directory.GetCurrentDirectory();
    }

    // If policy is at <repo>/architecture/dependencies.arch.yml, go up one level
    if (string.Equals(Path.GetFileName(policyDir), "architecture", StringComparison.OrdinalIgnoreCase))
    {
        return Path.GetDirectoryName(policyDir) ?? policyDir;
    }

    return policyDir;
}
