using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Session-side entry points for the contract families whose checking used to live here directly
// (issue #452). What remains is lifecycle only — contract selection, rule-input-coverage deferral,
// execution-context creation, unmatched-ignore collection and baseline-candidate publication.
// Family behavior itself lives in ArchLinterNet.Core.Execution.Checkers and reaches session facts
// through ArchitectureCheckerContext, so a new contract family is added by writing a checker plus a
// registry descriptor, never by growing this file.
internal sealed class ArchitectureCoreContractCheckingService
{
    private readonly ArchitectureAnalysisSession _session;

    public ArchitectureCoreContractCheckingService(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = DependencyChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        LayerChecker.Result result = LayerChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);

        // Exhaustive-sibling findings are appended after unmatched-ignore collection, preserving the
        // ordering this family had before the checker extraction.
        List<ArchitectureViolation> violations = result.Violations;
        violations.AddRange(result.ExhaustiveSiblingViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = AllowOnlyChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return Array.Empty<string>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        CycleChecker.Result result = CycleChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        _session.AddCycleBaselineCandidates(result.Graph, result.CandidateEvidence);
        return result.Cycles;
    }

    public IReadOnlyCollection<string> CheckAcyclicSiblingContract(ArchitectureAcyclicSiblingContract contract)
    {
        if (!_session.IsContractSelected(contract.Id))
        {
            return Array.Empty<string>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<string> cycles = AcyclicSiblingChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return cycles;
    }

    public List<ArchitectureViolation> CheckModuleContainerContract(ArchitectureModuleContainerContract contract)
    {
        if (!_session.IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = ModuleContainerChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public List<ArchitectureViolation> CheckMethodBodyContract(ArchitectureMethodBodyContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = MethodBodyChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract)
    {
        if (!_session.IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return AsmdefChecker.Check(contract, _session.CheckerContext);
    }

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = LayerIndependenceChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public List<ArchitectureViolation> CheckExternalContract(ArchitectureExternalDependencyContract contract)
    {
        if (!_session.IsContractSelected(contract) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = ExternalDependencyChecker.Check(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public List<ArchitectureViolation> CheckExternalAllowOnlyContract(ArchitectureExternalAllowOnlyContract contract)
    {
        if (!_session.IsContractSelected(contract) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations =
            ExternalDependencyChecker.CheckAllowOnly(contract, _session.CheckerContext, executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    public ArchitectureHandlerResult CheckContractSurfaceExposureContract(
        ArchitectureContractSurfaceExposureContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return ArchitectureHandlerResult.FromViolations(Array.Empty<ArchitectureViolation>());
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        ContractSurfaceExposureEvaluationResult result = ContractSurfaceExposureChecker.Evaluate(
            _session.CheckerContext,
            contract,
            executionContext);
        _session.CollectUnmatchedIgnores(executionContext);

        return ArchitectureHandlerResult.FromViolations(result.Violations) with
        {
            ApplicabilityExpectedEntries = new[] { result.ApplicabilityExpectedEntry },
            ApplicabilityRecords = new[] { result.ApplicabilityRecord },
        };
    }

    public ArchitectureHandlerResult CheckVersionedContractSurfaceIsolationContract(
        ArchitectureVersionedContractSurfaceIsolationContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return ArchitectureHandlerResult.FromViolations(Array.Empty<ArchitectureViolation>());
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        ContractSurfaceExposureEvaluationResult result = VersionedContractSurfaceIsolationChecker.Evaluate(
            _session.CheckerContext,
            contract,
            executionContext);
        _session.CollectUnmatchedIgnores(executionContext);

        return ArchitectureHandlerResult.FromViolations(result.Violations) with
        {
            ApplicabilityExpectedEntries = new[] { result.ApplicabilityExpectedEntry },
            ApplicabilityRecords = new[] { result.ApplicabilityRecord },
        };
    }
}
