using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// The contextual-selector key rules, shared by the contextual-contract and port-boundary raw
// validators because both families bind the same ArchitectureContextSelector shape.
internal static class RawContextualSelectorKeys
{
    public static void ValidateListKeys(
        YamlMappingNode contractNode, string contractName, string listKey, bool allowWhen = false)
    {
        if (!RawYamlNodes.TryGetChild(contractNode, listKey, out YamlNode? listNode) || listNode is not YamlSequenceNode listSequence)
        {
            return;
        }

        foreach (YamlNode itemNode in listSequence.Children)
        {
            if (itemNode is YamlMappingNode itemMapping)
            {
                ValidateNodeKeys(itemMapping, contractName, listKey, allowWhen);
            }
        }
    }

    // allowWhen is scoped per call site (not per selector type): ArchitectureContextSelector is
    // reused by port-boundary/adapter-binding contracts, which openspec/specs/cel-policy-model's
    // closed first-wave `when` location list does not include. Only RawContextualContractNodeValidator
    // (context_dependencies/context_allow_only) passes allowWhen: true. See
    // openspec/changes/archive/2026-07-18-core-cel-integration/design.md Decision D4.
    public static void ValidateNodeKeys(
        YamlMappingNode selectorNode, string contractName, string fieldName, bool allowWhen = false)
    {
        foreach ((YamlNode keyNode, _) in selectorNode.Children)
        {
            if (keyNode is YamlScalarNode scalar
                && !string.Equals(scalar.Value, "role", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "metadata", StringComparison.Ordinal)
                && !(allowWhen && string.Equals(scalar.Value, RawYamlNodes.WhenKey, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares an unknown property '{scalar.Value}' on its '{fieldName}' selector. " +
                    (allowWhen
                        ? "A contextual selector supports only 'role', 'metadata', and 'when'."
                        : "A contextual selector supports only 'role' and 'metadata'."));
            }
        }
    }
}
