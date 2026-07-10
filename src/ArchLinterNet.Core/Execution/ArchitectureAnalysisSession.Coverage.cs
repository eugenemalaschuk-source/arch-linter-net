using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public ArchitectureCoverageSummary? BuildCoverageSummary(ArchitectureCoverageContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return null;
        }

        if (string.Equals(contract.Scope, "rule_input", StringComparison.Ordinal))
        {
            return BuildRuleInputCoverageSummary(contract);
        }

        if (string.Equals(contract.Scope, "assembly", StringComparison.Ordinal))
        {
            return BuildAssemblyCoverageSummary(contract);
        }

        if (string.Equals(contract.Scope, "project", StringComparison.Ordinal))
        {
            return BuildProjectCoverageSummary(contract);
        }

        if (string.Equals(contract.Scope, "dependency_edge", StringComparison.Ordinal))
        {
            return BuildDependencyEdgeCoverageSummary(contract);
        }

        return BuildNamespaceCoverageSummary(contract);
    }

    private ArchitectureCoverageSummary BuildNamespaceCoverageSummary(ArchitectureCoverageContract contract)
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

    private ArchitectureCoverageSummary BuildRuleInputCoverageSummary(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> staleItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> unknownItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();

        foreach (string referencedContractId in contract.ContractIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            ArchitectureCoverageExclusion? matchedExclusion = contract.Exclude
                .FirstOrDefault(exclusion =>
                    !string.IsNullOrWhiteSpace(exclusion.ContractId)
                    && string.Equals(exclusion.ContractId, referencedContractId, StringComparison.OrdinalIgnoreCase));

            if (matchedExclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(referencedContractId, matchedExclusion.Reason));
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            IReadOnlyList<string> referencedLayerNames = GetReferencedLayerNames(descriptor.Contract)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            foreach (string layerName in referencedLayerNames)
            {
                if (!Document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer))
                {
                    unknownItems.Add(new ArchitectureCoverageSummaryEvidenceItem(referencedContractId, layerName));
                    continue;
                }

                bool matchesAnyCode = inventory.Namespaces.Any(entry =>
                    ArchitectureLayerResolver.MatchesNamespace(layer, entry.Namespace));

                if (!matchesAnyCode)
                {
                    staleItems.Add(new ArchitectureCoverageSummaryEvidenceItem(referencedContractId, layerName));
                    continue;
                }

                coveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem($"{referencedContractId}:{layerName}", layerName));
            }
        }

        return new ArchitectureCoverageSummary(
            contract.Name,
            contract.Id,
            contract.Scope,
            new ArchitectureCoverageSummaryCounts(coveredItems.Count, excludedItems.Count, 0, staleItems.Count, unknownItems.Count),
            excludedItems,
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            staleItems,
            unknownItems,
            coveredItems);
    }

    private ArchitectureCoverageSummary BuildAssemblyCoverageSummary(ArchitectureCoverageContract contract)
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

    private ArchitectureCoverageSummary BuildProjectCoverageSummary(ArchitectureCoverageContract contract)
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

    private ArchitectureCoverageSummary BuildDependencyEdgeCoverageSummary(ArchitectureCoverageContract contract)
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
            return CheckDependencyEdgeCoverageContract(contract);
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
            .Where(entry => !executionContext.IsIgnored(entry.Namespace, "uncovered namespace"))
