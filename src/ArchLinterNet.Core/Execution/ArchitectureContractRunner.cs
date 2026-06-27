using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureContractRunner(
    ArchitectureAnalysisContext context,
    ArchitectureContractDocument document,
    HashSet<string>? selectedContractIds = null,
    bool enableUnmatchedIgnoreTracking = true,
    IReadOnlyList<string>? preprocessorSymbols = null)
{
    private readonly ArchitectureAnalysisContext _context = context ?? throw new ArgumentNullException(nameof(context));

    private readonly ArchitectureAnalysisSession _session = new(context);

    private readonly ArchitectureContractDocument _document =
        document ?? throw new ArgumentNullException(nameof(document));

    private readonly HashSet<string>? _selectedContractIds = selectedContractIds;

    private readonly bool _enableUnmatchedIgnoreTracking = enableUnmatchedIgnoreTracking;

    private readonly IReadOnlyList<string>? _preprocessorSymbols = preprocessorSymbols;

    private readonly List<ArchitectureUnmatchedIgnoredViolation> _unmatchedIgnoredViolations = new();

    private readonly List<ArchitectureBaselineCandidate> _baselineCandidates = new();

    private readonly ArchitectureContractCatalog _catalog = ArchitectureContractCatalog.Build(document);

    private HashSet<string>? _ruleInputCoveredContractIdsForMode;

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _unmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _baselineCandidates;

    public ArchitectureContractCatalog Catalog => _catalog;

    internal ArchitectureAnalysisSession Session => _session;

    private ArchitectureContractExecutionContext CreateExecutionContext(
        IArchitectureContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations)
    {
        string? contractGroup = _enableUnmatchedIgnoreTracking ? ResolveContractGroup(contract) : null;
        return new ArchitectureContractExecutionContext(
            contract.Name, contract.Id, ignoredViolations, _enableUnmatchedIgnoreTracking, contractGroup, _baselineCandidates);
    }

    private string? ResolveContractGroup(IArchitectureContract contract)
    {
        return _catalog.ResolveGroup(contract);
    }

    private bool IsContractSelected(string? contractId)
    {
        return _selectedContractIds == null || _selectedContractIds.Count == 0
            || (contractId != null && _selectedContractIds.Contains(contractId));
    }

    // Called once by ArchitectureContractExecutor.Execute before any family loop runs, so every
    // Check*Contract call below can defer a dangling layer reference to rule-input coverage using
    // the exact mode/selection-aware set CheckConfiguration already computes — without each method
    // needing to know "mode" itself.
    internal void PrepareRuleInputCoverageDeferral(string mode)
    {
        _ruleInputCoveredContractIdsForMode = CollectRuleInputCoveredContractIds(mode == "strict");
    }

    // A contract whose layer-bearing field names a layer absent from `layers` would otherwise
    // throw via ArchitectureLayerResolver.ResolveLayer the moment its check runs. When a
    // rule_input coverage contract that will actually execute this request already tracks this
    // contract's ID, defer entirely to that coverage contract's "unresolved" finding instead of
    // crashing — mirroring CheckConfiguration's "empty layer namespace" deferral for the same
    // contract_ids-tracked relationship.
    private bool IsDanglingButCoveredByRuleInputCoverage(IArchitectureContract contract)
    {
        if (contract.Id == null
            || _ruleInputCoveredContractIdsForMode == null
            || !_ruleInputCoveredContractIdsForMode.Contains(contract.Id))
        {
            return false;
        }

        return GetReferencedLayerNames(contract).Any(layerName => !_document.Layers.ContainsKey(layerName));
    }

    public IEnumerable<ArchitectureDependencyContract> StrictContracts()
    {
        return _document.Contracts.Strict;
    }

    public IEnumerable<ArchitectureDependencyContract> AuditContracts()
    {
        return _document.Contracts.Audit;
    }

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts()
    {
        return _document.Contracts.StrictLayers;
    }

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts()
    {
        return _document.Contracts.AuditLayers;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts()
    {
        return _document.Contracts.StrictAllowOnly;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts()
    {
        return _document.Contracts.AuditAllowOnly;
    }

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts()
    {
        return _document.Contracts.StrictCycles;
    }

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts()
    {
        return _document.Contracts.AuditCycles;
    }

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts()
    {
        return _document.Contracts.StrictMethodBody;
    }

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts()
    {
        return _document.Contracts.AuditMethodBody;
    }

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts()
    {
        return _document.Contracts.StrictAsmdef;
    }

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts()
    {
        return _document.Contracts.AuditAsmdef;
    }

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts()
    {
        return _document.Contracts.StrictIndependence;
    }

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts()
    {
        return _document.Contracts.AuditIndependence;
    }

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts()
    {
        return _document.Contracts.StrictProtected;
    }

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts()
    {
        return _document.Contracts.AuditProtected;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> StrictExternalContracts()
    {
        return _document.Contracts.StrictExternal;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> AuditExternalContracts()
    {
        return _document.Contracts.AuditExternal;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblingContracts()
    {
        return _document.Contracts.StrictAcyclicSiblings;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblingContracts()
    {
        return _document.Contracts.AuditAcyclicSiblings;
    }

    public List<ArchitectureViolation> CheckConfiguration()
    {
        return CheckConfiguration(strict: true);
    }

    public List<ArchitectureViolation> CheckConfiguration(bool strict)
    {
        List<ArchitectureViolation> violations = new();

        foreach (string missingAssembly in _context.MissingAssemblyNames)
        {
            string probeInfo = _context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", _context.AssemblyProbingPaths)}"
                : string.Empty;

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" }));
        }

        foreach (ArchitectureProjectDiscoveryDiagnostic discoveryDiagnostic in _context.DiscoveryDiagnostics)
        {
            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                discoveryDiagnostic.Subject,
                discoveryDiagnostic.Kind,
                new[] { discoveryDiagnostic.Message }));
        }

        Dictionary<string, HashSet<string>> layerReferencingContractIds = new(StringComparer.Ordinal);
        HashSet<string> referencedExternalGroups = new(StringComparer.Ordinal);

        void AddLayerNames(string? contractId, IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                if (!layerReferencingContractIds.TryGetValue(name, out HashSet<string>? contractIds))
                {
                    contractIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    layerReferencingContractIds[name] = contractIds;
                }

                if (contractId != null)
                {
                    contractIds.Add(contractId);
                }
            }
        }

        void AddExternalGroupNames(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                referencedExternalGroups.Add(name);
            }
        }

        if (strict)
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Strict)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.StrictAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.StrictCycles)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.StrictMethodBody)
            {
                AddLayerNames(c.Id, new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.StrictIndependence)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.StrictLayers)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.StrictProtected)
            {
                AddLayerNames(c.Id, c.Protected);
                AddLayerNames(c.Id, c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in _document.Contracts.StrictExternal)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }
        }
        else
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Audit)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.AuditAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.AuditCycles)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.AuditMethodBody)
            {
                AddLayerNames(c.Id, new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.AuditIndependence)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.AuditLayers)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.AuditProtected)
            {
                AddLayerNames(c.Id, c.Protected);
                AddLayerNames(c.Id, c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in _document.Contracts.AuditExternal)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }
        }

        HashSet<string> ruleInputCoveredContractIds = CollectRuleInputCoveredContractIds(strict);

        foreach ((string layerName, HashSet<string> referencingContractIds) in layerReferencingContractIds)
        {
            bool isFullyOwnedByRuleInputCoverage = referencingContractIds.Count > 0
                && referencingContractIds.All(ruleInputCoveredContractIds.Contains);

            if (!_document.Layers.ContainsKey(layerName))
            {
                // A dangling layer name referenced exclusively by contracts a rule_input coverage
                // contract tracks defers to that coverage contract's own "unresolved" finding
                // instead of throwing here — otherwise scope: rule_input's unresolved diagnostic
                // would be unreachable through the real validation pipeline, since this resolution
                // happens before any contract or coverage check runs.
                if (isFullyOwnedByRuleInputCoverage)
                {
                    continue;
                }
            }

            ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(_document, "<configuration>", layerName);

            if (layer.External)
            {
                continue;
            }

            Type[] types = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, layer);

            if (types.Length == 0)
            {
                // A layer referenced exclusively by contracts that a rule_input coverage contract
                // explicitly tracks (via contract_ids) defers to that coverage contract's own
                // empty-input classification and severity instead of also failing here as a hard,
                // unconditional configuration error — otherwise analysis.coverage and exclude
                // entries could never actually govern the outcome for these contracts.
                if (isFullyOwnedByRuleInputCoverage)
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    ArchitectureLayerResolver.DescribeLayer(layer),
                    "empty layer namespace",
                    new[] { $"Layer '{layerName}' namespace '{layer.Namespace}' contains no types in loaded assemblies." }));
            }
        }

        foreach (string groupName in referencedExternalGroups)
        {
            if (!_document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
            {
                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    groupName,
                    "unknown external dependency group",
                    new[]
                    {
                        $"External dependency group '{groupName}' is referenced by a contract but is not declared in external_dependencies."
                    })
                {
                    ForbiddenExternalGroup = groupName
                });

                continue;
            }

            if (ArchitectureExternalDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                groupName,
                "invalid external dependency group",
                new[]
                {
                    $"External dependency group '{groupName}' must declare at least one non-empty namespace_prefixes or type_prefixes matcher."
                })
            {
                ForbiddenExternalGroup = groupName
            });
        }

        return violations;
    }

    // Only contracts that ArchitectureContractExecutor will actually run for this request can
    // defer CheckConfiguration's hard failure: ContractsFor(mode, "coverage") only executes the
    // group matching the current mode (strict_coverage for strict, audit_coverage for audit), and
    // CheckCoverageContract itself no-ops when the coverage contract isn't selected. Deferring
    // for a coverage contract that won't run this request would silently drop the finding
    // entirely instead of handing it off — the same false-green risk this deferral exists to
    // avoid in the first place.
    private HashSet<string> CollectRuleInputCoveredContractIds(bool strict)
    {
        IEnumerable<ArchitectureCoverageContract> coverageContractsForMode = strict
            ? _document.Contracts.StrictCoverage
            : _document.Contracts.AuditCoverage;

        return new HashSet<string>(
            coverageContractsForMode
                .Where(c => string.Equals(c.Scope, "rule_input", StringComparison.Ordinal))
                .Where(c => IsContractSelected(c.Id))
                .SelectMany(c => c.ContractIds),
            StringComparer.OrdinalIgnoreCase);
    }
}
