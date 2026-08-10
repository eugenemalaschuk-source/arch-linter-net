using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Raw-YAML key validation for layer template contracts. Mirrors
// RawLayoutConventionNodeValidator's rationale: IgnoreUnmatchedProperties() would otherwise silently
// drop a typo'd key (e.g. "exclude_container" for "exclude_containers") for a monolithic
// (non-imported) policy - the composed-policy path catches this via
// schema/dependencies.arch.schema.json's additionalProperties: false, but that JSON-schema pass never
// runs for a monolithic policy.
internal sealed class RawLayerTemplateNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _layerTemplateContractAllowedKeys =
        { "name", "id", "containers", "exclude_containers", "layers", "exhaustive", "reason" };

    private static readonly string[] _templateLayerAllowedKeys = { "name", "optional" };

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (!document.TryGetSection(RawYamlNodes.ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateGroup(contracts, "strict_layer_templates", document.Provenance);
        ValidateGroup(contracts, "audit_layer_templates", document.Provenance);
    }

    private static void ValidateGroup(
        YamlMappingNode contracts, string groupKey, ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!RawYamlNodes.TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        for (int index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode contractNode)
            {
                continue;
            }

            provenance.SetValidationSubject(RawYamlNodes.ContractPath(groupKey, index));
            ValidateContract(contractNode);
        }
    }

    private static void ValidateContract(YamlMappingNode contractNode)
    {
        string contractName = RawYamlNodes.ContractName(contractNode);

        RawYamlNodes.ValidateKnownKeys(contractNode, contractName, "layer template contract", _layerTemplateContractAllowedKeys);

        if (!RawYamlNodes.TryGetChild(contractNode, "layers", out YamlNode? layersNode) || layersNode is not YamlSequenceNode layersSequence)
        {
            return;
        }

        foreach (YamlNode layerNode in layersSequence.Children)
        {
            if (layerNode is YamlMappingNode layerMapping)
            {
                RawYamlNodes.ValidateKnownKeys(layerMapping, contractName, "layers", _templateLayerAllowedKeys);
            }
        }
    }
}
