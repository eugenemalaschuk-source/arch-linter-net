using System.Text.RegularExpressions;
using ArchLinterNet.Core.Resolution;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public static class ArchitectureContractLoader
{
    private static readonly Lazy<ArchitectureContractDocument> _document = new(LoadInternal);

    public static ArchitectureContractDocument Load()
    {
        return _document.Value;
    }

    private static ArchitectureContractDocument LoadInternal()
    {
        string repositoryRoot = ArchitectureRepositoryRootLocator.Resolve();
        return LoadFromRepositoryRoot(repositoryRoot);
    }

    public static ArchitectureContractDocument LoadFromRepositoryRoot(string repositoryRoot)
    {
        string contractPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");
        return LoadFromPath(contractPath);
    }

    public static ArchitectureContractDocument LoadFromPath(string contractPath)
    {
        if (!File.Exists(contractPath))
        {
            throw new FileNotFoundException($"Architecture contract file not found: {contractPath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = File.ReadAllText(contractPath);
        ArchitectureContractDocument? document = deserializer.Deserialize<ArchitectureContractDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
        }

        AssignFallbackIds(document);
        ValidateDuplicateIds(document);
        ValidateAcyclicSiblingContracts(document);
        ValidateLayerNamespaces(document);
        ValidateCoverageNamespaces(document);

        return document;
    }

    public static string NormalizeToContractId(string name)
    {
        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace(" -> ", "-to-");
        normalized = Regex.Replace(normalized, @"[^a-z0-9-]", "-");
        normalized = Regex.Replace(normalized, "-{2,}", "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    private static void AssignFallbackIds(ArchitectureContractDocument document)
    {
        foreach (IArchitectureContract contract in GetAllContracts(document))
        {
            if (string.IsNullOrEmpty(contract.Id))
            {
                contract.Id = NormalizeToContractId(contract.Name);
            }
        }
    }

    private static void ValidateDuplicateIds(ArchitectureContractDocument document)
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
            document.Contracts.StrictAsmdef,
            document.Contracts.AuditAsmdef,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictLayerTemplates,
            document.Contracts.AuditLayerTemplates,
            document.Contracts.StrictAcyclicSiblings,
            document.Contracts.AuditAcyclicSiblings,
            document.Contracts.StrictCoverage,
            document.Contracts.AuditCoverage,
        ];

        foreach (var group in groups)
        {
            var duplicates = group
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate contract IDs found: {string.Join(", ", duplicates)}. Each contract ID must be unique within its contract type and mode group.");
            }
        }
    }

    private static void ValidateAcyclicSiblingContracts(ArchitectureContractDocument document)
    {
        foreach (ArchitectureAcyclicSiblingContract contract in document.Contracts.StrictAcyclicSiblings
                     .Concat(document.Contracts.AuditAcyclicSiblings))
        {
            if (contract.Ancestors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Acyclic sibling contract '{contract.Name}' has an empty ancestors list. At least one ancestor namespace is required.");
            }

            for (int i = 0; i < contract.Ancestors.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(contract.Ancestors[i]))
                {
                    throw new InvalidOperationException(
                        $"Acyclic sibling contract '{contract.Name}' has a blank or empty ancestor at index {i}. Each ancestor must be a non-empty namespace prefix.");
                }
            }
        }
    }

    private static void ValidateLayerNamespaces(ArchitectureContractDocument document)
    {
        foreach (KeyValuePair<string, ArchitectureLayer> pair in document.Layers)
        {
            ArchitectureLayer layer = pair.Value;

            if (!string.IsNullOrWhiteSpace(layer.Namespace))
            {
                _ = layer.GlobPattern;
            }
        }
    }

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
                    "independence, protected, external, asmdef, acyclic_sibling, or layer_template contract.");
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
            document.Contracts.StrictAsmdef,
            document.Contracts.AuditAsmdef,
            document.Contracts.StrictIndependence,
            document.Contracts.AuditIndependence,
            document.Contracts.StrictProtected,
            document.Contracts.AuditProtected,
            document.Contracts.StrictExternal,
            document.Contracts.AuditExternal,
            document.Contracts.StrictLayerTemplates,
            document.Contracts.AuditLayerTemplates,
            document.Contracts.StrictAcyclicSiblings,
            document.Contracts.AuditAcyclicSiblings,
        ];

        return new HashSet<string>(
            groups.SelectMany(group => group).Select(c => c.Id).Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }
}
