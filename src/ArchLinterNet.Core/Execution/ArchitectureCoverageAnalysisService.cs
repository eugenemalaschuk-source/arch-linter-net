using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed partial class ArchitectureCoverageAnalysisService
{
    private readonly ArchitectureAnalysisSession _session;
    private readonly ArchitectureCoverageMatchingService _matching;
    private readonly ArchitectureSemanticCoverageService _semanticCoverageService;
    private readonly ArchitectureDependencyEdgeCoverageService _dependencyEdgeCoverageService;

    public ArchitectureCoverageAnalysisService(ArchitectureAnalysisSession session)
    {
        _session = session;
        _matching = new ArchitectureCoverageMatchingService(session);
        _semanticCoverageService = new ArchitectureSemanticCoverageService(session);
        _dependencyEdgeCoverageService = new ArchitectureDependencyEdgeCoverageService(session, this);
    }

    internal ArchitectureSemanticCoverageService SemanticCoverage => _semanticCoverageService;

    private ArchitectureAnalysisContext Context => _session.Context;
    private ArchitectureContractDocument Document => _session.Document;
    private bool IsContractSelected(string? contractId) => _session.IsContractSelected(contractId);

    private ArchitectureCoverageInventory BuildCoverageInventory(ArchitectureContractDocument document) =>
        _session.BuildCoverageInventory(document);

    private ArchitectureContractExecutionContext CreateExecutionContext(
        IArchitectureContract contract,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations) =>
        _session.CreateExecutionContext(contract, ignoredViolations);

    private List<ArchitectureContractDescriptor> BuildAllDescriptors() => _session.BuildAllDescriptors();

    private Assembly? ResolveProjectAssembly(ArchitectureDiscoveredProject project) =>
        _matching.ResolveProjectAssembly(project);

    private static string GetAssemblyName(Assembly assembly) =>
        ArchitectureCoverageMatchingService.GetAssemblyName(assembly);

    private static string[] GetAssemblyNamespaces(Assembly assembly) =>
        ArchitectureCoverageMatchingService.GetAssemblyNamespaces(assembly);

    private static string GetRepresentativeType(Assembly assembly) =>
        ArchitectureCoverageMatchingService.GetRepresentativeType(assembly);

    private static string[] GetAssemblyForbiddenReferences(Assembly assembly) =>
        ArchitectureCoverageMatchingService.GetAssemblyForbiddenReferences(assembly);

    private static string GetAssemblyEvidence(Assembly assembly) =>
        ArchitectureCoverageMatchingService.GetAssemblyEvidence(assembly);

    private static string GetProjectEvidence(ArchitectureDiscoveredProject project, Assembly resolvedAssembly) =>
        ArchitectureCoverageMatchingService.GetProjectEvidence(project, resolvedAssembly);

    private static bool MatchesAssemblyExclusion(ArchitectureCoverageExclusion exclusion, string assemblyName) =>
        ArchitectureCoverageMatchingService.MatchesAssemblyExclusion(exclusion, assemblyName);

    private static bool MatchesProjectExclusion(ArchitectureCoverageExclusion exclusion, ArchitectureDiscoveredProject project) =>
        ArchitectureCoverageMatchingService.MatchesProjectExclusion(exclusion, project);

    private static bool IsCoveredByDeclaredLayers(ArchitectureCoverageInventory inventory, string namespaceName) =>
        ArchitectureCoverageMatchingService.IsCoveredByDeclaredLayers(inventory, namespaceName);

    private static bool IsCoveredByExpandedTemplates(ArchitectureCoverageInventory inventory, string namespaceName) =>
        ArchitectureCoverageMatchingService.IsCoveredByExpandedTemplates(inventory, namespaceName);

    private static bool TryFindLayerExclusionReasons(
        ArchitectureCoverageInventory inventory,
        string namespaceName,
        out string reason,
        out IReadOnlyList<ArchitecturePolicySourceLocation> policyLocations) =>
        ArchitectureCoverageMatchingService.TryFindLayerExclusionReasons(
            inventory, namespaceName, out reason, out policyLocations);

    private static bool MatchesNamespaceRoot(ArchitectureCoverageRoot root, string namespaceName) =>
        ArchitectureCoverageMatchingService.MatchesNamespaceRoot(root, namespaceName);

    private static bool MatchesNamespaceExclusion(ArchitectureCoverageExclusion exclusion, string namespaceName) =>
        ArchitectureCoverageMatchingService.MatchesNamespaceExclusion(exclusion, namespaceName);

    internal ArchitectureCoverageSummary BuildNamespaceSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();

        foreach (ArchitectureCoverageNamespaceEntry entry in inventory.Namespaces
                     .Where(entry => contract.Roots.Any(root => MatchesNamespaceRoot(root, entry.Namespace)))
                     .OrderBy(entry => entry.Namespace, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesNamespaceExclusion(exclusion, entry.Namespace));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(entry.Namespace, matchedExclusion.Reason));
                continue;
            }

            if (IsCoveredByDeclaredLayers(inventory, entry.Namespace) || IsCoveredByExpandedTemplates(inventory, entry.Namespace))
            {
                coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(entry.Namespace, entry.RepresentativeType));
                continue;
            }

            if (TryFindLayerExclusionReasons(inventory, entry.Namespace, out string exclusionReason, out var exclusionLocations))
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(entry.Namespace, exclusionReason)
                {
                    PolicyLocation = exclusionLocations.Count > 0 ? exclusionLocations[0] : null,
                    RelatedPolicyLocations = exclusionLocations.Count > 1 ? exclusionLocations.Skip(1).ToArray() : Array.Empty<ArchitecturePolicySourceLocation>()
                });
                continue;
            }

            uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(entry.Namespace, entry.RepresentativeType));
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, uncoveredItems.Count, 0, 0),
            excludedItems,
            uncoveredItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            coveredItems);
    }

    internal ArchitectureCoverageSummary BuildAssemblySummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();

        foreach (Assembly assembly in Context.TargetAssemblies
                     .OrderBy(GetAssemblyName, StringComparer.Ordinal))
        {
            string assemblyName = GetAssemblyName(assembly);

            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesAssemblyExclusion(exclusion, assemblyName));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(assemblyName, matchedExclusion.Reason));
                continue;
            }

            string[] assemblyNamespaces = GetAssemblyNamespaces(assembly);

            if (assemblyNamespaces.Any(ns => IsCoveredByDeclaredLayers(inventory, ns) || IsCoveredByExpandedTemplates(inventory, ns)))
            {
                coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(assemblyName, GetAssemblyEvidence(assembly)));
                continue;
            }

            uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(assemblyName, GetAssemblyEvidence(assembly)));
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, uncoveredItems.Count, 0, 0),
            excludedItems,
            uncoveredItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            coveredItems);
    }

    internal ArchitectureCoverageSummary BuildProjectSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> unknownItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();

        foreach (ArchitectureDiscoveredProject project in discoveredProjects
                     .OrderBy(project => project.Path, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesProjectExclusion(exclusion, project));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(project.Path, matchedExclusion.Reason));
                continue;
            }

            Assembly? resolvedAssembly = ResolveProjectAssembly(project);

            if (resolvedAssembly == null)
            {
                unknownItems.Add(new ArchitectureCoverageSummaryEvidenceItem(project.Path, project.AssemblyName));
                continue;
            }

            string[] assemblyNamespaces = GetAssemblyNamespaces(resolvedAssembly);

            if (assemblyNamespaces.Any(ns => IsCoveredByDeclaredLayers(inventory, ns) || IsCoveredByExpandedTemplates(inventory, ns)))
            {
                coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    project.Path, GetProjectEvidence(project, resolvedAssembly)));
                continue;
            }

            uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                project.Path, GetProjectEvidence(project, resolvedAssembly)));
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, uncoveredItems.Count, 0, unknownItems.Count),
            excludedItems,
            uncoveredItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            unknownItems,
            coveredItems);
    }

    internal ArchitectureCoverageSummary BuildDependencyEdgeSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();

        foreach (List<string> pair in contract.Between)
        {
            string sourceLayer = pair[0];
            string targetLayer = pair[1];

            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion => MatchesDependencyEdgeExclusion(exclusion, sourceLayer, targetLayer));

            bool isGoverned = IsLayerPairGoverned(sourceLayer, targetLayer);

            foreach (ArchitectureCoverageDependencyEdge edge in GetEdgesForLayerPair(sourceLayer, targetLayer))
            {
                if (matchedExclusion != null)
                {
                    excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(
                        $"{edge.SourceNamespace} -> {edge.TargetNamespace}", matchedExclusion.Reason));
                    continue;
                }

                if (isGoverned)
                {
                    coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                        $"{edge.SourceNamespace} -> {edge.TargetNamespace}", GetRepresentativeNamespaceType(inventory, edge.SourceNamespace)));
                    continue;
                }

                uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    $"{edge.SourceNamespace} -> {edge.TargetNamespace}", GetRepresentativeNamespaceType(inventory, edge.SourceNamespace)));
            }
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, uncoveredItems.Count, 0, 0),
            excludedItems,
            uncoveredItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            coveredItems);
    }

    public List<ArchitectureViolation> CheckCoverageContract(ArchitectureCoverageContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        if (string.Equals(contract.Scope, "rule_input", StringComparison.Ordinal))
        {
            return CheckRuleInputCoverageContract(contract);
        }

        if (string.Equals(contract.Scope, "assembly", StringComparison.Ordinal))
        {
            return CheckAssemblyCoverageContract(contract);
        }

        if (string.Equals(contract.Scope, "project", StringComparison.Ordinal))
        {
            return CheckProjectCoverageContract(contract);
        }

        if (string.Equals(contract.Scope, "dependency_edge", StringComparison.Ordinal))
        {
            return _dependencyEdgeCoverageService.Check(contract);
        }

        if (string.Equals(contract.Scope, "semantic_role", StringComparison.Ordinal))
        {
            return _semanticCoverageService.Check(contract);
        }

        if (!string.Equals(contract.Scope, "namespace", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Coverage contract '{contract.Name}' declares unsupported scope '{contract.Scope}'. " +
                "Only scopes 'namespace', 'rule_input', 'project', 'assembly', and 'dependency_edge' are implemented right now.");
        }

        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureViolation> findings = inventory.Namespaces
            .Where(entry => contract.Roots.Any(root => MatchesNamespaceRoot(root, entry.Namespace)))
            .Where(entry => !contract.Exclude.Any(exclusion => MatchesNamespaceExclusion(exclusion, entry.Namespace)))
            .Where(entry => !IsCoveredByDeclaredLayers(inventory, entry.Namespace))
            .Where(entry => !IsCoveredByExpandedTemplates(inventory, entry.Namespace))
#pragma warning disable S6607 // IsIgnored has an ordered side effect (baseline-candidate
            // collection), so it must run in this OrderBy-then-Where sequence, not before it.
            .OrderBy(entry => entry.Namespace, StringComparer.Ordinal)
            .Where(entry => !executionContext.IsIgnored(
                entry.Namespace,
                "uncovered namespace",
                targetType: entry.Namespace,
                targetMember: "uncovered namespace"))
#pragma warning restore S6607
            .Select(entry => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                entry.Namespace,
                "uncovered namespace",
                new[] { entry.RepresentativeType }))
            .ToList();

        _session.CollectUnmatchedIgnores(executionContext);

        return findings;
    }

    // Source-set expansion derives per-instance contract ids ("<authored-id>/<source>"), so a
    // coverage contract that references the authored id an author actually wrote must resolve to
    // every instance it produced. Contracts that were never expanded resolve to themselves.
    private IEnumerable<(string AuthoredId, string ResolvedId)> ResolveReferencedContractIds(
        ArchitectureCoverageContract contract)
    {
        foreach (string referencedContractId in contract.ContractIds)
        {
            IReadOnlyList<string> instanceIds = Document.SourceExpansion.InstanceIdsFor(referencedContractId);

            if (instanceIds.Count == 0)
            {
                yield return (referencedContractId, referencedContractId);
                continue;
            }

            foreach (string instanceId in instanceIds)
            {
                yield return (referencedContractId, instanceId);
            }
        }
    }

    private static bool MatchesExcludedContractId(
        ArchitectureCoverageExclusion exclusion,
        string authoredContractId,
        string resolvedContractId)
    {
        return !string.IsNullOrWhiteSpace(exclusion.ContractId)
            && (string.Equals(exclusion.ContractId, resolvedContractId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(exclusion.ContractId, authoredContractId, StringComparison.OrdinalIgnoreCase));
    }

    private List<ArchitectureViolation> CheckAssemblyCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureViolation> findings = Context.TargetAssemblies
            .Select(assembly => (Assembly: assembly, Name: GetAssemblyName(assembly)))
            .Where(entry => !contract.Exclude.Any(exclusion => MatchesAssemblyExclusion(exclusion, entry.Name)))
            .Where(entry => !GetAssemblyNamespaces(entry.Assembly)
                .Any(ns => IsCoveredByDeclaredLayers(inventory, ns) || IsCoveredByExpandedTemplates(inventory, ns)))
#pragma warning disable S6607 // IsIgnored has an ordered side effect (baseline-candidate
            // collection), so it must run in this OrderBy-then-Where sequence, not before it.
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Where(entry => !executionContext.IsIgnored(
                entry.Name,
                "uncovered assembly",
                sourceAssembly: entry.Name,
                targetType: entry.Name,
                targetMember: "uncovered assembly"))
#pragma warning restore S6607
            .Select(entry => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                entry.Name,
                "uncovered assembly",
                GetAssemblyForbiddenReferences(entry.Assembly)))
            .ToList();

        _session.CollectUnmatchedIgnores(executionContext);

        return findings;
    }

    private List<ArchitectureViolation> CheckProjectCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        List<ArchitectureViolation> findings = new();

        foreach (ArchitectureDiscoveredProject project in discoveredProjects
                     .OrderBy(project => project.Path, StringComparer.Ordinal))
        {
            if (contract.Exclude.Any(exclusion => MatchesProjectExclusion(exclusion, project)))
            {
                continue;
            }

            Assembly? resolvedAssembly = ResolveProjectAssembly(project);

            if (resolvedAssembly == null)
            {
                if (!executionContext.IsIgnored(
                        project.Path,
                        "unresolved project",
                        sourceAssembly: project.AssemblyName,
                        targetType: project.AssemblyName,
                        targetMember: "unresolved project"))
                {
                    findings.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        project.Path,
                        "unresolved project",
                        new[] { project.AssemblyName }));
                }

                continue;
            }

            bool covered = GetAssemblyNamespaces(resolvedAssembly)
                .Any(ns => IsCoveredByDeclaredLayers(inventory, ns) || IsCoveredByExpandedTemplates(inventory, ns));

            if (!covered && !executionContext.IsIgnored(
                    project.Path,
                    "uncovered project",
                    sourceAssembly: project.AssemblyName,
                    targetType: project.AssemblyName,
                    targetMember: "uncovered project"))
            {
                findings.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    project.Path,
                    "uncovered project",
                    new[] { project.AssemblyName, GetRepresentativeType(resolvedAssembly) }));
            }
        }

        _session.CollectUnmatchedIgnores(executionContext);

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ToList();
    }

    internal IEnumerable<ArchitectureCoverageDependencyEdge> GetEdgesForLayerPair(string sourceLayer, string targetLayer)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        return inventory.DependencyEdges.Where(edge =>
            NamespaceMatchesLayer(edge.SourceNamespace, sourceLayer)
            && NamespaceMatchesLayer(edge.TargetNamespace, targetLayer));
    }

    private bool NamespaceMatchesLayer(string namespaceName, string layerName)
    {
        return Document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer)
               && ArchitectureLayerResolver.MatchesNamespace(layer, namespaceName);
    }

    internal static string GetRepresentativeNamespaceType(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        ArchitectureCoverageNamespaceEntry? entry = inventory.Namespaces
            .FirstOrDefault(n => string.Equals(n.Namespace, namespaceName, StringComparison.Ordinal));

        return entry?.RepresentativeType ?? namespaceName;
    }

    internal bool IsLayerPairGoverned(string sourceLayer, string targetLayer)
    {
        bool governedByDependencyContract = Document.Contracts.Strict
            .Concat(Document.Contracts.Audit)
            .Any(dependency =>
                string.Equals(dependency.Source, sourceLayer, StringComparison.Ordinal)
                && dependency.Forbidden.Contains(targetLayer, StringComparer.Ordinal));

        if (governedByDependencyContract)
        {
            return true;
        }

        bool governedByLayerContract = Document.Contracts.StrictLayers
            .Concat(Document.Contracts.AuditLayers)
            .Any(layer => layer.Layers.Contains(sourceLayer, StringComparer.Ordinal)
                          && layer.Layers.Contains(targetLayer, StringComparer.Ordinal));

        if (governedByLayerContract)
        {
            return true;
        }

        bool governedByIndependenceContract = Document.Contracts.StrictIndependence
            .Concat(Document.Contracts.AuditIndependence)
            .Any(independence => independence.Layers.Contains(sourceLayer, StringComparer.Ordinal)
                                  && independence.Layers.Contains(targetLayer, StringComparer.Ordinal));

        if (governedByIndependenceContract)
        {
            return true;
        }

        // An allow-only contract governs the entire outbound surface of its source layer —
        // every reference out of that layer is either explicitly allowed or a violation —
        // so it governs (A, B) regardless of whether B is itself in the allowed list.
        bool governedByAllowOnlyContract = Document.Contracts.StrictAllowOnly
            .Concat(Document.Contracts.AuditAllowOnly)
            .Any(allowOnly => string.Equals(allowOnly.Source, sourceLayer, StringComparison.Ordinal));

        if (governedByAllowOnlyContract)
        {
            return true;
        }

        // A protected contract governs every reference into its protected layer — allowed
        // importers are exempted by the contract itself, non-allowed importers are violations —
        // so it governs (A, B) whenever B is protected, regardless of A's importer status.
        bool governedByProtectedContract = Document.Contracts.StrictProtected
            .Concat(Document.Contracts.AuditProtected)
            .Any(protectedContract => protectedContract.Protected.Contains(targetLayer, StringComparer.Ordinal));

        if (governedByProtectedContract)
        {
            return true;
        }

        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        return inventory.ExpandedLayerTemplates.Any(template =>
            template.Layers.Any(ns => NamespaceMatchesLayer(ns, sourceLayer))
            && template.Layers.Any(ns => NamespaceMatchesLayer(ns, targetLayer)));
    }

    internal static bool MatchesDependencyEdgeExclusion(
        ArchitectureCoverageExclusion exclusion, string sourceLayer, string targetLayer)
    {
        return exclusion.Between.Count == 2
               && string.Equals(exclusion.Between[0], sourceLayer, StringComparison.Ordinal)
               && string.Equals(exclusion.Between[1], targetLayer, StringComparison.Ordinal);
    }

}
