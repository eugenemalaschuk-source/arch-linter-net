using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Abstractions;
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
    private const string ConfigurationSource = "<configuration>";

    private ArchitectureCoverageInventory? _cachedCoverageInventory;
    private ArchitectureContractDocument? _cachedCoverageInventoryDocument;

    private readonly List<ArchitectureUnmatchedIgnoredViolation> _unmatchedIgnoredViolations = new();

    private readonly List<ArchitectureBaselineCandidate> _baselineCandidates = new();

    private readonly Dictionary<string, ArchitectureContextualConsumerReference> _registeredContextualConsumers =
        new(StringComparer.Ordinal);

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
        RoleIndex = new ArchitectureRoleIndex(document.Classification, TypeIndex);
        SourceFileFactIndex = new ArchitectureSourceFileFactIndex(
            context.TargetAssemblies, context.RepositoryRoot, document.Analysis.SourceRoots,
            preprocessorSymbols);
        RegisterAllContextualConsumersFromDocument();
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

    private Type[] FindTypesInLayer(ArchitectureLayer layer)
    {
        return TypeIndex.FindTypesInLayer(layer, RoleIndex);
    }

    private bool MatchesLayer(ArchitectureLayer layer, Type type)
    {
        return ArchitectureLayerTypeMatcher.Matches(layer, type, RoleIndex);
    }

    private bool IsInAnyDeclaredLayer(Type type)
    {
        return Document.Layers.Values.Any(layer => MatchesLayer(layer, type));
    }

    private string? ResolveContainingLayer(Type type, IReadOnlySet<string> candidateLayerNames)
    {
        return candidateLayerNames
            .Select(layerName => new
            {
                LayerName = layerName,
                Layer = ArchitectureLayerResolver.ResolveLayer(Document, "type-resolution", layerName)
            })
            .Where(entry => MatchesLayer(entry.Layer, type))
            .Select(entry =>
            {
                bool hasNamespace = !string.IsNullOrWhiteSpace(entry.Layer.Namespace);
                NamespaceGlobPattern? pattern = hasNamespace ? entry.Layer.GlobPattern : null;
                return new
                {
                    entry.LayerName,
                    HasSelector = entry.Layer.Selector != null,
                    HasNamespace = hasNamespace,
                    IsGlob = pattern?.IsGlob ?? false,
                    LiteralCount = pattern?.LiteralCount ?? -1,
                    HasSuffix = !string.IsNullOrEmpty(entry.Layer.NamespaceSuffix),
                    WildcardCount = pattern?.WildcardCount ?? int.MaxValue
                };
            })
            .OrderByDescending(entry => entry.HasSelector)
            .ThenByDescending(entry => entry.HasNamespace)
            .ThenByDescending(entry => entry.HasNamespace && !entry.IsGlob)
            .ThenByDescending(entry => entry.LiteralCount)
            .ThenByDescending(entry => entry.HasSuffix)
            .ThenBy(entry => entry.WildcardCount)
            .ThenBy(entry => entry.LayerName, StringComparer.Ordinal)
            .Select(entry => entry.LayerName)
            .FirstOrDefault();
    }

    public ArchitectureReferenceGraph ReferenceGraph { get; } = new();

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _unmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _baselineCandidates;

    // Coverage-participating consumption recorded by contextual dependency/allow-only contracts.
    // See ArchitectureContextualConsumerReference and design.md Decision 7. Nothing consumes this
    // collection yet — it exists so a future coverage change can query it.
    public IReadOnlyCollection<ArchitectureContextualConsumerReference> RegisteredContextualConsumers
        => _registeredContextualConsumers.Values;

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

        AddMissingAssemblyViolations(violations);
        AddDiscoveryDiagnosticViolations(violations);

        ArchitectureConfigurationReferenceCollector collector = BuildConfigurationReferenceCollector(strict);
        HashSet<string> ruleInputCoveredContractIds = CollectRuleInputCoveredContractIds(strict);

        AddLayerReferenceViolations(violations, collector, ruleInputCoveredContractIds);
        AddExternalDependencyGroupViolations(violations, collector);
        AddPackageGroupViolations(violations, collector);
        AddPackageMetadataViolations(violations, collector);
        AddProjectMetadataViolations(violations, collector);

        return violations;
    }

    private void AddMissingAssemblyViolations(List<ArchitectureViolation> violations)
    {
        foreach (string missingAssembly in Context.MissingAssemblyNames)
        {
            string probeInfo = Context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", Context.AssemblyProbingPaths)}"
                : string.Empty;

            var violation = new ArchitectureViolation(
                ConfigurationSource,
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" });
            int index = Document.Analysis.TargetAssemblies.IndexOf(missingAssembly);
            violations.Add(index < 0
                ? violation
                : Document.Provenance.EnrichAtPath(
                    violation,
                    ArchitecturePolicyProvenancePath.AppendIndex(
                        ArchitecturePolicyProvenancePath.AppendProperty(
                            ArchitecturePolicyProvenancePath.Property("analysis"), "target_assemblies"),
                        index)));
        }
    }

    private void AddDiscoveryDiagnosticViolations(List<ArchitectureViolation> violations)
    {
        foreach (ArchitectureProjectDiscoveryDiagnostic discoveryDiagnostic in Context.DiscoveryDiagnostics)
        {
            violations.Add(new ArchitectureViolation(
                ConfigurationSource,
                null,
                discoveryDiagnostic.Subject,
                discoveryDiagnostic.Kind,
                new[] { discoveryDiagnostic.Message }));
        }
    }

    private ArchitectureConfigurationReferenceCollector BuildConfigurationReferenceCollector(bool strict)
    {
        ArchitectureConfigurationReferenceCollector collector = new();

        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            if (descriptor.ConfigurationContributor is null)
            {
                continue;
            }

            IEnumerable<IArchitectureContract> contracts = strict
                ? descriptor.StrictContracts(Document.Contracts)
                : descriptor.AuditContracts(Document.Contracts);

            foreach (IArchitectureContract contract in contracts)
            {
                descriptor.ConfigurationContributor(this, collector, contract);
            }
        }

        return collector;
    }

    private void AddLayerReferenceViolations(
        List<ArchitectureViolation> violations,
        ArchitectureConfigurationReferenceCollector collector,
        HashSet<string> ruleInputCoveredContractIds)
    {
        foreach ((string layerName, List<IArchitectureContract> referencingContracts) in
                 collector.LayerReferencingContracts)
        {
            HashSet<string> referencingContractIds = referencingContracts
                .Select(contract => contract.Id)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool isFullyOwnedByRuleInputCoverage = referencingContractIds.Count > 0
                && referencingContractIds.All(ruleInputCoveredContractIds.Contains);

            // A dangling layer name referenced exclusively by contracts a rule_input coverage
            // contract tracks defers to that coverage contract's own "unresolved" finding
            // instead of throwing here — otherwise scope: rule_input's unresolved diagnostic
            // would be unreachable through the real validation pipeline, since this resolution
            // happens before any contract or coverage check runs.
            if (!Document.Layers.ContainsKey(layerName) && isFullyOwnedByRuleInputCoverage)
            {
                continue;
            }

            ArchitectureLayer layer;
            try
            {
                layer = ArchitectureLayerResolver.ResolveLayer(Document, ConfigurationSource, layerName);
            }
            catch (InvalidOperationException exception)
            {
                Exception enriched = Document.Provenance.EnrichValidationException(
                    exception,
                    referencingContracts.Cast<object>());
                if (ReferenceEquals(enriched, exception))
                {
                    throw;
                }

                throw enriched;
            }

            if (layer.External)
            {
                continue;
            }

            Type[] types = FindTypesInLayer(layer);

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

                string matchDescription = layer.Selector == null
                    ? $"namespace '{layer.Namespace}'"
                    : $"semantic selector '{ArchitectureLayerResolver.DescribeLayer(layer)}'";

                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    ArchitectureLayerResolver.DescribeLayer(layer),
                    layer.Selector == null ? "empty layer namespace" : "empty layer selector",
                    new[] { $"Layer '{layerName}' {matchDescription} contains no matching types in loaded assemblies." });
                violations.Add(Document.Provenance.EnrichAtPath(
                    violation,
                    ArchitecturePolicyProvenancePath.AppendProperty(
                        ArchitecturePolicyProvenancePath.Property("layers"), layerName)));
            }
        }
    }

    private void AddExternalDependencyGroupViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((string groupName, List<IArchitectureContract> referencingContracts) in
                 collector.ReferencedExternalGroups)
        {
            if (!Document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
            {
                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    groupName,
                    "unknown external dependency group",
                    new[]
                    {
                        $"External dependency group '{groupName}' is referenced by a contract but is not declared in external_dependencies."
                    })
                {
                    Payload = new ExternalDependencyPayload(groupName)
                };
                violations.Add(Document.Provenance.Enrich(
                    violation,
                    referencingContracts.FirstOrDefault(),
                    referencingContracts.Skip(1).Cast<object>()));

                continue;
            }

            if (ArchitectureExternalDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            var invalidGroup = new ArchitectureViolation(
                ConfigurationSource,
                null,
                groupName,
                "invalid external dependency group",
                new[]
                {
                    $"External dependency group '{groupName}' must declare at least one non-empty namespace_prefixes or type_prefixes matcher."
                })
            {
                Payload = new ExternalDependencyPayload(groupName)
            };
            violations.Add(Document.Provenance.EnrichAtPath(
                invalidGroup,
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property("external_dependencies"), groupName)));
        }
    }

    private void AddPackageGroupViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((string groupName, List<IArchitectureContract> referencingContracts) in
                 collector.ReferencedPackageGroups)
        {
            if (!Document.Packages.TryGetValue(groupName, out ArchitecturePackageGroup? group))
            {
                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    groupName,
                    "unknown package group",
                    new[]
                    {
                        $"Package group '{groupName}' is referenced by a contract but is not declared in packages."
                    })
                {
                    Payload = new PackageDependencyPayload(groupName)
                };
                violations.Add(Document.Provenance.Enrich(
                    violation,
                    referencingContracts.FirstOrDefault(),
                    referencingContracts.Skip(1).Cast<object>()));

                continue;
            }

            if (ArchitecturePackageDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            var invalidGroup = new ArchitectureViolation(
                ConfigurationSource,
                null,
                groupName,
                "invalid package group",
                new[]
                {
                    $"Package group '{groupName}' must declare at least one non-empty package_ids or package_prefixes matcher."
                })
            {
                Payload = new PackageDependencyPayload(groupName)
            };
            violations.Add(Document.Provenance.EnrichAtPath(
                invalidGroup,
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property("packages"), groupName)));
        }
    }

    private void AddPackageMetadataViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        if (collector.PackageContractSources.Count == 0)
        {
            return;
        }

        HashSet<string> projectsWithPackageData = new(
            Context.ProjectDiscovery?.DiscoveredProjects.Select(project => project.AssemblyName) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        foreach ((IArchitectureContract contract, string source) in collector.PackageContractSources
                     .DistinctBy(entry => (entry.Contract, entry.Source)))
        {
            if (projectsWithPackageData.Contains(source))
            {
                continue;
            }

            var violation = new ArchitectureViolation(
                contract.Name,
                contract.Id,
                source,
                "no package metadata discovered",
                new[]
                {
                    $"Contract '{contract.Name}' declares source '{source}', but no discovered project with that assembly name has package reference metadata available. " +
                    "Package dependency/allow-only contracts require analysis.solution or analysis.projects to be configured so project discovery can parse PackageReference items; " +
                    "without it, this contract will never report a violation."
                });
            violations.Add(Document.Provenance.Enrich(violation, contract));
        }
    }

    private void AddProjectMetadataViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        if (collector.ProjectMetadataContractProjects.Count == 0)
        {
            return;
        }

        HashSet<string> discoveredProjectPaths = new(
            Context.ProjectDiscovery?.DiscoveredProjects.Select(project => NormalizeProjectPath(project.Path))
            ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach ((IArchitectureContract contract, string projectPath) in collector.ProjectMetadataContractProjects
                     .DistinctBy(entry => (entry.Contract, entry.ProjectPath)))
        {
            if (discoveredProjectPaths.Contains(projectPath))
            {
                continue;
            }

            var violation = new ArchitectureViolation(
                contract.Name,
                contract.Id,
                projectPath,
                "no project metadata discovered",
                new[]
                {
                    $"Contract '{contract.Name}' targets project '{projectPath}', but project discovery did not expose metadata for that path. " +
                    "Project metadata contracts require analysis.solution or analysis.projects to discover and parse the matching .csproj file."
                })
            {
                Payload = new ProjectMetadataPayload(ProjectMetadataKind: "missing_project")
            };
            violations.Add(Document.Provenance.Enrich(violation, contract));
        }
    }

    internal static string NormalizeProjectPath(string path)
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
