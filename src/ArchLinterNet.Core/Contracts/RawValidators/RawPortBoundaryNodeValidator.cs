using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Same rationale as RawContextualContractNodeValidator, for the port-boundary family: its contract,
// target_context, selector-list and adapter-binding nodes are all closed key sets whose typos
// IgnoreUnmatchedProperties() would silently discard. Runs immediately after the contextual
// validator, reproducing the single raw contextual pass that covered both families before
// extraction.
internal sealed class RawPortBoundaryNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _targetContextAllowedKeys = { RawYamlNodes.MetadataKey };
    private static readonly string[] _adapterBindingAllowedKeys = { "adapter", "expected_port", "allowed_contexts" };
    private static readonly string[] _adapterBindingSelectorFields = { "adapter", "expected_port" };

    private static readonly string[] _contractAllowedKeys =
    {
        "name", "id", RawYamlNodes.SourceKey, "target_context", "allowed_seams", RawYamlNodes.ForbiddenKey,
        "adapter_bindings", RawYamlNodes.ExcludeKey, "ignored_violations", "reason"
    };

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        RawYamlNodes.ForEachContract(document, "strict_port_boundaries", (entry, _, _) => ValidateContract(entry));
        RawYamlNodes.ForEachContract(document, "audit_port_boundaries", (entry, _, _) => ValidateContract(entry));
    }

    private static void ValidateContract(YamlMappingNode entry)
    {
        string name = RawYamlNodes.ContractName(entry);
        RawYamlNodes.ValidateKnownKeys(entry, name, "port-boundary contract", _contractAllowedKeys);

        if (RawYamlNodes.TryGetChild(entry, RawYamlNodes.SourceKey, out YamlNode? source)
            && source is YamlMappingNode sourceMapping)
        {
            RawContextualSelectorKeys.ValidateNodeKeys(sourceMapping, name, RawYamlNodes.SourceKey);
        }

        if (RawYamlNodes.TryGetChild(entry, "target_context", out YamlNode? targetContext)
            && targetContext is YamlMappingNode targetMapping)
        {
            RawYamlNodes.ValidateKnownKeys(targetMapping, name, "target_context", _targetContextAllowedKeys);
        }

        RawContextualSelectorKeys.ValidateListKeys(entry, name, "allowed_seams");
        RawContextualSelectorKeys.ValidateListKeys(entry, name, RawYamlNodes.ForbiddenKey);
        RawContextualSelectorKeys.ValidateListKeys(entry, name, RawYamlNodes.ExcludeKey);
        ValidateAdapterBindings(entry, name);
    }

    private static void ValidateAdapterBindings(YamlMappingNode contractNode, string contractName)
    {
        if (!RawYamlNodes.TryGetChild(contractNode, "adapter_bindings", out YamlNode? bindingsNode)
            || bindingsNode is not YamlSequenceNode bindings)
        {
            return;
        }

        foreach (YamlMappingNode binding in bindings.Children.OfType<YamlMappingNode>())
        {
            RawYamlNodes.ValidateKnownKeys(binding, contractName, "adapter_bindings entry", _adapterBindingAllowedKeys);

            foreach (string field in _adapterBindingSelectorFields)
            {
                if (RawYamlNodes.TryGetChild(binding, field, out YamlNode? selector)
                    && selector is YamlMappingNode mapping)
                {
                    RawContextualSelectorKeys.ValidateNodeKeys(mapping, contractName, $"adapter_bindings.{field}");
                }
            }

            RawContextualSelectorKeys.ValidateListKeys(binding, contractName, "allowed_contexts");
        }
    }
}
