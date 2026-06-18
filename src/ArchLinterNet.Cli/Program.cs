using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

string version = typeof(ArchitectureContractLoader).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

string policyPath = "architecture/dependencies.arch.yml";
string mode = "strict";
string format = "human";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--help" or "-h":
            PrintHelp();
            return 0;
        case "--version" or "-v":
            Console.WriteLine($"arch-linter-net {version}");
            return 0;
        case "--policy" or "-p" when i + 1 < args.Length:
            policyPath = args[++i];
            break;
        case "--mode" or "-m" when i + 1 < args.Length:
            mode = args[++i];
            break;
        case "--format" or "-f" when i + 1 < args.Length:
            format = args[++i];
            break;
        case "--strict":
            mode = "strict";
            break;
        case "--audit":
            mode = "audit";
            break;
        case "--json":
            format = "json";
            break;
        default:
            Console.Error.WriteLine($"Unknown option: {args[i]}");
            Console.Error.WriteLine("Run with --help for usage information.");
            return 2;
    }
}

if (mode is not ("strict" or "audit"))
{
    Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict' or 'audit'.");
    return 2;
}

if (format is not ("human" or "json"))
{
    Console.Error.WriteLine($"Invalid format: {format}. Use 'human' or 'json'.");
    return 2;
}

if (!File.Exists(policyPath))
{
    Console.Error.WriteLine($"Policy file not found: {policyPath}");
    return 2;
}

try
{
    ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(policyPath);

    string repositoryRoot = ResolveRepositoryRoot(policyPath);

    ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

    ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
        resolution.MissingAssemblyNames);
    ArchitectureContractRunner runner = new(context, document);

    List<ArchitectureViolation> allViolations = new();
    List<string> allCycles = new();

    allViolations.AddRange(runner.CheckConfiguration(strict: mode == "strict"));

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
        IReadOnlyCollection<string> contractCycles = runner.CheckCycleContract(contract);
        allCycles.AddRange(contractCycles);
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
        if (format == "json")
        {
            Console.WriteLine("{\"passed\":true}");
        }
        else
        {
            Console.WriteLine("Architecture validation passed.");
        }

        return 0;
    }

    if (format == "json")
    {
        Console.WriteLine(ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            mode, passed, allViolations, allCycles));
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

    if (string.Equals(Path.GetFileName(policyDir), "architecture", StringComparison.OrdinalIgnoreCase))
    {
        return Path.GetDirectoryName(policyDir) ?? policyDir;
    }

    return policyDir;
}

static void PrintHelp()
{
    Console.WriteLine("""
        arch-linter-net — architecture contract linter for .NET

        Usage:
          arch-linter-net [options]

        Options:
          -p, --policy <path>   Path to YAML contract file
                                (default: architecture/dependencies.arch.yml)
          -m, --mode <mode>     Validation mode: strict or audit (default: strict)
              --strict          Shortcut for --mode strict
              --audit           Shortcut for --mode audit
          -f, --format <fmt>    Output format: human or json (default: human)
              --json            Shortcut for --format json
          -h, --help            Show this help message
          -v, --version         Show version

        Exit codes:
          0   All contracts passed
          1   One or more contracts failed
          2   Runtime error (invalid arguments, file not found, etc.)
        """);
}
