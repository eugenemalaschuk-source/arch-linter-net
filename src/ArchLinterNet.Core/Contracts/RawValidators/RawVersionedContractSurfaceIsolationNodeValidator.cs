using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

internal sealed class RawVersionedContractSurfaceIsolationNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _contractKeys =
        ["id", "name", "surfaces", "source_surface", "forbidden_surfaces", "ignored_violations", "reason"];
    private static readonly string[] _surfaceKeys = ["id", "types_matching"];
    private static readonly string[] _selectorKeys =
        ["name_suffix", "name_prefix", "namespace", "layer", "base_type", "implements_interface", "has_attribute", "role"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        ValidateGroup(document, "strict_versioned_contract_surface_isolation");
        ValidateGroup(document, "audit_versioned_contract_surface_isolation");
    }

    private static void ValidateGroup(ArchitecturePolicyRawDocument document, string groupKey)
    {
        if (!document.TryGetSection(RawYamlNodes.ContractsKey, out YamlMappingNode? contracts)
            || !RawYamlNodes.TryGetChild(contracts, groupKey, out YamlNode? groupNode)) return;
        if (groupNode is not YamlSequenceNode sequence)
            throw new InvalidOperationException($"Contract group '{groupKey}' must be a list of contract objects.");
        for (int i = 0; i < sequence.Children.Count; i++)
        {
            document.Provenance.SetValidationSubject(RawYamlNodes.ContractPath(groupKey, i));
            if (sequence.Children[i] is not YamlMappingNode contract)
                throw new InvalidOperationException($"Contract group '{groupKey}' entry {i} must be an object.");
            ValidateContract(contract);
        }
    }

    private static void ValidateContract(YamlMappingNode contract)
    {
        string name = RawYamlNodes.ContractName(contract);
        RawYamlNodes.ValidateKnownKeys(contract, name, "versioned contract-surface isolation contract", _contractKeys);
        Require(contract, "id", name, "stable contract ID");
        Require(contract, "name", name, "contract name");
        if (!RawYamlNodes.TryGetChild(contract, "surfaces", out YamlNode? surfacesNode)
            || surfacesNode is not YamlSequenceNode surfaces || surfaces.Children.Count == 0)
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' must declare a non-empty 'surfaces' list.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (YamlNode node in surfaces.Children)
        {
            if (node is not YamlMappingNode surface) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' surfaces entries must be objects.");
            RawYamlNodes.ValidateKnownKeys(surface, name, "surface", _surfaceKeys);
            string id = Require(surface, "id", name, "surface ID");
            if (!ids.Add(id)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' declares duplicate surface ID '{id}'.");
            if (!RawYamlNodes.TryGetChild(surface, "types_matching", out YamlNode? selectorNode) || selectorNode is not YamlMappingNode selector)
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' surface '{id}' must declare a 'types_matching' selector object.");
            ValidateSelector(selector, name, $"surface '{id}'.types_matching");
        }
        Require(contract, "source_surface", name, "source surface ID");
        ValidateReferences(contract, name, surfaces, ids);
    }

    private static void ValidateReferences(YamlMappingNode contract, string name, YamlSequenceNode surfaces, HashSet<string> ids)
    {
        string source = Require(contract, "source_surface", name, "source surface ID");
        if (!ids.Contains(source)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' references unknown source surface '{source}'.");
        if (!RawYamlNodes.TryGetChild(contract, "forbidden_surfaces", out YamlNode? forbiddenNode)
            || forbiddenNode is not YamlSequenceNode forbidden || forbidden.Children.Count == 0)
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' must declare a non-empty 'forbidden_surfaces' list.");
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (YamlNode node in forbidden.Children)
        {
            if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' declares a blank forbidden surface reference.");
            string id = scalar.Value!;
            if (!refs.Add(id)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' declares duplicate forbidden surface '{id}'.");
            if (string.Equals(id, source, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' cannot forbid its source surface '{source}'.");
            if (!ids.Contains(id)) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' references unknown forbidden surface '{id}'.");
        }
    }

    private static void ValidateSelector(YamlMappingNode selector, string name, string location)
    {
        RawYamlNodes.ValidateKnownKeys(selector, name, location, _selectorKeys);
        if (selector.Children.Count == 0) throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' declares an empty or unbounded '{location}' selector.");
        foreach ((YamlNode key, YamlNode value) in selector.Children)
            if (key is not YamlScalarNode || value is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' declares a blank or non-scalar selector value at '{location}'.");
    }

    private static string Require(YamlMappingNode parent, string key, string name, string description)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? node) || node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{name}' must declare a non-blank '{key}' ({description}).");
        return scalar.Value!;
    }
}
