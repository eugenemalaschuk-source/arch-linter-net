using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

public static class ArchitectureContractExecutor
{
    private static readonly ArchitectureContractHandlerRegistry _handlerRegistry =
        ArchitectureContractHandlerRegistry.CreateDefault();

    public sealed record ExecutionResult(
        IReadOnlyCollection<ArchitectureViolation> Violations,
        IReadOnlyCollection<string> Cycles,
        IReadOnlyCollection<ArchitectureViolation> CoverageViolations);

    public static ExecutionResult Execute(
        ArchitectureContractRunner runner,
        ArchitectureContractDocument document,
        string mode,
        bool includeAsmdefContracts = true,
        ValidationTiming? timing = null)
    {
        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        runner.PrepareRuleInputCoverageDeferral(mode);

        bool isStrict = mode == "strict";

        List<ArchitectureViolation> violations = new();
        List<string> cycles = new();
        List<ArchitectureViolation> coverageViolations = new();

        int depCount = 0;
        using (timing?.MeasureContractFamily("dependency", () => depCount))
        {
            foreach (IArchitectureContract contract in runner.Catalog.ContractsFor(mode, "dependency"))
            {
                depCount++;
                violations.AddRange(_handlerRegistry.Execute("dependency", runner, contract).Violations);
            }
        }

        int layerCount = 0;
        using (timing?.MeasureContractFamily("layer", () => layerCount))
        {
            List<ArchitectureLayerContract> layerTemplateContracts = runner.Catalog
                .ContractsFor(mode, "layer_template")
                .Cast<ArchitectureLayerContract>()
                .ToList();

            // Expanded-template ID conflicts are detected (with configurable severity) by the
            // policy-consistency pass (analysis.policy_consistency), not by a hard throw here.
            IEnumerable<IArchitectureContract> layerContracts = runner.Catalog.ContractsFor(mode, "layer")
                .Concat(layerTemplateContracts);

            foreach (IArchitectureContract contract in layerContracts)
            {
                layerCount++;
                violations.AddRange(_handlerRegistry.Execute("layer", runner, contract).Violations);
            }
        }

        int allowOnlyCount = 0;
        using (timing?.MeasureContractFamily("allow_only", () => allowOnlyCount))
        {
            IEnumerable<ArchitectureAllowOnlyContract> allowOnlyContracts = isStrict
                ? runner.StrictAllowOnlyContracts()
                : runner.AuditAllowOnlyContracts();

            foreach (ArchitectureAllowOnlyContract contract in allowOnlyContracts)
            {
                allowOnlyCount++;
                violations.AddRange(runner.CheckAllowOnlyContract(contract));
            }
        }

        int cycleCount = 0;
        using (timing?.MeasureContractFamily("cycle", () => cycleCount))
        {
            foreach (IArchitectureContract contract in runner.Catalog.ContractsFor(mode, "cycle"))
            {
                cycleCount++;
                cycles.AddRange(_handlerRegistry.Execute("cycle", runner, contract).Cycles);
            }
        }

        int methodBodyCount = 0;
        using (timing?.MeasureContractFamily("method_body", () => methodBodyCount))
        {
            IEnumerable<ArchitectureMethodBodyContract> methodBodyContracts = isStrict
                ? runner.StrictMethodBodyContracts()
                : runner.AuditMethodBodyContracts();

            foreach (ArchitectureMethodBodyContract contract in methodBodyContracts)
            {
                methodBodyCount++;
                violations.AddRange(runner.CheckMethodBodyContract(contract));
            }
        }

        if (includeAsmdefContracts)
        {
            int asmdefCount = 0;
            using (timing?.MeasureContractFamily("asmdef", () => asmdefCount))
            {
                IEnumerable<ArchitectureAsmdefContract> asmdefContracts = isStrict
                    ? runner.StrictAsmdefContracts()
                    : runner.AuditAsmdefContracts();

                foreach (ArchitectureAsmdefContract contract in asmdefContracts)
                {
                    asmdefCount++;
                    violations.AddRange(runner.CheckAsmdefContract(contract));
                }
            }
        }

        int independenceCount = 0;
        using (timing?.MeasureContractFamily("independence", () => independenceCount))
        {
            IEnumerable<ArchitectureIndependenceContract> independenceContracts = isStrict
                ? runner.StrictIndependenceContracts()
                : runner.AuditIndependenceContracts();

            foreach (ArchitectureIndependenceContract contract in independenceContracts)
            {
                independenceCount++;
                violations.AddRange(runner.CheckIndependenceContract(contract));
            }
        }

        int protectedCount = 0;
        using (timing?.MeasureContractFamily("protected", () => protectedCount))
        {
            IEnumerable<ArchitectureProtectedContract> protectedContracts = isStrict
                ? runner.StrictProtectedContracts()
                : runner.AuditProtectedContracts();

            foreach (ArchitectureProtectedContract contract in protectedContracts)
            {
                protectedCount++;
                violations.AddRange(runner.CheckProtectedContract(contract));
            }
        }

        int externalCount = 0;
        using (timing?.MeasureContractFamily("external", () => externalCount))
        {
            IEnumerable<ArchitectureExternalDependencyContract> externalContracts = isStrict
                ? runner.StrictExternalContracts()
                : runner.AuditExternalContracts();

            foreach (ArchitectureExternalDependencyContract contract in externalContracts)
            {
                externalCount++;
                violations.AddRange(runner.CheckExternalContract(contract));
            }
        }

        int acyclicSiblingCount = 0;
        using (timing?.MeasureContractFamily("acyclic_sibling", () => acyclicSiblingCount))
        {
            IEnumerable<ArchitectureAcyclicSiblingContract> acyclicSiblingContracts = isStrict
                ? runner.StrictAcyclicSiblingContracts()
                : runner.AuditAcyclicSiblingContracts();

            foreach (ArchitectureAcyclicSiblingContract contract in acyclicSiblingContracts)
            {
                acyclicSiblingCount++;
                IReadOnlyCollection<string> contractCycles = runner.CheckAcyclicSiblingContract(contract);
                string idPrefix = contract.Id != null ? $"[{contract.Id}] " : string.Empty;
                cycles.AddRange(contractCycles.Select(c => $"{idPrefix}{c}"));
            }
        }

        int coverageCount = 0;
        using (timing?.MeasureContractFamily("coverage", () => coverageCount))
        {
            foreach (IArchitectureContract contract in runner.Catalog.ContractsFor(mode, "coverage"))
            {
                coverageCount++;
                coverageViolations.AddRange(_handlerRegistry.Execute("coverage", runner, contract).Violations);
            }
        }

        return new ExecutionResult(violations, cycles, coverageViolations);
    }
}
