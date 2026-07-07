using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// The per-validation-run session/context: owns every piece of state shared across contract-family
// checks for one run (resolved assemblies/type/reference caches, the document being validated,
// contract selection, and the mutable unmatched-ignore/baseline-candidate/rule-input-coverage
// tracking that accumulates as checks execute). ArchitectureContractRunner is a thin facade over
// this session kept for public API stability; handlers receive this session, not the runner.
public sealed partial class ArchitectureAnalysisSession
{
    private ArchitectureCoverageInventory? _cachedCoverageInventory;
    private ArchitectureContractDocument? _cachedCoverageInventoryDocument;

    private readonly List<ArchitectureUnmatchedIgnoredViolation> _unmatchedIgnoredViolations = new();

    private readonly List<ArchitectureBaselineCandidate> _baselineCandidates = new();

    private HashSet<string>? _ruleInputCoveredContractIdsForMode;

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
        TypeIndex = new ArchitectureTypeIndex(context.TargetAssemblies);
    }

    public ArchitectureAnalysisContext Context { get; }

    public ArchitectureContractDocument Document { get; }

    public HashSet<string>? SelectedContractIds { get; }

    public bool EnableUnmatchedIgnoreTracking { get; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; }

    public ArchitectureContractCatalog Catalog { get; }

    public ArchitectureTypeIndex TypeIndex { get; }

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _unmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _baselineCandidates;

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

    private ArchitectureContractExecutionContext CreateExecutionContext(
        IArchitectureContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations)
    {
        string? contractGroup = EnableUnmatchedIgnoreTracking ? ResolveContractGroup(contract) : null;
        return new ArchitectureContractExecutionContext(
            contract.Name, contract.Id, ignoredViolations, EnableUnmatchedIgnoreTracking, contractGroup, _baselineCandidates);
    }

    private string? ResolveContractGroup(IArchitectureContract contract)
    {
        return Catalog.ResolveGroup(contract);
    }

    public bool IsContractSelected(string? contractId)
    {
        return SelectedContractIds == null || SelectedContractIds.Count == 0
            || (contractId != null && SelectedContractIds.Contains(contractId));
    }

    // Called once by ArchitectureContractExecutor.Execute before any family loop runs, so every
    // Check*Contract call below can defer a dangling layer reference to rule-input coverage using
    // the exact mode/selection-aware set CheckConfiguration already computes — without each method
    // needing to know "mode" itself.
    public void PrepareRuleInputCoverageDeferral(string mode)
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

        return GetReferencedLayerNames(contract).Any(layerName => !Document.Layers.ContainsKey(layerName));
    }

    public IEnumerable<ArchitectureDependencyContract> StrictContracts()
    {
        return Document.Contracts.Strict;
    }

    public IEnumerable<ArchitectureDependencyContract> AuditContracts()
    {
        return Document.Contracts.Audit;
    }

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts()
    {
        return Document.Contracts.StrictLayers;
    }

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts()
    {
        return Document.Contracts.AuditLayers;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts()
    {
        return Document.Contracts.StrictAllowOnly;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts()
    {
        return Document.Contracts.AuditAllowOnly;
    }

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts()
    {
        return Document.Contracts.StrictCycles;
    }

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts()
    {
        return Document.Contracts.AuditCycles;
    }

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts()
    {
        return Document.Contracts.StrictMethodBody;
    }

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts()
    {
        return Document.Contracts.AuditMethodBody;
    }

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts()
    {
        return Document.Contracts.StrictAsmdef;
    }

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts()
    {
        return Document.Contracts.AuditAsmdef;
    }

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts()
    {
        return Document.Contracts.StrictIndependence;
    }

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts()
    {
        return Document.Contracts.AuditIndependence;
    }

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts()
    {
        return Document.Contracts.StrictProtected;
    }

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts()
    {
        return Document.Contracts.AuditProtected;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> StrictExternalContracts()
    {
        return Document.Contracts.StrictExternal;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> AuditExternalContracts()
    {
        return Document.Contracts.AuditExternal;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblingContracts()
    {
        return Document.Contracts.StrictAcyclicSiblings;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblingContracts()
    {
        return Document.Contracts.AuditAcyclicSiblings;
    }

    public List<ArchitectureViolation> CheckConfiguration()
    {
        return CheckConfiguration(strict: true);
    }

    public List<ArchitectureViolation> CheckConfiguration(bool strict)
    {
        List<ArchitectureViolation> violations = new();

        foreach (string missingAssembly in Context.MissingAssemblyNames)
        {
            string probeInfo = Context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", Context.AssemblyProbingPaths)}"
                : string.Empty;

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" }));
        }

        foreach (ArchitectureProjectDiscoveryDiagnostic discoveryDiagnostic in Context.DiscoveryDiagnostics)
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
        HashSet<string> referencedPackageGroups = new(StringComparer.Ordinal);
        List<(string ContractName, string? ContractId, string Source)> packageContractSources = new();
        List<(string ContractName, string? ContractId, string ProjectPath)> projectMetadataContractProjects = new();

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

        void AddPackageGroupNames(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                referencedPackageGroups.Add(name);
            }
        }

        if (strict)
        {
            foreach (ArchitectureDependencyContract c in Document.Contracts.Strict)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in Document.Contracts.StrictAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Allowed);
            }

            foreach (ArchitectureCycleContract c in Document.Contracts.StrictCycles)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in Document.Contracts.StrictMethodBody)
            {
                AddLayerNames(c.Id, new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in Document.Contracts.StrictIndependence)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureLayerContract c in Document.Contracts.StrictLayers)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureProtectedContract c in Document.Contracts.StrictProtected)
            {
                AddLayerNames(c.Id, c.Protected);
                AddLayerNames(c.Id, c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in Document.Contracts.StrictExternal)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }

            foreach (ArchitectureExternalAllowOnlyContract c in Document.Contracts.StrictExternalAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(Document.ExternalDependencies.Keys.Where(name => !c.Allowed.Contains(name)));
            }

            foreach (ArchitecturePackageDependencyContract c in Document.Contracts.StrictPackageDependency)
            {
                AddPackageGroupNames(c.Forbidden);
                packageContractSources.Add((c.Name, c.Id, c.Source));
            }

            foreach (ArchitecturePackageAllowOnlyContract c in Document.Contracts.StrictPackageAllowOnly)
            {
                AddPackageGroupNames(c.Allowed);
                packageContractSources.Add((c.Name, c.Id, c.Source));
            }

            foreach (ArchitectureProjectMetadataContract c in Document.Contracts.StrictProjectMetadata)
            {
                projectMetadataContractProjects.AddRange(c.Projects.Select(project => (c.Name, c.Id, NormalizeProjectPath(project))));
            }

            foreach (ArchitectureTypePlacementContract c in Document.Contracts.StrictTypePlacement)
            {
                AddLayerNames(c.Id, GetTypePlacementReferencedLayerNames(c));
            }

            foreach (ArchitectureAttributeUsageContract c in Document.Contracts.StrictAttributeUsage)
            {
                AddLayerNames(c.Id, GetAttributeUsageReferencedLayerNames(c));
            }

            foreach (ArchitectureInheritanceContract c in Document.Contracts.StrictInheritance)
            {
                AddLayerNames(c.Id, c.SourceLayers);
            }

            foreach (ArchitectureInterfaceImplementationContract c in Document.Contracts.StrictInterfaceImplementation)
            {
                AddLayerNames(c.Id, GetInterfaceImplementationReferencedLayerNames(c));
            }
        }
        else
        {
            foreach (ArchitectureDependencyContract c in Document.Contracts.Audit)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in Document.Contracts.AuditAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddLayerNames(c.Id, c.Allowed);
            }

            foreach (ArchitectureCycleContract c in Document.Contracts.AuditCycles)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in Document.Contracts.AuditMethodBody)
            {
                AddLayerNames(c.Id, new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in Document.Contracts.AuditIndependence)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureLayerContract c in Document.Contracts.AuditLayers)
            {
                AddLayerNames(c.Id, c.Layers);
            }

            foreach (ArchitectureProtectedContract c in Document.Contracts.AuditProtected)
            {
                AddLayerNames(c.Id, c.Protected);
                AddLayerNames(c.Id, c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in Document.Contracts.AuditExternal)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }

            foreach (ArchitectureExternalAllowOnlyContract c in Document.Contracts.AuditExternalAllowOnly)
            {
                AddLayerNames(c.Id, new[] { c.Source });
                AddExternalGroupNames(Document.ExternalDependencies.Keys.Where(name => !c.Allowed.Contains(name)));
            }

            foreach (ArchitecturePackageDependencyContract c in Document.Contracts.AuditPackageDependency)
            {
                AddPackageGroupNames(c.Forbidden);
                packageContractSources.Add((c.Name, c.Id, c.Source));
            }

            foreach (ArchitecturePackageAllowOnlyContract c in Document.Contracts.AuditPackageAllowOnly)
            {
                AddPackageGroupNames(c.Allowed);
                packageContractSources.Add((c.Name, c.Id, c.Source));
            }

            foreach (ArchitectureProjectMetadataContract c in Document.Contracts.AuditProjectMetadata)
            {
                projectMetadataContractProjects.AddRange(c.Projects.Select(project => (c.Name, c.Id, NormalizeProjectPath(project))));
            }

            foreach (ArchitectureTypePlacementContract c in Document.Contracts.AuditTypePlacement)
            {
                AddLayerNames(c.Id, GetTypePlacementReferencedLayerNames(c));
            }

            foreach (ArchitectureAttributeUsageContract c in Document.Contracts.AuditAttributeUsage)
            {
                AddLayerNames(c.Id, GetAttributeUsageReferencedLayerNames(c));
            }

            foreach (ArchitectureInheritanceContract c in Document.Contracts.AuditInheritance)
            {
                AddLayerNames(c.Id, c.SourceLayers);
            }

            foreach (ArchitectureInterfaceImplementationContract c in Document.Contracts.AuditInterfaceImplementation)
            {
                AddLayerNames(c.Id, GetInterfaceImplementationReferencedLayerNames(c));
            }
        }

        HashSet<string> ruleInputCoveredContractIds = CollectRuleInputCoveredContractIds(strict);

        foreach ((string layerName, HashSet<string> referencingContractIds) in layerReferencingContractIds)
        {
            bool isFullyOwnedByRuleInputCoverage = referencingContractIds.Count > 0
                && referencingContractIds.All(ruleInputCoveredContractIds.Contains);

            if (!Document.Layers.ContainsKey(layerName))
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

            ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(Document, "<configuration>", layerName);

            if (layer.External)
            {
                continue;
            }

            Type[] types = ArchitectureTypeScanner.FindTypesInLayer(Context.TargetAssemblies, layer);

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
            if (!Document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
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

        foreach (string groupName in referencedPackageGroups)
        {
            if (!Document.Packages.TryGetValue(groupName, out ArchitecturePackageGroup? group))
            {
                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    groupName,
                    "unknown package group",
                    new[]
                    {
                        $"Package group '{groupName}' is referenced by a contract but is not declared in packages."
                    })
                {
                    ForbiddenPackageGroup = groupName
                });

                continue;
            }

            if (ArchitecturePackageDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                groupName,
                "invalid package group",
                new[]
                {
                    $"Package group '{groupName}' must declare at least one non-empty package_ids or package_prefixes matcher."
                })
            {
                ForbiddenPackageGroup = groupName
            });
        }

        if (packageContractSources.Count > 0)
        {
            HashSet<string> projectsWithPackageData = new(
                Context.ProjectDiscovery?.DiscoveredProjects.Select(project => project.AssemblyName) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            foreach ((string contractName, string? contractId, string source) in packageContractSources
                         .DistinctBy(entry => (entry.ContractName, entry.ContractId, entry.Source)))
            {
                if (projectsWithPackageData.Contains(source))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contractName,
                    contractId,
                    source,
                    "no package metadata discovered",
                    new[]
                    {
                        $"Contract '{contractName}' declares source '{source}', but no discovered project with that assembly name has package reference metadata available. " +
                        "Package dependency/allow-only contracts require analysis.solution or analysis.projects to be configured so project discovery can parse PackageReference items; " +
                        "without it, this contract will never report a violation."
                    }));
            }
        }

        if (projectMetadataContractProjects.Count > 0)
        {
            HashSet<string> discoveredProjectPaths = new(
                Context.ProjectDiscovery?.DiscoveredProjects.Select(project => NormalizeProjectPath(project.Path))
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach ((string contractName, string? contractId, string projectPath) in projectMetadataContractProjects
                         .DistinctBy(entry => (entry.ContractName, entry.ContractId, entry.ProjectPath)))
            {
                if (discoveredProjectPaths.Contains(projectPath))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contractName,
                    contractId,
                    projectPath,
                    "no project metadata discovered",
                    new[]
                    {
                        $"Contract '{contractName}' targets project '{projectPath}', but project discovery did not expose metadata for that path. " +
                        "Project metadata contracts require analysis.solution or analysis.projects to discover and parse the matching .csproj file."
                    })
                {
                    ProjectMetadataKind = "missing_project"
                });
            }
        }

        return violations;
    }

    private static string NormalizeProjectPath(string path)
    {
        return path.Replace('\\', '/').Trim();
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
            ? Document.Contracts.StrictCoverage
            : Document.Contracts.AuditCoverage;

        return new HashSet<string>(
            coverageContractsForMode
                .Where(c => string.Equals(c.Scope, "rule_input", StringComparison.Ordinal))
                .Where(c => IsContractSelected(c.Id))
                .SelectMany(c => c.ContractIds),
            StringComparer.OrdinalIgnoreCase);
    }
}
