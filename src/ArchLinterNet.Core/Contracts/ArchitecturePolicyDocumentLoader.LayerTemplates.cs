using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts;

// Raw-YAML key validation for layer template contracts, split out of
// ArchitecturePolicyDocumentLoader.cs to keep both files under the repository's file-size lint
// budget (make/lint.mk CS_SIZE_LINT_ERROR_LINES). Mirrors ValidateRawLayoutConventionYaml's
// rationale: IgnoreUnmatchedProperties() would otherwise silently drop a typo'd key (e.g.
// "exclude_container" for "exclude_containers") for a monolithic (non-imported) policy - the
// composed-policy path catches this via schema/dependencies.arch.schema.json's
// additionalProperties: false, but that JSON-schema pass never runs for a monolithic policy.
public sealed partial class ArchitecturePolicyDocumentLoader
{
    private static readonly string[] _layerTemplateContractAllowedKeys =
        { "name", "id", "containers", "exclude_containers", "layers", "exhaustive", "reason" };

    private static readonly string[] _templateLayerAllowedKeys = { "name", "optional" };

    private static void ValidateRawLayerTemplateYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateLayerTemplateContractGroup(contracts!, "strict_layer_templates", provenance);
        ValidateLayerTemplateContractGroup(contracts!, "audit_layer_templates", provenance);
    }

    private static void ValidateLayerTemplateContractGroup(
        YamlMappingNode contracts, string groupKey, ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        for (int index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode contractNode)
            {
                continue;
            }

            provenance.SetValidationSubject(ContractPath(groupKey, index));
            string contractName = TryGetChild(contractNode, "name", out YamlNode? nameNode)
                && nameNode is YamlScalarNode nameScalar
                    ? nameScalar.Value ?? UnnamedContractName
                    : UnnamedContractName;

            ValidateKnownKeys(contractNode, contractName, "layer template contract", _layerTemplateContractAllowedKeys);

            if (TryGetChild(contractNode, "layers", out YamlNode? layersNode) && layersNode is YamlSequenceNode layersSequence)
            {
                foreach (YamlNode layerNode in layersSequence.Children)
                {
                    if (layerNode is YamlMappingNode layerMapping)
                    {
                        ValidateKnownKeys(layerMapping, contractName, "layers", _templateLayerAllowedKeys);
                    }
                }
            }
        }
    }
}
