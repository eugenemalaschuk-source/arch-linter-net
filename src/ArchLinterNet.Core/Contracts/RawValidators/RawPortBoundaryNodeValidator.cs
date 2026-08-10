using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Same rationale as RawContextualContractNodeValidator, for the port-boundary family: its contract,
// target_context, selector-list and adapter-binding nodes are all closed key sets whose typos
// IgnoreUnmatchedProperties() would silently discard. Runs immediately after the contextual
// validator, reproducing the single raw contextual pass that covered both families before
// extraction.
internal sealed class RawPortBoundaryNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _targetContextAllowedKeys = { "metadata" };
    private static readonly string[] _adapterBindingAllowedKeys = { "adapter", "expected_port", "allowed_contexts" };

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        RawYamlNodes.ForEachContract(document, "strict_port_boundaries", (entry, _, _) => ValidateContract(entry));
        RawYamlNodes.ForEachContract(document, "audit_port_boundaries", (entry, _, _) => ValidateContract(entry));
    }

    private static void ValidateContract(YamlMappingNode entry)
    {
        string name = RawYamlNodes.ContractName(entry);
        ValidateContractNodeKeys(entry, name);
        if (RawYamlNodes.TryGetChild(entry, RawYamlNodes.SourceKey, out YamlNode? source) && source is YamlMappingNode sourceMapping)
        {
            RawContextualSelectorKeys.ValidateNodeKeys(sourceMapping, name, RawYamlNodes.SourceKey);
        }
        if (RawYamlNodes.TryGetChild(entry, "target_context", out YamlNode? targetContext) && targetContext is YamlMappingNode targetMapping)
        {
            ValidateTargetContextNodeKeys(targetMapping, name);
        }
        RawContextualSelectorKeys.ValidateListKeys(entry, name, "allowed_seams");
        RawContextualSelectorKeys.ValidateListKeys(entry, name, "forbidden");
        RawContextualSelectorKeys.ValidateListKeys(entry, name, RawYamlNodes.ExcludeKey);
        ValidateAdapterBindings(entry, name);
    }

    private static void ValidateContractNodeKeys(YamlMappingNode node, string contractName)
    {
        string[] allowed = { "name", "id", "source", "target_context", "allowed_seams", "forbidden", "adapter_bindings", RawYamlNodes.ExcludeKey, "ignored_violations", "reason" };
        RawYamlNodes.ValidateKnownKeys(node, contractName, "port-boundary contract", allowed);
    }

    private static void ValidateTargetContextNodeKeys(YamlMappingNode node, string contractName) =>
        RawYamlNodes.ValidateKnownKeys(node, contractName, "target_context", _targetContextAllowedKeys);

    private static void ValidateAdapterBindings(YamlMappingNode contractNode, string contractName)
    {
        if (!RawYamlNodes.TryGetChild(contractNode, "adapter_bindings", out YamlNode? bindingsNode) || bindingsNode is not YamlSequenceNode bindings) return;
        foreach (YamlMappingNode binding in bindings.Children.OfType<YamlMappingNode>())
        {
            RawYamlNodes.ValidateKnownKeys(binding, contractName, "adapter_bindings entry", _adapterBindingAllowedKeys);
            foreach (string field in new[] { "adapter", "expected_port" })
            {
                if (RawYamlNodes.TryGetChild(binding, field, out YamlNode? selector) && selector is YamlMappingNode mapping)
                    RawContextualSelectorKeys.ValidateNodeKeys(mapping, contractName, $"adapter_bindings.{field}");
            }
            RawContextualSelectorKeys.ValidateListKeys(binding, contractName, "allowed_contexts");
        }
    }
}
