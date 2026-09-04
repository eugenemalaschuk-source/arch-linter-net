using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Execution.Configuration;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Execution.Results;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// The per-validation-run session/context: owns every piece of state shared across contract-family
// checks for one run (resolved assemblies/type/reference caches, the document being validated,
// contract selection, and the mutable unmatched-ignore/baseline-candidate/rule-input-coverage
// tracking that accumulates as checks execute). ArchitectureContractRunner is a thin facade over
// this session kept for public API stability; handlers receive this session, not the runner.
public sealed class ArchitectureAnalysisSession
{
    private ArchitectureCoverageInventory? _cachedCoverageInventory;
    private ArchitectureContractDocument? _cachedCoverageInventoryDocument;

    private readonly List<ArchitectureUnmatchedIgnoredViolation> _unmatchedIgnoredViolations = new();

    private HashSet<string>? _ruleInputCoveredContractIdsForMode;

    private readonly ArchitectureConfigurationValidationService _configurationValidationService;

    private readonly ArchitectureCoreContractCheckingService _coreContractCheckingService;

    private readonly ArchitectureSupplementalContractCheckingService _supplementalContractCheckingService;

    private readonly ArchitectureFrameworkReferenceAnalysisService _frameworkReferenceAnalysisService;

    private readonly ArchitectureClassificationAnalysisService _classificationAnalysisService;

    private readonly ArchitectureContextualConsumerRegistry _contextualConsumerRegistry;

    private readonly ArchitecturePublicApiSurfaceAnalysisService _publicApiSurfaceAnalysisService;

    private readonly ArchitecturePublicApiSurfaceIndex _publicApiSurfaceIndex = new();

    private readonly ArchitectureContractSurfaceExposureIndex _contractSurfaceExposureIndex = new();

    private readonly ArchitectureCycleBaselineCandidateRecorder _cycleBaselineCandidateRecorder = new();

    private readonly List<ArchitectureMetricBaselineEntry> _metricBaselineCandidates = new();

    private readonly ArchitectureFindingIdentityService _findingIdentityService = new();

    private readonly ArchitectureSubtractiveMatcherParticipationRecorder _subtractiveMatcherParticipationRecorder;

    private readonly ArchitectureContractSelectionService _contractSelectionService;

    private readonly ArchitecturePolicyConsistencyAnalysisService _policyConsistencyAnalysisService;

    private readonly ArchitectureCoverageAnalysisService _coverageAnalysisService;

    private readonly ArchitectureCoverageSummaryService _coverageSummaryService;

    internal ArchitectureAnalysisFactService Facts { get; }

    private ArchitectureCheckerContext? _checkerContext;

