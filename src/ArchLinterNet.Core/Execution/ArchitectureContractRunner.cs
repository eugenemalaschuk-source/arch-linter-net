using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Execution;

// Thin facade over ArchitectureAnalysisSession, kept for public API stability: every member here
// is a one-line delegation. All per-run mutable state and contract-family algorithms live on the
// session; handlers receive the session directly rather than this runner.
public sealed class ArchitectureContractRunner(
    ArchitectureAnalysisContext context,
    ArchitectureContractDocument document,
    HashSet<string>? selectedContractIds = null,
    bool enableUnmatchedIgnoreTracking = true,
    IReadOnlyList<string>? preprocessorSymbols = null)
{
    private readonly ArchitectureAnalysisSession _session =
        new(context, document, selectedContractIds, enableUnmatchedIgnoreTracking, preprocessorSymbols);

    internal ArchitectureAnalysisSession Session => _session;

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _session.UnmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _session.BaselineCandidates;

    public ArchitectureContractCatalog Catalog => _session.Catalog;

    internal void PrepareRuleInputCoverageDeferral(string mode) => _session.PrepareRuleInputCoverageDeferral(mode);

    public IEnumerable<ArchitectureDependencyContract> StrictContracts() => _session.StrictContracts();

    public IEnumerable<ArchitectureDependencyContract> AuditContracts() => _session.AuditContracts();

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts() => _session.StrictLayerContracts();

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts() => _session.AuditLayerContracts();

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts() => _session.StrictAllowOnlyContracts();

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts() => _session.AuditAllowOnlyContracts();

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts() => _session.StrictCycleContracts();

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts() => _session.AuditCycleContracts();

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts() => _session.StrictMethodBodyContracts();

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts() => _session.AuditMethodBodyContracts();

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts() => _session.StrictAsmdefContracts();

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts() => _session.AuditAsmdefContracts();

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts() => _session.StrictIndependenceContracts();

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts() => _session.AuditIndependenceContracts();

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts() => _session.StrictProtectedContracts();

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts() => _session.AuditProtectedContracts();

    public IEnumerable<ArchitectureExternalDependencyContract> StrictExternalContracts() => _session.StrictExternalContracts();

    public IEnumerable<ArchitectureExternalDependencyContract> AuditExternalContracts() => _session.AuditExternalContracts();

    public IEnumerable<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblingContracts() => _session.StrictAcyclicSiblingContracts();

    public IEnumerable<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblingContracts() => _session.AuditAcyclicSiblingContracts();

    public List<ArchitectureViolation> CheckConfiguration() => _session.CheckConfiguration();

    public List<ArchitectureViolation> CheckConfiguration(bool strict) => _session.CheckConfiguration(strict);

    public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency() => _session.CheckPolicyConsistency();

    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract) => _session.CheckContract(contract);

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract) => _session.CheckLayerContract(contract);

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract) => _session.CheckAllowOnlyContract(contract);

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract) => _session.CheckCycleContract(contract);

    public IReadOnlyCollection<string> CheckAcyclicSiblingContract(ArchitectureAcyclicSiblingContract contract) => _session.CheckAcyclicSiblingContract(contract);

    public List<ArchitectureViolation> CheckMethodBodyContract(ArchitectureMethodBodyContract contract) => _session.CheckMethodBodyContract(contract);

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract) => _session.CheckAsmdefContract(contract);

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract) => _session.CheckIndependenceContract(contract);

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract) => _session.CheckProtectedContract(contract);

    public List<ArchitectureViolation> CheckExternalContract(ArchitectureExternalDependencyContract contract) => _session.CheckExternalContract(contract);

    public ArchitectureCoverageSummary? BuildCoverageSummary(ArchitectureCoverageContract contract) => _session.BuildCoverageSummary(contract);

    public List<ArchitectureViolation> CheckCoverageContract(ArchitectureCoverageContract contract) => _session.CheckCoverageContract(contract);
}
