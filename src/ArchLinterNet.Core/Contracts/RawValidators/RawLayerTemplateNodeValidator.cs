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
        RawYamlNodes.ForEachContract(document, "strict_layer_templates", (contract, _, _) => ValidateContract(contract));
        RawYamlNodes.ForEachContract(document, "audit_layer_templates", (contract, _, _) => ValidateContract(contract));
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
