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
            string layerName = pair.Key;
            ArchitectureLayer layer = pair.Value;

            if (!string.IsNullOrEmpty(layer.Namespace) && layer.Namespace.Contains('*'))
            {
                _ = layer.GlobPattern;
            }
        }
    }

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }
}
