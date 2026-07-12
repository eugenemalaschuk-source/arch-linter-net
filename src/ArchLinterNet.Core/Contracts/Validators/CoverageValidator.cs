using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class CoverageValidator : IArchitecturePolicyDocumentValidator
{
    private static readonly string[] _implementedCoverageScopes =
        { "namespace", "rule_input", "project", "assembly", "dependency_edge", "semantic_role" };

    public void Validate(ArchitectureContractDocument document)
    {
        ValidateCoverageNamespaces(document);
        ValidateImplementedCoverageScopes(document);
    }

    private static void ValidateCoverageNamespaces(ArchitectureContractDocument document)
    {
        foreach (ArchitectureCoverageContract contract in document.Contracts.StrictCoverage
                     .Concat(document.Contracts.AuditCoverage))
        {
            switch (contract.Scope)
            {
                case "rule_input":
                    ValidateRuleInputCoverageContract(document, contract);
                    continue;
                case "project":
                    ValidateProjectOrAssemblyCoverageContract(document, contract, "Project");
                    continue;
                case "assembly":
                    ValidateProjectOrAssemblyCoverageContract(document, contract, "Assembly");
                    continue;
                case "dependency_edge":
                    ValidateDependencyEdgeCoverageContract(document, contract);
                    continue;
                case "namespace":
                    ValidateNamespaceCoverageContract(contract);
                    continue;
                case "semantic_role":
                    ValidateSemanticRoleCoverageContract(contract);
                    continue;
                default:
                    continue;
            }
        }
    }

    private static void ValidateSemanticRoleCoverageContract(ArchitectureCoverageContract contract)
    {
        if (contract.Between.Count > 0 || contract.ContractIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Semantic-role coverage contract '{contract.Name}' cannot declare 'between' or 'contract_ids'.");
        }

        for (int i = 0; i < contract.Exclude.Count; i++)
        {
            ArchitectureCoverageExclusion exclusion = contract.Exclude[i];
            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException(
                    $"Semantic-role coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty reason.");
            }

            if (string.IsNullOrWhiteSpace(exclusion.Role))
            {
                throw new InvalidOperationException(
                    $"Semantic-role coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty role matcher.");
            }

            if (!string.IsNullOrWhiteSpace(exclusion.Namespace)
                || !string.IsNullOrWhiteSpace(exclusion.NamespaceSuffix)
                || !string.IsNullOrWhiteSpace(exclusion.Project)
                || !string.IsNullOrWhiteSpace(exclusion.Assembly)
                || !string.IsNullOrWhiteSpace(exclusion.ContractId)
                || exclusion.Between.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Semantic-role coverage contract '{contract.Name}' has an exclusion at index {i} using a non-semantic matcher.");
            }
        }
    }

    private static void ValidateNamespaceCoverageContract(ArchitectureCoverageContract contract)
    {
        if (contract.Roots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Namespace coverage contract '{contract.Name}' must declare at least one root with a non-empty namespace.");
        }

        if (contract.Between.Count > 0)
        {
            throw new InvalidOperationException(
                $"Namespace coverage contract '{contract.Name}' cannot declare 'between'. That field is only valid for scope 'dependency_edge'.");
        }

        if (contract.ContractIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Namespace coverage contract '{contract.Name}' cannot declare 'contract_ids'. That field is only valid for scope 'rule_input'.");
        }

        ValidateNamespaceCoverageRoots(contract);
        ValidateNamespaceCoverageExclusions(contract);
    }

    private static void ValidateNamespaceCoverageRoots(ArchitectureCoverageContract contract)
    {
        for (int i = 0; i < contract.Roots.Count; i++)
        {
            ArchitectureCoverageRoot root = contract.Roots[i];

            if (string.IsNullOrWhiteSpace(root.Namespace))
            {
                throw new InvalidOperationException(
                    $"Namespace coverage contract '{contract.Name}' has a root at index {i} without a non-empty namespace. Namespace coverage roots must use the layer namespace matcher shape.");
            }

            if (root.Include.Count > 0 || root.Exclude.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Namespace coverage contract '{contract.Name}' has a root at index {i} using include/exclude discovery fields. Namespace coverage roots must use only namespace and optional namespace_suffix.");
            }

            _ = new ArchitectureLayer
            {
                Namespace = root.Namespace,
                NamespaceSuffix = root.NamespaceSuffix
            }.GlobPattern;
        }
    }

    private static void ValidateNamespaceCoverageExclusions(ArchitectureCoverageContract contract)
    {
        for (int i = 0; i < contract.Exclude.Count; i++)
        {
            ArchitectureCoverageExclusion exclusion = contract.Exclude[i];

            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException(
                    $"Namespace coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty reason.");
            }

            if (!string.IsNullOrWhiteSpace(exclusion.Project) || !string.IsNullOrWhiteSpace(exclusion.Assembly))
            {
                throw new InvalidOperationException(
                    $"Namespace coverage contract '{contract.Name}' has an exclusion at index {i} using project/assembly fields. Namespace coverage exclusions must use namespace and/or namespace_suffix only.");
            }

            if (string.IsNullOrWhiteSpace(exclusion.Namespace) && string.IsNullOrWhiteSpace(exclusion.NamespaceSuffix))
            {
                throw new InvalidOperationException(
                    $"Namespace coverage contract '{contract.Name}' has an exclusion at index {i} without a namespace or namespace_suffix matcher.");
            }

            if (!string.IsNullOrWhiteSpace(exclusion.Namespace))
            {
                _ = new ArchitectureLayer
                {
                    Namespace = exclusion.Namespace,
                    NamespaceSuffix = exclusion.NamespaceSuffix
                }.GlobPattern;
            }
        }
    }

    private static void ValidateRuleInputCoverageContract(
        ArchitectureContractDocument document, ArchitectureCoverageContract contract)
    {
        if (contract.ContractIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Rule-input coverage contract '{contract.Name}' must declare at least one entry in 'contract_ids'.");
        }

        if (contract.Roots.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rule-input coverage contract '{contract.Name}' cannot declare 'roots'. That field is only valid for scope 'namespace'.");
        }

        if (contract.Between.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rule-input coverage contract '{contract.Name}' cannot declare 'between'. That field is only valid for scope 'dependency_edge'.");
        }

        HashSet<string> knownContractIds = CollectLayerBearingContractIds(document);

        string? unknownContractId = contract.ContractIds.FirstOrDefault(id => !knownContractIds.Contains(id));
        if (unknownContractId != null)
        {
            throw new InvalidOperationException(
                $"Rule-input coverage contract '{contract.Name}' references unknown contract ID '{unknownContractId}' " +
                "in 'contract_ids'. The ID must match a declared dependency, layer, allow_only, cycle, method_body, " +
                "independence, protected, or external contract. asmdef, acyclic_sibling, and layer_template contracts " +
                "are not valid for scope 'rule_input'.");
        }

        for (int i = 0; i < contract.Exclude.Count; i++)
        {
            ArchitectureCoverageExclusion exclusion = contract.Exclude[i];

            if (string.IsNullOrWhiteSpace(exclusion.ContractId))
            {
                throw new InvalidOperationException(
                    $"Rule-input coverage contract '{contract.Name}' has an exclusion at index {i} without a " +
                    "'contract_id' matcher. Rule-input coverage exclusions must declare 'contract_id'.");
            }

            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException(
                    $"Rule-input coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty reason.");
            }
        }
    }

    private static void ValidateDependencyEdgeCoverageContract(
        ArchitectureContractDocument document, ArchitectureCoverageContract contract)
    {
        if (contract.Between.Count == 0)
        {
            throw new InvalidOperationException(
                $"Dependency-edge coverage contract '{contract.Name}' must declare at least one pair in 'between'.");
        }

        if (contract.Roots.Count > 0)
        {
            throw new InvalidOperationException(
                $"Dependency-edge coverage contract '{contract.Name}' cannot declare 'roots'. That field is only valid for scope 'namespace', 'project', or 'assembly'.");
        }

        if (contract.ContractIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Dependency-edge coverage contract '{contract.Name}' cannot declare 'contract_ids'. That field is only valid for scope 'rule_input'.");
        }

        ValidateDependencyEdgeBetweenPairs(document, contract);

        HashSet<string> declaredPairs = new(
            contract.Between.Select(pair => $"{pair[0]}→{pair[1]}"),
            StringComparer.Ordinal);

        ValidateDependencyEdgeExclusions(document, contract, declaredPairs);
    }

    private static void ValidateDependencyEdgeBetweenPairs(
        ArchitectureContractDocument document, ArchitectureCoverageContract contract)
    {
        for (int i = 0; i < contract.Between.Count; i++)
        {
            List<string> pair = contract.Between[i];

            if (pair.Count != 2 || pair.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has a 'between' entry at index {i} that is not a pair of two non-empty declared layer names.");
            }

            string? missingLayer = pair.FirstOrDefault(layerName => !document.Layers.ContainsKey(layerName));
            if (missingLayer != null)
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has a 'between' entry at index {i} referencing undeclared layer '{missingLayer}'.");
            }
        }
    }

    private static void ValidateDependencyEdgeExclusions(
        ArchitectureContractDocument document, ArchitectureCoverageContract contract, HashSet<string> declaredPairs)
    {
        for (int i = 0; i < contract.Exclude.Count; i++)
        {
            ArchitectureCoverageExclusion exclusion = contract.Exclude[i];

            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty reason.");
            }

            if (exclusion.Between.Count != 2 || exclusion.Between.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} without a 'between' pair of two non-empty declared layer names. Dependency-edge coverage exclusions must declare 'between'.");
            }

            if (!string.IsNullOrWhiteSpace(exclusion.Namespace)
                || !string.IsNullOrWhiteSpace(exclusion.NamespaceSuffix)
                || !string.IsNullOrWhiteSpace(exclusion.Project)
                || !string.IsNullOrWhiteSpace(exclusion.Assembly)
                || !string.IsNullOrWhiteSpace(exclusion.ContractId))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} using namespace/namespace_suffix/project/assembly/contract_id fields. " +
                    "Dependency-edge coverage exclusions must use 'between' only — those other matchers belong to other coverage scopes and an exclusion always suppresses the whole declared pair regardless of any other field.");
            }

            string? missingLayer = exclusion.Between.FirstOrDefault(layerName => !document.Layers.ContainsKey(layerName));
            if (missingLayer != null)
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} referencing undeclared layer '{missingLayer}'.");
            }

            string excludedPairKey = $"{exclusion.Between[0]}→{exclusion.Between[1]}";
            if (!declaredPairs.Contains(excludedPairKey))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} for pair " +
                    $"[{exclusion.Between[0]}, {exclusion.Between[1]}] that is not declared in this contract's own 'between' list. " +
                    "An exclusion can only narrow a pair this contract already classifies.");
            }
        }
    }

    private static void ValidateProjectOrAssemblyCoverageContract(
        ArchitectureContractDocument document, ArchitectureCoverageContract contract, string scopeLabel)
    {
        bool isProjectScope = string.Equals(scopeLabel, "Project", StringComparison.Ordinal);

        if (isProjectScope &&
            string.IsNullOrWhiteSpace(document.Analysis.Solution) &&
            document.Analysis.Projects.Count == 0)
        {
            throw new InvalidOperationException(
                $"Project coverage contract '{contract.Name}' requires 'analysis.solution' or 'analysis.projects' " +
                "to be set, since discovered projects are the units this scope classifies.");
        }

        if (contract.Roots.Count > 0)
        {
            throw new InvalidOperationException(
                $"{scopeLabel} coverage contract '{contract.Name}' cannot declare 'roots'. That field is only valid for scope 'namespace'.");
        }

        if (contract.Between.Count > 0)
        {
            throw new InvalidOperationException(
                $"{scopeLabel} coverage contract '{contract.Name}' cannot declare 'between'. That field is only valid for scope 'dependency_edge'.");
        }

        if (contract.ContractIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"{scopeLabel} coverage contract '{contract.Name}' cannot declare 'contract_ids'. That field is only valid for scope 'rule_input'.");
        }

        string matcherField = isProjectScope ? "project" : "assembly";

        for (int i = 0; i < contract.Exclude.Count; i++)
        {
            ArchitectureCoverageExclusion exclusion = contract.Exclude[i];

            if (string.IsNullOrWhiteSpace(exclusion.Reason))
            {
                throw new InvalidOperationException(
                    $"{scopeLabel} coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty reason.");
            }

            string matcherValue = isProjectScope ? exclusion.Project : exclusion.Assembly;

            if (string.IsNullOrWhiteSpace(matcherValue))
            {
                throw new InvalidOperationException(
                    $"{scopeLabel} coverage contract '{contract.Name}' has an exclusion at index {i} without a non-empty " +
                    $"'{matcherField}' matcher. {scopeLabel} coverage exclusions must declare '{matcherField}'.");
            }
        }
    }

    private static void ValidateImplementedCoverageScopes(ArchitectureContractDocument document)
    {
        List<ArchitectureCoverageContract> unsupported = document.Contracts.StrictCoverage
            .Concat(document.Contracts.AuditCoverage)
            .Where(contract => !_implementedCoverageScopes.Contains(contract.Scope, StringComparer.Ordinal))
            .ToList();

        if (unsupported.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", unsupported.Select(contract => $"{contract.Name} ({contract.Scope})"));
        throw new InvalidOperationException(
            "Only coverage contracts with scope 'namespace', 'rule_input', 'project', 'assembly', or 'dependency_edge' " +
            $"are implemented right now; 'semantic_role' is also implemented. Unsupported coverage contract scopes: {details}.");
    }

    // Limited to the contract families ArchitectureContractRunner's GetReferencedLayerNames
    // actually maps to document.Layers keys. Asmdef (source_assemblies, not a layer namespace),
    // acyclic_sibling (ancestors are namespace prefixes, not layer keys), and layer_template are
    // intentionally excluded: layer_template's expanded ArchitectureLayerContract instances carry
    // synthetic IDs ("<template>/<container>") distinct from the authored template ID, and their
    // Layers entries are concrete namespaces rather than document.Layers keys, so neither the ID
    // nor the field values resolve the way rule-input coverage expects. Referencing one of these
    // families is therefore rejected below as an unknown contract ID rather than silently
    // producing zero findings.
    private static HashSet<string> CollectLayerBearingContractIds(ArchitectureContractDocument document)
    {
        IEnumerable<IArchitectureContract>[] groups =
        [
            document.Contracts.Strict,
            document.Contracts.Audit,
            document.Contracts.StrictLayers,
            document.Contracts.AuditLayers,
            document.Contracts.StrictAllowOnly,
            document.Contracts.AuditAllowOnly,
            document.Contracts.StrictCycles,
            document.Contracts.AuditCycles,
            document.Contracts.StrictMethodBody,
            document.Contracts.AuditMethodBody,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictExternalAllowOnly,
            document.Contracts.AuditExternalAllowOnly,
            document.Contracts.StrictTypePlacement,
            document.Contracts.AuditTypePlacement,
            document.Contracts.StrictAttributeUsage,
            document.Contracts.AuditAttributeUsage,
            document.Contracts.StrictInheritance,
            document.Contracts.AuditInheritance,
            document.Contracts.StrictInterfaceImplementation,
            document.Contracts.AuditInterfaceImplementation,
            document.Contracts.StrictComposition,
            document.Contracts.AuditComposition,
        ];

        return new HashSet<string>(
            groups.SelectMany(group => group).Select(c => c.Id).Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);
    }
}
