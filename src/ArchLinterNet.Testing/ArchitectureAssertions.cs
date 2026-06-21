using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

using static ArchLinterNet.Core.Execution.LayerTemplateExpander;

namespace ArchLinterNet.Testing;

public static class ArchitectureAssertions
{
    public static ArchitectureValidationBuilder FromPolicy(string policyPath)
    {
        return new ArchitectureValidationBuilder(policyPath);
    }

    public static ArchitectureValidationBuilder FromRepositoryRoot(string repositoryRoot)
    {
        string policyPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");
        return new ArchitectureValidationBuilder(policyPath);
    }
}

public sealed class ArchitectureValidationBuilder
{
    private readonly string _policyPath;
    private string? _conditionSetName;

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
    }

    public ArchitectureValidationBuilder WithConditionSet(string name)
    {
        _conditionSetName = name;
        return this;
    }

    public ArchitectureValidationResult ValidateStrict()
    {
        return Validate(contracts: "strict");
    }

    public ArchitectureValidationResult ValidateAudit()
    {
        return Validate(contracts: "audit");
    }

    private ArchitectureValidationResult Validate(string contracts)
    {
        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(_policyPath);

        string repositoryRoot = ArchitectureRepositoryRootLocator.ResolveFrom(_policyPath);

        string selectedConditionSet = _conditionSetName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedConditionSet))
        {
            selectedConditionSet = document.Analysis.DefaultConditionSet;
        }

        IReadOnlyList<string> preprocessorSymbols = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(selectedConditionSet))
        {
            if (!document.Analysis.ConditionSets.TryGetValue(
                    selectedConditionSet, out List<string>? symbols))
            {
                string available = document.Analysis.ConditionSets.Keys.Count > 0
                    ? $" Available condition sets: {string.Join(", ", document.Analysis.ConditionSets.Keys.OrderBy(x => x))}."
                    : string.Empty;
                throw new InvalidOperationException(
                    $"Unknown condition set: '{selectedConditionSet}'.{available}");
            }

            preprocessorSymbols = symbols;
        }

        ResolutionResult resolution = ArchitectureAssemblyResolver.ResolveFromDocument(document, repositoryRoot);

        ArchitectureAnalysisContext context = new(repositoryRoot, resolution.ResolvedAssemblies,
            resolution.MissingAssemblyNames, resolution.AssemblyProbingPaths);
        ArchitectureContractRunner runner = new(context, document,
            preprocessorSymbols: preprocessorSymbols);

        List<ArchitectureViolation> allViolations = new();
        List<string> allCycles = new();

        bool isStrict = contracts == "strict";

        allViolations.AddRange(runner.CheckConfiguration(strict: isStrict));

        IEnumerable<ArchitectureDependencyContract> dependencyContracts = isStrict
            ? runner.StrictContracts()
            : runner.AuditContracts();

        foreach (ArchitectureDependencyContract contract in dependencyContracts)
        {
            allViolations.AddRange(runner.CheckContract(contract));
        }

        List<ArchitectureLayerContract> expandedLayerContracts = Expand(
            isStrict
                ? document.Contracts.StrictLayerTemplates
                : document.Contracts.AuditLayerTemplates,
            isStrict
                ? document.Contracts.StrictLayers
                : document.Contracts.AuditLayers);

        IEnumerable<ArchitectureLayerContract> layerContracts = (isStrict
                ? runner.StrictLayerContracts()
                : runner.AuditLayerContracts())
            .Concat(expandedLayerContracts);

        foreach (ArchitectureLayerContract contract in layerContracts)
        {
            allViolations.AddRange(runner.CheckLayerContract(contract));
        }

        IEnumerable<ArchitectureAllowOnlyContract> allowOnlyContracts = isStrict
            ? runner.StrictAllowOnlyContracts()
            : runner.AuditAllowOnlyContracts();

        foreach (ArchitectureAllowOnlyContract contract in allowOnlyContracts)
        {
            allViolations.AddRange(runner.CheckAllowOnlyContract(contract));
        }

        IEnumerable<ArchitectureCycleContract> cycleContracts = isStrict
            ? runner.StrictCycleContracts()
            : runner.AuditCycleContracts();

        foreach (ArchitectureCycleContract contract in cycleContracts)
        {
            IReadOnlyCollection<string> cycles = runner.CheckCycleContract(contract);
            string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
            allCycles.AddRange(cycles.Select(c => $"{idPrefix}{c}"));
        }

        IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = isStrict
            ? runner.StrictMethodBodyContracts()
            : runner.AuditMethodBodyContracts();

        foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
        {
            allViolations.AddRange(runner.CheckMethodBodyContract(contract));
        }

        IEnumerable<ArchitectureAsmdefContract> asmdefContracts = isStrict
            ? runner.StrictAsmdefContracts()
            : runner.AuditAsmdefContracts();

        foreach (ArchitectureAsmdefContract contract in asmdefContracts)
        {
            allViolations.AddRange(runner.CheckAsmdefContract(contract));
        }

        IEnumerable<ArchitectureIndependenceContract> independenceContracts = isStrict
            ? runner.StrictIndependenceContracts()
            : runner.AuditIndependenceContracts();

        foreach (ArchitectureIndependenceContract contract in independenceContracts)
        {
            allViolations.AddRange(runner.CheckIndependenceContract(contract));
        }

        IEnumerable<ArchitectureProtectedContract> protectedContracts = isStrict
            ? runner.StrictProtectedContracts()
            : runner.AuditProtectedContracts();

        foreach (ArchitectureProtectedContract contract in protectedContracts)
        {
            allViolations.AddRange(runner.CheckProtectedContract(contract));
        }

        IEnumerable<ArchitectureExternalDependencyContract> externalContracts = isStrict
            ? runner.StrictExternalContracts()
            : runner.AuditExternalContracts();

        foreach (ArchitectureExternalDependencyContract contract in externalContracts)
        {
            allViolations.AddRange(runner.CheckExternalContract(contract));
        }

        IEnumerable<ArchitectureAcyclicSiblingContract> acyclicSiblingContracts = isStrict
            ? runner.StrictAcyclicSiblingContracts()
            : runner.AuditAcyclicSiblingContracts();

        foreach (ArchitectureAcyclicSiblingContract contract in acyclicSiblingContracts)
        {
            IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);
            string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
            allCycles.AddRange(cycles.Select(c => $"{idPrefix}{c}"));
        }

        return new ArchitectureValidationResult(
            allViolations.Count == 0 && allCycles.Count == 0,
            allViolations,
            allCycles);
    }

}

public sealed class ArchitectureValidationResult
{
    public bool Passed { get; }
    public IReadOnlyCollection<ArchitectureViolation> Violations { get; }
    public IReadOnlyCollection<string> Cycles { get; }

    public ArchitectureValidationResult(
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles)
    {
        Passed = passed;
        Violations = violations;
        Cycles = cycles;
    }

    public void ShouldPass()
    {
        if (!Passed)
        {
            string violationDetails = Violations.Count > 0
                ? ArchitectureDiagnosticFormatter.FormatViolationsForHumans(Violations)
                : string.Empty;

            string cycleDetails = Cycles.Count > 0
                ? ArchitectureDiagnosticFormatter.FormatCyclesForHumans(Cycles)
                : string.Empty;

            string message = $"Architecture validation failed.{Environment.NewLine}";
            if (!string.IsNullOrEmpty(violationDetails))
            {
                message += $"Violations:{Environment.NewLine}{violationDetails}{Environment.NewLine}";
            }

            if (!string.IsNullOrEmpty(cycleDetails))
            {
                message += $"Cycles:{Environment.NewLine}{cycleDetails}{Environment.NewLine}";
            }

            throw new InvalidOperationException(message);
        }
    }
}
