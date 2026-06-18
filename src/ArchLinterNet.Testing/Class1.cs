using ArchLinterNet.Core;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;

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

    public ArchitectureValidationBuilder(string policyPath)
    {
        _policyPath = policyPath;
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

        IReadOnlyCollection<System.Reflection.Assembly> assemblies =
            ArchitectureAssemblyResolver.ResolveFromDocument(document);

        string repositoryRoot = Path.GetDirectoryName(_policyPath) ?? Directory.GetCurrentDirectory();
        ArchitectureAnalysisContext context = new(repositoryRoot, assemblies);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> allViolations = new();
        List<string> allCycles = new();

        bool isStrict = contracts == "strict";

        IEnumerable<ArchitectureDependencyContract> dependencyContracts = isStrict
            ? runner.StrictContracts()
            : runner.AuditContracts();

        foreach (ArchitectureDependencyContract contract in dependencyContracts)
        {
            allViolations.AddRange(runner.CheckContract(contract));
        }

        IEnumerable<ArchitectureLayerContract> layerContracts = isStrict
            ? runner.StrictLayerContracts()
            : runner.AuditLayerContracts();

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
            allCycles.AddRange(cycles);
        }

        IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = isStrict
            ? runner.StrictMethodBodyContracts()
            : runner.AuditMethodBodyContracts();

        foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
        {
            allViolations.AddRange(runner.CheckMethodBodyContract(contract));
        }

        IEnumerable<ArchitectureIndependenceContract> independenceContracts = isStrict
            ? runner.StrictIndependenceContracts()
            : runner.AuditIndependenceContracts();

        foreach (ArchitectureIndependenceContract contract in independenceContracts)
        {
            allViolations.AddRange(runner.CheckIndependenceContract(contract));
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
