using System.Diagnostics;
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
        if (args.Length > 0 && args[0] == "baseline")
        {
            return RunBaselineCommand(args[1..]);
        }

        return RunValidateCommand(args);
    }

    private static int RunBaselineCommand(string[] args)
    {
        int argIndex = 0;
        if (argIndex < args.Length && args[argIndex] == "generate")
        {
            argIndex++;
        }

        string policyPath = "architecture/dependencies.arch.yml";
        string? outputPath = null;
        string reason = "generated baseline";
        string mode = "all";
        string? conditionSetName = null;

        for (int i = argIndex; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    PrintBaselineHelp();
                    return 0;
                case "--config" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--reason" when i + 1 < args.Length:
                    reason = args[++i];
                    break;
                case "--mode" or "-m" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    Console.Error.WriteLine("Run 'arch-linter-net baseline --help' for usage information.");
                    return 2;
            }
        }

        if (mode is not ("strict" or "audit" or "all"))
        {
            Console.Error.WriteLine($"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
            return 2;
        }

        if (outputPath == null)
        {
            Console.Error.WriteLine("--output is required for baseline generate.");
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

            if (!ConditionSetResolver.TryResolve(
                    document, conditionSetName, out IReadOnlyList<string> preprocessorSymbols, out string? resolveError))
            {
                Console.Error.WriteLine(resolveError);
                return 2;
            }

            string repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);
            ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
            var context = new ArchitectureAnalysisContext(repositoryRoot, resolution.ResolvedAssemblies,
                resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);

            var runner = new ArchitectureContractRunner(context, document,
                enableUnmatchedIgnoreTracking: true,
                preprocessorSymbols: preprocessorSymbols);

            List<ArchitectureViolation> configViolations = runner.CheckConfiguration(strict: true);
            if (configViolations.Count > 0)
            {
                Console.Error.WriteLine("Configuration violations detected — baseline cannot be generated:");
                foreach (ArchitectureViolation v in configViolations)
                {
                    Console.Error.WriteLine($"  {v.SourceType}: {v.ForbiddenNamespace}");
                }
                return 2;
            }

            bool includeStrict = mode is "strict" or "all";
            bool includeAudit = mode is "audit" or "all";

            if (includeStrict)
            {
                foreach (ArchitectureDependencyContract contract in runner.StrictContracts())
                    runner.CheckContract(contract);

                foreach (ArchitectureLayerContract contract in runner.StrictLayerContracts())
                    runner.CheckLayerContract(contract);

                foreach (ArchitectureAllowOnlyContract contract in runner.StrictAllowOnlyContracts())
                    runner.CheckAllowOnlyContract(contract);

                foreach (ArchitectureCycleContract contract in runner.StrictCycleContracts())
                    runner.CheckCycleContract(contract);

                foreach (ArchitectureMethodBodyContract contract in runner.StrictMethodBodyContracts())
                    runner.CheckMethodBodyContract(contract);

                foreach (ArchitectureIndependenceContract contract in runner.StrictIndependenceContracts())
                    runner.CheckIndependenceContract(contract);

                foreach (ArchitectureProtectedContract contract in runner.StrictProtectedContracts())
                    runner.CheckProtectedContract(contract);

                foreach (ArchitectureExternalDependencyContract contract in runner.StrictExternalContracts())
                    runner.CheckExternalContract(contract);

                foreach (ArchitectureAcyclicSiblingContract contract in runner.StrictAcyclicSiblingContracts())
                    runner.CheckAcyclicSiblingContract(contract);

                List<ArchitectureLayerContract> strictExpanded = Expand(
                    document.Contracts.StrictLayerTemplates,
                    document.Contracts.StrictLayers);

                foreach (ArchitectureLayerContract contract in strictExpanded)
                    runner.CheckLayerContract(contract);
            }

            if (includeAudit)
            {
                foreach (ArchitectureDependencyContract contract in runner.AuditContracts())
                    runner.CheckContract(contract);

                foreach (ArchitectureLayerContract contract in runner.AuditLayerContracts())
                    runner.CheckLayerContract(contract);

                foreach (ArchitectureAllowOnlyContract contract in runner.AuditAllowOnlyContracts())
                    runner.CheckAllowOnlyContract(contract);

                foreach (ArchitectureCycleContract contract in runner.AuditCycleContracts())
                    runner.CheckCycleContract(contract);

                foreach (ArchitectureMethodBodyContract contract in runner.AuditMethodBodyContracts())
                    runner.CheckMethodBodyContract(contract);

                foreach (ArchitectureIndependenceContract contract in runner.AuditIndependenceContracts())
                    runner.CheckIndependenceContract(contract);

                foreach (ArchitectureProtectedContract contract in runner.AuditProtectedContracts())
                    runner.CheckProtectedContract(contract);

                foreach (ArchitectureExternalDependencyContract contract in runner.AuditExternalContracts())
                    runner.CheckExternalContract(contract);

                foreach (ArchitectureAcyclicSiblingContract contract in runner.AuditAcyclicSiblingContracts())
                    runner.CheckAcyclicSiblingContract(contract);

                List<ArchitectureLayerContract> auditExpanded = Expand(
                    document.Contracts.AuditLayerTemplates,
                    document.Contracts.AuditLayers);

                foreach (ArchitectureLayerContract contract in auditExpanded)
                    runner.CheckLayerContract(contract);
            }

            ArchitectureBaselineDocument baseline = ArchitectureBaselineGenerator.Generate(
                document, runner.BaselineCandidates, reason);

            string yaml = ArchitectureBaselineGenerator.Serialize(baseline);
            File.WriteAllText(outputPath, yaml);

            int candidateCount = runner.BaselineCandidates.Count;
            Console.WriteLine($"Generated baseline with {candidateCount} violation entries.");
            Console.WriteLine($"Output: {outputPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Baseline generation error: {ex.Message}");
            return 2;
        }
    }

    private static int RunValidateCommand(string[] args)
    {
        string version = typeof(ArchitectureContractLoader).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        string policyPath = "architecture/dependencies.arch.yml";
        string mode = "strict";
        string format = "human";
        List<string> contractIds = new();
        string? conditionSetName = null;
        bool timingsEnabled = false;
        string? baselinePath = null;

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
                case "--condition-set" when i + 1 < args.Length:
                    conditionSetName = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
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
                case "--timings":
                    timingsEnabled = true;
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
            ValidationTiming? timing = timingsEnabled ? new ValidationTiming() : null;

            bool passed;
            List<ArchitectureViolation> allViolations = new();
            List<string> allCycles = new();
            IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> allUnmatched =
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
            string unmatchedConfig = "error";

            using (timing?.Measure("total"))
            {
                ArchitectureContractDocument document = null!;
                string repositoryRoot = null!;
                ResolutionResult resolution = null!;
                ArchitectureContractRunner runner = null!;

                using (timing?.Measure("load_and_setup"))
                {
                    using (timing?.Measure("yaml_loading", indent: 1))
                        document = ArchitectureContractLoader.LoadFromPath(policyPath);

                    if (baselinePath != null)
                    {
                        using (timing?.Measure("baseline_loading", indent: 1))
                        {
                            ArchitectureBaselineDocument baseline = ArchitectureBaselineLoader.LoadFromPath(baselinePath);
                            ArchitectureBaselineMerger.MergeAndValidate(document, baseline);
                        }
                    }

                    unmatchedConfig = document.Analysis.UnmatchedIgnoredViolations;

                    if (unmatchedConfig is not ("error" or "warn" or "off"))
                    {
                        Console.Error.WriteLine(
                            $"Invalid analysis.unmatched_ignored_violations: {unmatchedConfig}. Use 'error', 'warn', or 'off'.");
                        return 2;
                    }

                    using (timing?.Measure("root_resolution", indent: 1))
                        repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(policyPath);

                    HashSet<string>? selectedIds = contractIds.Count > 0
                        ? new HashSet<string>(contractIds, StringComparer.OrdinalIgnoreCase)
                        : null;

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

                    IReadOnlyList<string> preprocessorSymbols = null!;

                    using (timing?.Measure("condition_set_resolution", indent: 1))
                    {
                        if (!ConditionSetResolver.TryResolve(
                                document, conditionSetName, out preprocessorSymbols, out string? resolveError))
                        {
                            Console.Error.WriteLine(resolveError);
                            return 2;
                        }
                    }

                    using (timing?.Measure("assembly_resolution", indent: 1))
                    {
                        resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);
                        var context = new ArchitectureAnalysisContext(repositoryRoot, resolution.ResolvedAssemblies,
                            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);
                        runner = new ArchitectureContractRunner(context, document, selectedIds, unmatchedConfig != "off",
                            preprocessorSymbols: preprocessorSymbols);
                    }
                }

                using (timing?.Measure("configuration_check"))
                    allViolations.AddRange(runner.CheckConfiguration(strict: mode == "strict"));

                using (timing?.Measure("contract_checks"))
                {
                    int depCount = 0;
                    using (timing?.MeasureContractFamily("dependency", () => depCount))
                    {
                        IEnumerable<ArchitectureDependencyContract> dependencyContracts = mode == "audit"
                            ? runner.AuditContracts()
                            : runner.StrictContracts();

                        foreach (ArchitectureDependencyContract contract in dependencyContracts)
                        {
                            depCount++;
                            allViolations.AddRange(runner.CheckContract(contract));
                        }
                    }

                    int layerCount = 0;
                    using (timing?.MeasureContractFamily("layer", () => layerCount))
                    {
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
                            layerCount++;
                            allViolations.AddRange(runner.CheckLayerContract(contract));
                        }
                    }

                    int allowOnlyCount = 0;
                    using (timing?.MeasureContractFamily("allow_only", () => allowOnlyCount))
                    {
                        IEnumerable<ArchitectureAllowOnlyContract> allowOnlyContracts = mode == "audit"
                            ? runner.AuditAllowOnlyContracts()
                            : runner.StrictAllowOnlyContracts();

                        foreach (ArchitectureAllowOnlyContract contract in allowOnlyContracts)
                        {
                            allowOnlyCount++;
                            allViolations.AddRange(runner.CheckAllowOnlyContract(contract));
                        }
                    }

                    int cycleCount = 0;
                    using (timing?.MeasureContractFamily("cycle", () => cycleCount))
                    {
                        IEnumerable<ArchitectureCycleContract> cycleContracts = mode == "audit"
                            ? runner.AuditCycleContracts()
                            : runner.StrictCycleContracts();

                        foreach (ArchitectureCycleContract contract in cycleContracts)
                        {
                            cycleCount++;
                            IReadOnlyCollection<string> contractCycles = runner.CheckCycleContract(contract);
                            string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
                            allCycles.AddRange(contractCycles.Select(c => $"{idPrefix}{c}"));
                        }
                    }

                    int methodBodyCount = 0;
                    using (timing?.MeasureContractFamily("method_body", () => methodBodyCount))
                    {
                        IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = mode == "audit"
                            ? runner.AuditMethodBodyContracts()
                            : runner.StrictMethodBodyContracts();

                        foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
                        {
                            methodBodyCount++;
                            allViolations.AddRange(runner.CheckMethodBodyContract(contract));
                        }
                    }

                    int asmdefCount = 0;
                    using (timing?.MeasureContractFamily("asmdef", () => asmdefCount))
                    {
                        IEnumerable<ArchitectureAsmdefContract> asmdefContracts = mode == "audit"
                            ? runner.AuditAsmdefContracts()
                            : runner.StrictAsmdefContracts();

                        foreach (ArchitectureAsmdefContract contract in asmdefContracts)
                        {
                            asmdefCount++;
                            allViolations.AddRange(runner.CheckAsmdefContract(contract));
                        }
                    }

                    int independenceCount = 0;
                    using (timing?.MeasureContractFamily("independence", () => independenceCount))
                    {
                        IEnumerable<ArchitectureIndependenceContract> independenceContracts = mode == "audit"
                            ? runner.AuditIndependenceContracts()
                            : runner.StrictIndependenceContracts();

                        foreach (ArchitectureIndependenceContract contract in independenceContracts)
                        {
                            independenceCount++;
                            allViolations.AddRange(runner.CheckIndependenceContract(contract));
                        }
                    }

                    int protectedCount = 0;
                    using (timing?.MeasureContractFamily("protected", () => protectedCount))
                    {
                        IEnumerable<ArchitectureProtectedContract> protectedContracts = mode == "audit"
                            ? runner.AuditProtectedContracts()
                            : runner.StrictProtectedContracts();

                        foreach (ArchitectureProtectedContract contract in protectedContracts)
                        {
                            protectedCount++;
                            allViolations.AddRange(runner.CheckProtectedContract(contract));
                        }
                    }

                    int externalCount = 0;
                    using (timing?.MeasureContractFamily("external", () => externalCount))
                    {
                        IEnumerable<ArchitectureExternalDependencyContract> externalContracts = mode == "audit"
                            ? runner.AuditExternalContracts()
                            : runner.StrictExternalContracts();

                        foreach (ArchitectureExternalDependencyContract contract in externalContracts)
                        {
                            externalCount++;
                            allViolations.AddRange(runner.CheckExternalContract(contract));
                        }
                    }

                    int acyclicSiblingCount = 0;
                    using (timing?.MeasureContractFamily("acyclic_sibling", () => acyclicSiblingCount))
                    {
                        IEnumerable<ArchitectureAcyclicSiblingContract> acyclicSiblingContracts = mode == "audit"
                            ? runner.AuditAcyclicSiblingContracts()
                            : runner.StrictAcyclicSiblingContracts();

                        foreach (ArchitectureAcyclicSiblingContract contract in acyclicSiblingContracts)
                        {
                            acyclicSiblingCount++;
                            IReadOnlyCollection<string> contractCycles = runner.CheckAcyclicSiblingContract(contract);
                            string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
                            allCycles.AddRange(contractCycles.Select(c => $"{idPrefix}{c}"));
                        }
                    }
                }

                using (timing?.Measure("post_processing"))
                {
                    allUnmatched = unmatchedConfig != "off"
                        ? runner.UnmatchedIgnoredViolations
                        : Array.Empty<ArchitectureUnmatchedIgnoredViolation>();
                }

                bool hasBlockingUnmatched = unmatchedConfig == "error" && allUnmatched.Count > 0;
                passed = allViolations.Count == 0 && allCycles.Count == 0 && !hasBlockingUnmatched;
            }

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

            timing?.WriteReport(Console.Error);

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
              arch-linter-net baseline generate --config <path> --output <path> [options]

            Validate Options:
              -p, --policy <path>   Path to YAML contract file
                                    (default: architecture/dependencies.arch.yml)
              -m, --mode <mode>     Validation mode: strict or audit (default: strict)
                  --strict          Shortcut for --mode strict
                  --audit           Shortcut for --mode audit
                  --contract <id>   Run only the contract with the given ID (may be repeated)
                  --condition-set <name>
                                    Use a named condition set from analysis.condition_sets
                                    to control conditional compilation symbols during
                                    Roslyn source analysis (default: policy default_condition_set,
                                    otherwise empty symbol set)
                  --baseline <path> Path to baseline file to merge with policy ignores
                  --timings         Print phase-level timing report to stderr
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

    private static void PrintBaselineHelp()
    {
        Console.WriteLine("""
            arch-linter-net baseline generate — generate a baseline of current violations

            Usage:
              arch-linter-net baseline generate --config <path> --output <path> [options]

            Options:
              --config <path>     Path to YAML contract file
                                  (default: architecture/dependencies.arch.yml)
              --output <path>     Path to write the generated baseline file (required)
              --mode <mode>       Contract mode: strict, audit, or all (default: all)
              --reason <text>     Reason text for baseline entries
                                  (default: "generated baseline")
              --condition-set <name>
                                  Use a named condition set from analysis.condition_sets
                                  to control conditional compilation symbols during
                                  Roslyn source analysis (default: policy
                                  default_condition_set, otherwise empty symbol set)
              -h, --help          Show this help message

            Exit codes:
              0   Baseline generated successfully
              2   Runtime error (invalid arguments, file not found, config violations, etc.)
            """);
    }
}