    public ArchitectureAnalysisSession(
        ArchitectureAnalysisContext context,
        ArchitectureContractDocument document,
        HashSet<string>? selectedContractIds,
        bool enableUnmatchedIgnoreTracking,
        IReadOnlyList<string>? preprocessorSymbols)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        SelectedContractIds = selectedContractIds;
        EnableUnmatchedIgnoreTracking = enableUnmatchedIgnoreTracking;
        PreprocessorSymbols = preprocessorSymbols;
        Catalog = ArchitectureContractCatalog.Build(document);
        TypeIndex = new ArchitectureTypeIndex(
            context.TargetAssemblies, context.MaxParallelism, context.ProfilingCounters, context.CancellationToken);
        RoleIndex = new ArchitectureRoleIndex(document.Classification, TypeIndex, context.CancellationToken);
        SourceFileFactIndex = new ArchitectureSourceFileFactIndex(
            context.TargetAssemblies,
            context.RepositoryRoot,
            document.Analysis.SourceRoots,
            preprocessorSymbols,
            fileSystem: null,
            new ArchitectureSourceFileFactIndex.ProjectOwnership(context.ProjectDiscovery, SourceRootAssemblyOwnership: null),
            new ArchitectureSourceFileFactIndex.ConstructionOptions(
                context.ProfilingCounters, context.CancellationToken, context.MaxParallelism));
        ExpressionFacts = new ArchitectureExpressionFactService(RoleIndex, SourceFileFactIndex, context.ProjectDiscovery);
        Facts = new ArchitectureAnalysisFactService(
            context,
            document,
            TypeIndex,
            RoleIndex,
            ExpressionFacts,
            new ArchitectureSessionMetadataIndexes(context));
        ExternalDependencyFacts = new ArchitectureExternalDependencyFactIndex(this);
        _contextualConsumerRegistry = new ArchitectureContextualConsumerRegistry();
        RegisterAllContextualConsumersFromDocument();
        _configurationValidationService = new ArchitectureConfigurationValidationService(this);
        _coreContractCheckingService = new ArchitectureCoreContractCheckingService(this);
        _supplementalContractCheckingService = new ArchitectureSupplementalContractCheckingService(this);
        _frameworkReferenceAnalysisService = new ArchitectureFrameworkReferenceAnalysisService(this);
        _classificationAnalysisService = new ArchitectureClassificationAnalysisService(this);
        _publicApiSurfaceAnalysisService = new ArchitecturePublicApiSurfaceAnalysisService(this);
        _subtractiveMatcherParticipationRecorder = new ArchitectureSubtractiveMatcherParticipationRecorder(this);
        _contractSelectionService = new ArchitectureContractSelectionService(this);
        _policyConsistencyAnalysisService = new ArchitecturePolicyConsistencyAnalysisService(this);
        _coverageAnalysisService = new ArchitectureCoverageAnalysisService(this);
        _coverageSummaryService = new ArchitectureCoverageSummaryService(
            this, _coverageAnalysisService, _coverageAnalysisService.SemanticCoverage);
    }

    // Registered eagerly at construction, before any contract-family checker (including a future
    // coverage handler) executes — the registry currently runs "coverage" before either contextual
    // family (see ArchitectureContractFamilyRegistry.All), so registering lazily inside
    // CheckContextDependencyContract/CheckContextAllowOnlyContract would leave this collection empty
    // by the time a #114 coverage checker reads it. Matches BuildConfigurationReferenceCollector's
    // existing convention of collecting per-family configuration references (layer names, here
    // role/metadata) independent of --contract-id selection, not gated by IsContractSelected.
    private void RegisterAllContextualConsumersFromDocument()
    {
        foreach (ArchitectureContextDependencyContract contract in Document.Contracts.StrictContextDependencies
                     .Concat(Document.Contracts.AuditContextDependencies))
        {
            RegisterContextualConsumers(contract.Source, contract.Forbidden, contract.Exclude);
        }

        foreach (ArchitectureContextAllowOnlyContract contract in Document.Contracts.StrictContextAllowOnly
                     .Concat(Document.Contracts.AuditContextAllowOnly))
        {
            RegisterContextualConsumers(contract.Source, contract.Allowed, contract.Exclude);
        }
    }

    public ArchitectureAnalysisContext Context { get; }

    public ArchitectureContractDocument Document { get; }

    public HashSet<string>? SelectedContractIds { get; }

    public bool EnableUnmatchedIgnoreTracking { get; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; }

    public ArchitectureContractCatalog Catalog { get; }

    public ArchitectureTypeIndex TypeIndex { get; }

    public ArchitectureRoleIndex RoleIndex { get; }

    public ArchitectureSourceFileFactIndex SourceFileFactIndex { get; }

    internal ArchitectureExpressionFactService ExpressionFacts { get; }

    internal ArchitecturePublicApiSurfaceMaterialization GetPublicApiSurface(Assembly assembly) =>
        _publicApiSurfaceIndex.GetOrMaterialize(assembly);

    // Friend-test evidence only; the index remains private to this session and no counter enters
    // the public analysis profile or schema.
    internal int PublicApiSurfaceMaterializationCount => _publicApiSurfaceIndex.MaterializationCount;

    // Caller-selected roots keep reviewed public-API membership authoritative. The exposure index
    // only materializes reusable reflection facts and never selects roots or assigns policy roles.
    internal ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(IEnumerable<Type> roots) =>
        _contractSurfaceExposureIndex.GetOrMaterialize(roots);

    internal ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(
        IEnumerable<Type> roots,
        ArchitectureContractSurfaceShape surfaceShape) =>
        _contractSurfaceExposureIndex.GetOrMaterialize(roots, surfaceShape);

    internal ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(
        Type root,
        ArchitectureContractSurfaceShape surfaceShape) =>
        _contractSurfaceExposureIndex.GetOrMaterialize(root, surfaceShape);

    internal ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(params Type[] roots) =>
        _contractSurfaceExposureIndex.GetOrMaterialize(roots);

    internal ArchitectureContractSurfaceExposureResult GetContractSurfaceExposure(
        ArchitectureContractSurfaceShape surfaceShape,
        params Type[] roots) =>
        _contractSurfaceExposureIndex.GetOrMaterialize(roots, surfaceShape);

    internal int ContractSurfaceExposureMaterializationCount =>
        _contractSurfaceExposureIndex.MaterializationCount;

    internal ArchitectureCheckerContext CheckerContext => _checkerContext ??= new ArchitectureCheckerContext(this);

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();

    // Direct external-group facts are materialized once per analysis session. Measurement and
    // future consumers read this projection instead of re-running the external violation finders
    // or an IL/reference scan of their own.
    internal ArchitectureExternalDependencyFactIndex ExternalDependencyFacts { get; }

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _unmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _cycleBaselineCandidateRecorder.Candidates;

    // Separate from finding candidates by design: scalar metric values never participate in
    // occurrence attribution, ignore matching, or finding-debt lifecycle comparison.
    internal IReadOnlyList<ArchitectureMetricBaselineEntry> MetricBaselineCandidates
        => _metricBaselineCandidates;

    // Coverage-participating consumption recorded by contextual dependency/allow-only contracts.
    // See ArchitectureContextualConsumerReference and design.md Decision 7. Nothing consumes this
    // collection yet — it exists so a future coverage change can query it.
    public IReadOnlyCollection<ArchitectureContextualConsumerReference> RegisteredContextualConsumers
        => _contextualConsumerRegistry.Consumers;

    // Cached per session so multiple future coverage contract handlers share one inventory instead of
    // each rebuilding it; an explicit projectDiscovery override bypasses the cache (test-only substitution).
    public ArchitectureCoverageInventory BuildCoverageInventory(
        ArchitectureContractDocument document,
        ProjectDiscoveryResult? projectDiscovery = null)
    {
        if (projectDiscovery != null)
        {
            return ArchitectureCoverageInventory.Build(document, this, projectDiscovery);
        }

        if (_cachedCoverageInventory != null && ReferenceEquals(_cachedCoverageInventoryDocument, document))
        {
            return _cachedCoverageInventory;
        }

        ArchitectureCoverageInventory inventory = ArchitectureCoverageInventory.Build(document, this, Context.ProjectDiscovery);
        _cachedCoverageInventory = inventory;
        _cachedCoverageInventoryDocument = document;
        return inventory;
    }

    internal ArchitectureContractExecutionContext CreateExecutionContext(
        IArchitectureContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations)
    {
        string? contractGroup = ResolveContractGroup(contract);
        return new ArchitectureContractExecutionContext(
            contract.Name,
            contract.Id,
            ignoredViolations,
            EnableUnmatchedIgnoreTracking,
            contractGroup,
            _cycleBaselineCandidateRecorder.CandidateStore,
            _findingIdentityService.Candidates);
    }

    internal void CollectUnmatchedIgnores(ArchitectureContractExecutionContext executionContext)
    {
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
    }

    internal string? ResolveContractGroup(IArchitectureContract contract)
    {
        return Catalog.ResolveGroup(contract);
    }

    public bool IsContractSelected(string? contractId)
    {
        return SelectedContractIds == null || SelectedContractIds.Count == 0
            || (contractId != null && SelectedContractIds.Contains(contractId));
    }

    public bool IsContractSelected(IArchitectureContract contract) => _contractSelectionService.IsContractSelected(contract);

    internal bool IsDanglingButCoveredByRuleInputCoverage(IArchitectureContract contract) =>
        _contractSelectionService.IsDanglingButCoveredByRuleInputCoverage(contract);

    internal HashSet<string>? RuleInputCoveredContractIdsForMode => _ruleInputCoveredContractIdsForMode;

    // Called once by ArchitectureContractExecutor.Execute before any family loop runs, so every
    // Check*Contract call below can defer a dangling layer reference to rule-input coverage using
    // the exact mode/selection-aware set CheckConfiguration already computes — without each method
    // needing to know "mode" itself.
    public void PrepareRuleInputCoverageDeferral(string mode)
    {
        _ruleInputCoveredContractIdsForMode = _configurationValidationService.CollectRuleInputCoveredContractIds(mode == "strict");
    }

    public IEnumerable<ArchitectureDependencyContract> StrictContracts() => Document.Contracts.Strict;

    public IEnumerable<ArchitectureDependencyContract> AuditContracts() => Document.Contracts.Audit;

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts() => Document.Contracts.StrictLayers;

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts() => Document.Contracts.AuditLayers;

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts() => Document.Contracts.StrictAllowOnly;

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts() => Document.Contracts.AuditAllowOnly;

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts() => Document.Contracts.StrictCycles;

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts() => Document.Contracts.AuditCycles;

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts() => Document.Contracts.StrictMethodBody;

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts() => Document.Contracts.AuditMethodBody;

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts() => Document.Contracts.StrictAsmdef;

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts() => Document.Contracts.AuditAsmdef;

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts() => Document.Contracts.StrictIndependence;

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts() => Document.Contracts.AuditIndependence;

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts() => Document.Contracts.StrictProtected;

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts() => Document.Contracts.AuditProtected;

    public IEnumerable<ArchitectureExternalDependencyContract> StrictExternalContracts() => Document.Contracts.StrictExternal;

    public IEnumerable<ArchitectureExternalDependencyContract> AuditExternalContracts() => Document.Contracts.AuditExternal;

    public IEnumerable<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblingContracts() => Document.Contracts.StrictAcyclicSiblings;

    public IEnumerable<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblingContracts() => Document.Contracts.AuditAcyclicSiblings;

    public IEnumerable<ArchitectureModuleContainerContract> StrictModuleContainerContracts() => Document.Contracts.StrictModuleContainers;

    public IEnumerable<ArchitectureModuleContainerContract> AuditModuleContainerContracts() => Document.Contracts.AuditModuleContainers;

    public List<ArchitectureViolation> CheckConfiguration()
    {
        return CheckConfiguration(strict: true);
    }

    public List<ArchitectureViolation> CheckConfiguration(bool strict)
    {
        return _configurationValidationService.Check(strict);
    }

    public List<PolicyConsistencyDiagnostic> CheckPolicyConsistency() =>
        _policyConsistencyAnalysisService.Check();

    internal List<ArchitectureContractDescriptor> BuildAllDescriptors() =>
        _policyConsistencyAnalysisService.BuildAllDescriptors();

    public ArchitectureCoverageSummary? BuildCoverageSummary(ArchitectureCoverageContract contract) =>
        _coverageSummaryService.Build(contract);

    public List<ArchitectureViolation> CheckCoverageContract(ArchitectureCoverageContract contract) =>
        _coverageAnalysisService.CheckCoverageContract(contract);

    public List<ArchitectureViolation> CheckMetricBudgetContract(ArchitectureMetricBudgetContract contract) =>
        ArchitectureMetricBudgetAnalysisService.Evaluate(this, [contract]).Violations.ToList();

    internal ArchitectureMetricBudgetEvaluationResult CheckMetricBudgetContracts(
        IReadOnlyCollection<ArchitectureMetricBudgetContract> contracts) =>
        ArchitectureMetricBudgetAnalysisService.Evaluate(this, contracts);

    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract) =>
        _coreContractCheckingService.CheckContract(contract);

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract) =>
        _coreContractCheckingService.CheckLayerContract(contract);

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract) =>
        _coreContractCheckingService.CheckAllowOnlyContract(contract);

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract) =>
        _coreContractCheckingService.CheckCycleContract(contract);

    public IReadOnlyCollection<string> CheckAcyclicSiblingContract(ArchitectureAcyclicSiblingContract contract) =>
        _coreContractCheckingService.CheckAcyclicSiblingContract(contract);

    public List<ArchitectureViolation> CheckModuleContainerContract(ArchitectureModuleContainerContract contract) =>
        _coreContractCheckingService.CheckModuleContainerContract(contract);

    public List<ArchitectureViolation> CheckMethodBodyContract(ArchitectureMethodBodyContract contract) =>
        _coreContractCheckingService.CheckMethodBodyContract(contract);

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract) =>
        _coreContractCheckingService.CheckAsmdefContract(contract);

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract) =>
        _coreContractCheckingService.CheckIndependenceContract(contract);

    public List<ArchitectureViolation> CheckExternalContract(ArchitectureExternalDependencyContract contract) =>
        _coreContractCheckingService.CheckExternalContract(contract);

    public List<ArchitectureViolation> CheckExternalAllowOnlyContract(ArchitectureExternalAllowOnlyContract contract) =>
        _coreContractCheckingService.CheckExternalAllowOnlyContract(contract);

    public List<ArchitectureViolation> CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract) =>
        _supplementalContractCheckingService.CheckAssemblyIndependenceContract(contract);

    public List<ArchitectureViolation> CheckPortBoundaryContract(ArchitecturePortBoundaryContract contract) =>
        _supplementalContractCheckingService.CheckPortBoundaryContract(contract);

    public List<ArchitectureViolation> CheckAttributeUsageContract(ArchitectureAttributeUsageContract contract) =>
        _supplementalContractCheckingService.CheckAttributeUsageContract(contract);

    public List<ArchitectureViolation> CheckAssemblyDependencyContract(ArchitectureAssemblyDependencyContract contract) =>
        _supplementalContractCheckingService.CheckAssemblyDependencyContract(contract);

    public List<ArchitectureViolation> CheckAssemblyAllowOnlyContract(ArchitectureAssemblyAllowOnlyContract contract) =>
        _supplementalContractCheckingService.CheckAssemblyAllowOnlyContract(contract);

    public List<ArchitectureViolation> CheckCompositionContract(ArchitectureCompositionContract contract) =>
        _supplementalContractCheckingService.CheckCompositionContract(contract);

    public List<ArchitectureViolation> CheckInheritanceContract(ArchitectureInheritanceContract contract) =>
        _supplementalContractCheckingService.CheckInheritanceContract(contract);

    public List<ArchitectureViolation> CheckInterfaceImplementationContract(ArchitectureInterfaceImplementationContract contract) =>
        _supplementalContractCheckingService.CheckInterfaceImplementationContract(contract);

    public List<ArchitectureViolation> CheckLayoutConventionsContract(ArchitectureLayoutConventionContract contract) =>
        _supplementalContractCheckingService.CheckLayoutConventionsContract(contract);

    public List<ArchitectureViolation> CheckPackageDependencyContract(ArchitecturePackageDependencyContract contract) =>
        _supplementalContractCheckingService.CheckPackageDependencyContract(contract);

    public List<ArchitectureViolation> CheckPackageAllowOnlyContract(ArchitecturePackageAllowOnlyContract contract) =>
        _supplementalContractCheckingService.CheckPackageAllowOnlyContract(contract);

    public List<ArchitectureViolation> CheckProjectMetadataContract(ArchitectureProjectMetadataContract contract) =>
        _supplementalContractCheckingService.CheckProjectMetadataContract(contract);

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract) =>
        _supplementalContractCheckingService.CheckProtectedContract(contract);

    public List<ArchitectureViolation> CheckTypePlacementContract(ArchitectureTypePlacementContract contract) =>
        _supplementalContractCheckingService.CheckTypePlacementContract(contract);

    public List<ArchitectureViolation> CheckContextDependencyContract(ArchitectureContextDependencyContract contract) =>
        _supplementalContractCheckingService.CheckContextDependencyContract(contract);

    public List<ArchitectureViolation> CheckContextAllowOnlyContract(ArchitectureContextAllowOnlyContract contract) =>
        _supplementalContractCheckingService.CheckContextAllowOnlyContract(contract);

    public (IReadOnlyList<ArchitectureClassificationConflict> Conflicts, IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
        CheckClassificationFacts() => _classificationAnalysisService.CheckClassificationFacts();

    public IReadOnlyList<ArchitectureClassificationRoleFact> CheckClassificationRoles() =>
        _classificationAnalysisService.CheckClassificationRoles();

    public ArchitectureClassificationPathDeferredNotice? CheckClassificationPathDeferred() =>
        _classificationAnalysisService.CheckClassificationPathDeferred();

    public List<ArchitectureViolation> CheckPublicApiSurfaceContract(ArchitecturePublicApiSurfaceContract contract) =>
        _publicApiSurfaceAnalysisService.CheckPublicApiSurfaceContract(contract);

    public ArchitectureHandlerResult CheckContractSurfaceExposureContract(
        ArchitectureContractSurfaceExposureContract contract) =>
        _coreContractCheckingService.CheckContractSurfaceExposureContract(contract);

    public ArchitectureHandlerResult CheckVersionedContractSurfaceIsolationContract(
        ArchitectureVersionedContractSurfaceIsolationContract contract) =>
        _coreContractCheckingService.CheckVersionedContractSurfaceIsolationContract(contract);

    internal ArchitecturePublicApiSurfaceRootResolution ResolvePublicApiSurfaceRoots(string publicApiSurfaceId) =>
        _publicApiSurfaceAnalysisService.ResolveSelectedRoots(publicApiSurfaceId);

    public IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies) =>
        _publicApiSurfaceAnalysisService.CapturePublicApiSurface(contract, out missingAssemblies);

    internal IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies,
        out IReadOnlyList<ArchitectureViolation> selectorSafetyViolations) =>
        _publicApiSurfaceAnalysisService.CapturePublicApiSurface(
            contract, out missingAssemblies, out selectorSafetyViolations);

    internal IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies,
        out IReadOnlyList<ArchitectureViolation> selectorSafetyViolations,
        out bool isComplete) =>
        _publicApiSurfaceAnalysisService.CapturePublicApiSurface(
            contract, out missingAssemblies, out selectorSafetyViolations, out isComplete);

    public List<ArchitectureViolation> CheckFrameworkDependencyContract(ArchitectureFrameworkReferenceContract contract) =>
        _frameworkReferenceAnalysisService.CheckFrameworkDependencyContract(contract);

    public List<ArchitectureViolation> CheckFrameworkAllowOnlyContract(ArchitectureFrameworkReferenceAllowOnlyContract contract) =>
        _frameworkReferenceAnalysisService.CheckFrameworkAllowOnlyContract(contract);

    internal ArchitectureDiscoveredFrameworkReference[] ResolveFrameworkReferences(string sourceAssemblyName) =>
        _frameworkReferenceAnalysisService.ResolveFrameworkReferences(sourceAssemblyName);

    internal string ResolvedBuildConfiguration => _frameworkReferenceAnalysisService.ResolvedBuildConfiguration;

    internal void AddFrameworkEvaluationFailureViolations(
        List<ArchitectureViolation> violations,
        ArchitectureConfigurationReferenceCollector collector) =>
        _frameworkReferenceAnalysisService.AddFrameworkEvaluationFailureViolations(violations, collector);

    internal void AddCycleBaselineCandidates(
        IReadOnlyDictionary<string, HashSet<string>> fullGraph,
        IReadOnlyCollection<CycleCandidateEvidence> candidateEvidence) =>
        _cycleBaselineCandidateRecorder.Record(EnableUnmatchedIgnoreTracking, fullGraph, candidateEvidence);

    internal void AddMetricBaselineCandidate(ArchitectureMetricBaselineEntry candidate)
    {
        if (_metricBaselineCandidates.Any(existing =>
                string.Equals(existing.MetricId, candidate.MetricId, StringComparison.Ordinal)))
        {
            return;
        }

        _metricBaselineCandidates.Add(candidate);
    }

    internal int FindingIdentityCursor => _findingIdentityService.Cursor;

    internal IReadOnlyList<ArchitectureViolation> AttachFindingIdentities(
        IReadOnlyCollection<ArchitectureViolation> violations,
        int cursor) => _findingIdentityService.Attach(violations, cursor);

    public IReadOnlyList<ArchitectureSubtractiveMatcherParticipation> SubtractiveMatcherParticipation =>
        _subtractiveMatcherParticipationRecorder.Participations;

    internal void RecordSubtractiveMatcherParticipation(
        IArchitectureContract contract,
        string field,
        int? index,
        bool matched,
        bool evaluationFailed = false,
        ArchitectureSelectorParticipationKind kind = ArchitectureSelectorParticipationKind.Exclusion) =>
        _subtractiveMatcherParticipationRecorder.Record(contract, field, index, matched, evaluationFailed, kind);

    internal void RegisterContextualConsumer(ArchitectureContextSelector selector) =>
        _contextualConsumerRegistry.RegisterContextualConsumer(selector);

    internal void RegisterContextualConsumer(ArchitectureContextSelector source, ArchitectureContextSelector selector) =>
        _contextualConsumerRegistry.RegisterContextualConsumer(source, selector);

    private void RegisterContextualConsumers(
        ArchitectureContextSelector source,
        IEnumerable<ArchitectureContextSelector> targetSelectors,
        IEnumerable<ArchitectureContextSelector> excludeSelectors)
    {
        RegisterContextualConsumer(source);

        foreach (ArchitectureContextSelector selector in targetSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }

        foreach (ArchitectureContextSelector selector in excludeSelectors)
        {
            RegisterContextualConsumer(source, selector);
        }
    }

}