#pragma warning restore S6607
            .Select(entry => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                entry.Namespace,
                "uncovered namespace",
                new[] { entry.RepresentativeType }))
            .ToList();

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        return findings;
    }

    private List<ArchitectureViolation> CheckRuleInputCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);

        HashSet<string> excludedContractIds = new(
            contract.Exclude
                .Where(exclusion => !string.IsNullOrWhiteSpace(exclusion.ContractId))
                .Select(exclusion => exclusion.ContractId),
            StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ArchitectureContractDescriptor> descriptorsById = BuildAllDescriptors()
            .Where(descriptor => !string.IsNullOrEmpty(descriptor.Id))
            .GroupBy(descriptor => descriptor.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureViolation> findings = new();

        foreach (string referencedContractId in contract.ContractIds)
        {
            if (excludedContractIds.Contains(referencedContractId))
            {
                continue;
            }

            if (!descriptorsById.TryGetValue(referencedContractId, out ArchitectureContractDescriptor? descriptor))
            {
                continue;
            }

            AddRuleInputCoverageFindingsForContract(
                contract, referencedContractId, descriptor, inventory, executionContext, findings);
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ThenBy(f => f.ForbiddenReferences.First(), StringComparer.Ordinal)
            .ToList();
    }

    private void AddRuleInputCoverageFindingsForContract(
        ArchitectureCoverageContract contract,
        string referencedContractId,
        ArchitectureContractDescriptor descriptor,
        ArchitectureCoverageInventory inventory,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> findings)
    {
        IReadOnlyList<string> referencedLayerNames = GetReferencedLayerNames(descriptor.Contract)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string layerName in referencedLayerNames)
        {
            if (!Document.Layers.TryGetValue(layerName, out ArchitectureLayer? layer))
            {
                if (!executionContext.IsIgnored(referencedContractId, layerName))
                {
                    findings.Add(new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        referencedContractId,
                        "unresolved",
                        new[] { layerName }));
                }

                continue;
            }

            bool matchesAnyCode = inventory.Namespaces.Any(entry =>
                ArchitectureLayerResolver.MatchesNamespace(layer, entry.Namespace));

            if (!matchesAnyCode && !executionContext.IsIgnored(referencedContractId, layerName))
            {
                findings.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    referencedContractId,
                    "empty-input",
                    new[] { layerName }));
            }
        }
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
            .Where(entry => !executionContext.IsIgnored(entry.Name, "uncovered assembly"))
#pragma warning restore S6607
            .Select(entry => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                entry.Name,
                "uncovered assembly",
                GetAssemblyForbiddenReferences(entry.Assembly)))
            .ToList();

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

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
                if (!executionContext.IsIgnored(project.Path, "unresolved project"))
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

            if (!covered && !executionContext.IsIgnored(project.Path, "uncovered project"))
            {
                findings.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    project.Path,
                    "uncovered project",
                    new[] { project.AssemblyName, GetRepresentativeType(resolvedAssembly) }));
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ToList();
    }

    private List<ArchitectureViolation> CheckDependencyEdgeCoverageContract(ArchitectureCoverageContract contract)
    {
        ArchitectureCoverageInventory inventory = BuildCoverageInventory(Document);
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        List<ArchitectureViolation> findings = new();

        foreach (List<string> pair in contract.Between)
        {
            string sourceLayer = pair[0];
            string targetLayer = pair[1];

            if (contract.Exclude.Any(exclusion => MatchesDependencyEdgeExclusion(exclusion, sourceLayer, targetLayer)))
            {
                continue;
            }

            if (IsLayerPairGoverned(sourceLayer, targetLayer))
            {
                continue;
            }

            foreach (ArchitectureCoverageDependencyEdge edge in GetEdgesForLayerPair(sourceLayer, targetLayer))
            {
                string edgeKey = $"{edge.SourceNamespace} -> {edge.TargetNamespace}";

                if (executionContext.IsIgnored(edgeKey, "uncovered dependency edge"))
                {
                    continue;
                }

                findings.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    edgeKey,
                    "uncovered dependency edge",
                    new[] { GetRepresentativeNamespaceType(inventory, edge.SourceNamespace) }));
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);

        return findings
            .OrderBy(f => f.SourceType, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<ArchitectureCoverageDependencyEdge> GetEdgesForLayerPair(string sourceLayer, string targetLayer)
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

    private static string GetRepresentativeNamespaceType(ArchitectureCoverageInventory inventory, string namespaceName)
    {
        ArchitectureCoverageNamespaceEntry? entry = inventory.Namespaces
            .FirstOrDefault(n => string.Equals(n.Namespace, namespaceName, StringComparison.Ordinal));

        return entry?.RepresentativeType ?? namespaceName;
    }

    private bool IsLayerPairGoverned(string sourceLayer, string targetLayer)
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

    private static bool MatchesDependencyEdgeExclusion(
        ArchitectureCoverageExclusion exclusion, string sourceLayer, string targetLayer)
    {
        return exclusion.Between.Count == 2
               && string.Equals(exclusion.Between[0], sourceLayer, StringComparison.Ordinal)
               && string.Equals(exclusion.Between[1], targetLayer, StringComparison.Ordinal);
    }

}
