namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitecturePolicyDocumentLoader
{
    private static void ValidateCoverageNamespaces(ArchitectureContractDocument document)
    {
        foreach (ArchitectureCoverageContract contract in document.Contracts.StrictCoverage
                     .Concat(document.Contracts.AuditCoverage))
        {
            if (string.Equals(contract.Scope, "rule_input", StringComparison.Ordinal))
            {
                ValidateRuleInputCoverageContract(document, contract);
                continue;
            }

            if (string.Equals(contract.Scope, "project", StringComparison.Ordinal))
            {
                ValidateProjectOrAssemblyCoverageContract(document, contract, "Project");
                continue;
            }

            if (string.Equals(contract.Scope, "assembly", StringComparison.Ordinal))
            {
                ValidateProjectOrAssemblyCoverageContract(document, contract, "Assembly");
                continue;
            }

            if (string.Equals(contract.Scope, "dependency_edge", StringComparison.Ordinal))
            {
                ValidateDependencyEdgeCoverageContract(document, contract);
                continue;
            }

            if (!string.Equals(contract.Scope, "namespace", StringComparison.Ordinal))
            {
                continue;
            }

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

        foreach (string referencedContractId in contract.ContractIds)
        {
            if (!knownContractIds.Contains(referencedContractId))
            {
                throw new InvalidOperationException(
                    $"Rule-input coverage contract '{contract.Name}' references unknown contract ID '{referencedContractId}' " +
                    "in 'contract_ids'. The ID must match a declared dependency, layer, allow_only, cycle, method_body, " +
                    "independence, protected, or external contract. asmdef, acyclic_sibling, and layer_template contracts " +
                    "are not valid for scope 'rule_input'.");
            }
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

        for (int i = 0; i < contract.Between.Count; i++)
        {
            List<string> pair = contract.Between[i];

            if (pair.Count != 2 || pair.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Dependency-edge coverage contract '{contract.Name}' has a 'between' entry at index {i} that is not a pair of two non-empty declared layer names.");
            }

            foreach (string layerName in pair)
            {
                if (!document.Layers.ContainsKey(layerName))
                {
                    throw new InvalidOperationException(
                        $"Dependency-edge coverage contract '{contract.Name}' has a 'between' entry at index {i} referencing undeclared layer '{layerName}'.");
                }
            }
        }

        HashSet<string> declaredPairs = new(
            contract.Between.Select(pair => $"{pair[0]}→{pair[1]}"),
            StringComparer.Ordinal);

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

            foreach (string layerName in exclusion.Between)
            {
                if (!document.Layers.ContainsKey(layerName))
                {
                    throw new InvalidOperationException(
                        $"Dependency-edge coverage contract '{contract.Name}' has an exclusion at index {i} referencing undeclared layer '{layerName}'.");
                }
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
}
