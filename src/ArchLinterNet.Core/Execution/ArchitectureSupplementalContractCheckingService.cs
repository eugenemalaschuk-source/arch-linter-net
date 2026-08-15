using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Executes the contract families whose semantics already live in a dedicated checker. It owns
// their common run lifecycle (selection, rule-input deferral and ignored-violation collection),
// while the session remains the public facade and owner of run-scoped facts.
internal sealed class ArchitectureSupplementalContractCheckingService
{
    private readonly ArchitectureAnalysisSession _session;

    public ArchitectureSupplementalContractCheckingService(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public List<ArchitectureViolation> CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract)
    {
        if (!_session.IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return Execute(contract, contract.IgnoredViolations, executionContext =>
            AssemblyIndependenceChecker.Check(contract, _session.Context.TargetAssemblies, executionContext));
    }

    public List<ArchitectureViolation> CheckPortBoundaryContract(ArchitecturePortBoundaryContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: false, executionContext =>
            PortBoundaryChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckAttributeUsageContract(ArchitectureAttributeUsageContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            AttributeUsageChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckAssemblyDependencyContract(ArchitectureAssemblyDependencyContract contract)
    {
        if (!_session.IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);
        return Execute(contract, contract.IgnoredViolations, executionContext =>
            AssemblyDependencyChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckAssemblyAllowOnlyContract(ArchitectureAssemblyAllowOnlyContract contract)
    {
        if (!_session.IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);
        return Execute(contract, contract.IgnoredViolations, executionContext =>
            AssemblyDependencyChecker.CheckAllowOnly(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckCompositionContract(ArchitectureCompositionContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            CompositionChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckInheritanceContract(ArchitectureInheritanceContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            InheritanceChecker.Check(contract, _session.Document, _session.TypeIndex, executionContext));
    }

    public List<ArchitectureViolation> CheckInterfaceImplementationContract(ArchitectureInterfaceImplementationContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            InterfaceImplementationChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckLayoutConventionsContract(ArchitectureLayoutConventionContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        LayoutConventionChecker.Result result = LayoutConventionChecker.Check(contract, _session.CheckerContext, executionContext);
        if (result.EvaluatedIgnores)
        {
            _session.CollectUnmatchedIgnores(executionContext);
        }

        return result.Violations;
    }

    public List<ArchitectureViolation> CheckPackageDependencyContract(ArchitecturePackageDependencyContract contract)
    {
        if (!_session.IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);
        return Execute(contract, contract.IgnoredViolations, executionContext =>
            PackageDependencyChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckPackageAllowOnlyContract(ArchitecturePackageAllowOnlyContract contract)
    {
        if (!_session.IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        AssemblyDependencyDepthGuard.RequireDirect(contract.Name, contract.DependencyDepth);
        return Execute(contract, contract.IgnoredViolations, executionContext =>
            PackageDependencyChecker.CheckAllowOnly(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckProjectMetadataContract(ArchitectureProjectMetadataContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: false, executionContext =>
            ProjectMetadataChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            ProtectedChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckTypePlacementContract(ArchitectureTypePlacementContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: true, executionContext =>
            TypePlacementChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckContextDependencyContract(ArchitectureContextDependencyContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: false, executionContext =>
            ContextDependencyChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    public List<ArchitectureViolation> CheckContextAllowOnlyContract(ArchitectureContextAllowOnlyContract contract)
    {
        return ExecuteSelected(contract, contract.IgnoredViolations, deferToRuleInputCoverage: false, executionContext =>
            ContextAllowOnlyChecker.Check(contract, _session.CheckerContext, executionContext));
    }

    private List<ArchitectureViolation> ExecuteSelected<TContract>(
        TContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        bool deferToRuleInputCoverage,
        Func<ArchitectureContractExecutionContext, List<ArchitectureViolation>> checker)
        where TContract : IArchitectureContract
    {
        if (!_session.IsContractSelected(contract.Id)
            || (deferToRuleInputCoverage && _session.IsDanglingButCoveredByRuleInputCoverage(contract)))
        {
            return new List<ArchitectureViolation>();
        }

        return Execute(contract, ignoredViolations, checker);
    }

    private List<ArchitectureViolation> Execute<TContract>(
        TContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        Func<ArchitectureContractExecutionContext, List<ArchitectureViolation>> checker)
        where TContract : IArchitectureContract
    {
        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, ignoredViolations);
        List<ArchitectureViolation> violations = checker(executionContext);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }
}
