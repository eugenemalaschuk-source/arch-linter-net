using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

using static ArchLinterNet.Core.Execution.LayerTemplateExpander;

namespace ArchLinterNet.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        string version = typeof(ArchitectureContractLoader).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        string policyPath = "architecture/dependencies.arch.yml";
        string mode = "strict";
        string format = "human";
        List<string> contractIds = new();

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
                case "--contract" when i + 1 < args.Length:
                    contractIds.Add(args[++i]);
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

            string unmatchedConfig = document.Analysis.UnmatchedIgnoredViolations;

            if (unmatchedConfig is not ("error" or "warn" or "off"))
            {
                Console.Error.WriteLine(
                    $"Invalid analysis.unmatched_ignored_violations: {unmatchedConfig}. Use 'error', 'warn', or 'off'.");
                return 2;
            }

            string repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);

            HashSet<string>? selectedIds = contractIds.Count > 0 ? new HashSet<string>(contractIds, StringComparer.OrdinalIgnoreCase) : null;

            if (selectedIds != null)
            {
                HashSet<string> availableIds = CollectAvailableContractIds(document, mode);
                List<string> unknownIds = selectedIds.Where(id => !availableIds.Contains(id)).ToList();

                if (unknownIds.Count > 0)
                {
                    Console.Error.WriteLine($"Unknown contract IDs: {string.Join(", ", unknownIds)}");
                    Console.Error.WriteLine($"Available IDs in {mode} mode: {string.Join(", ", availableIds.OrderBy(id => id))}");
                    return 2;
                }
            }

            ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

            ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
                resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);
            ArchitectureContractRunner runner = new(context, document, selectedIds, unmatchedConfig != "off");

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

            List<ArchitectureLayerContract> expandedLayerContracts = Expand(
                mode == "audit"
                    ? document.Contracts.AuditLayerTemplates
                    : document.Contracts.StrictLayerTemplates,
                mode == "audit"
                    ? document.Contracts.AuditLayers
                    : document.Contracts.StrictLayers);

            IEnumerable<ArchitectureLayerContract> layerContracts = (mode == "audit"
                    ? runner.AuditLayerContracts()
                    : runner.StrictLayerContracts())
                .Concat(expandedLayerContracts);

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
                string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
                allCycles.AddRange(contractCycles.Select(c => $"{idPrefix}{c}"));
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

            IEnumerable<ArchitectureProtectedContract> protectedContracts = mode == "audit"
                ? runner.AuditProtectedContracts()
                : runner.StrictProtectedContracts();

            foreach (ArchitectureProtectedContract contract in protectedContracts)
            {
                allViolations.AddRange(runner.CheckProtectedContract(contract));
            }

            IEnumerable<ArchitectureExternalDependencyContract> externalContracts = mode == "audit"
                ? runner.AuditExternalContracts()
                : runner.StrictExternalContracts();

            foreach (ArchitectureExternalDependencyContract contract in externalContracts)
            {
                allViolations.AddRange(runner.CheckExternalContract(contract));
            }

            IEnumerable<ArchitectureAcyclicSiblingContract> acyclicSiblingContracts = mode == "audit"
                ? runner.AuditAcyclicSiblingContracts()
                : runner.StrictAcyclicSiblingContracts();

            foreach (ArchitectureAcyclicSiblingContract contract in acyclicSiblingContracts)
            {
                IReadOnlyCollection<string> contractCycles = runner.CheckAcyclicSiblingContract(contract);
                string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
                allCycles.AddRange(contractCycles.Select(c => $"{idPrefix}{c}"));
            }

            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> allUnmatched = unmatchedConfig != "off"
                ? runner.UnmatchedIgnoredViolations
                : Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
            bool hasBlockingUnmatched = unmatchedConfig == "error" && allUnmatched.Count > 0;

            bool passed = allViolations.Count == 0 && allCycles.Count == 0 && !hasBlockingUnmatched;

            if (format == "json")
            {
                Console.WriteLine(ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
                    mode, passed, allViolations, allCycles, allUnmatched));
            }
            else
            {
                if (passed)
                {
                    Console.WriteLine("Architecture validation passed.");
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

                if (allUnmatched.Count > 0 && unmatchedConfig != "off")
                {
                    string unmatchedSection = ArchitectureDiagnosticFormatter.FormatUnmatchedForHumans(allUnmatched);
                    if (!string.IsNullOrEmpty(unmatchedSection))
                    {
                        Console.WriteLine();
                        Console.WriteLine(unmatchedSection);
                    }
                }
            }

            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Architecture validation error: {ex.Message}");
            return 2;
        }
    }

    private static HashSet<string> CollectAvailableContractIds(ArchitectureContractDocument document, string mode)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        IEnumerable<IArchitectureContract> contracts = mode == "strict"
            ? document.Contracts.AllStrict
            : document.Contracts.AllAudit;

        foreach (var c in contracts)
        {
            if (c.Id != null)
            {
                ids.Add(c.Id);
            }
        }

        List<ArchitectureLayerTemplateContract> templates = mode == "strict"
            ? document.Contracts.StrictLayerTemplates
            : document.Contracts.AuditLayerTemplates;

        foreach (ArchitectureLayerContract expanded in Expand(templates))
        {
            if (expanded.Id != null)
            {
                ids.Add(expanded.Id);
            }
        }

        return ids;
    }

    private static void PrintHelp()
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
                  --contract <id>   Run only the contract with the given ID (may be repeated)
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
}
