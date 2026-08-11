using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// ContextualContractValidator (Validators/) runs after deserialization and can only see what
// IgnoreUnmatchedProperties() left behind - an unknown selector property (e.g. "metdata" typo'd
// for "metadata") is silently dropped by deserialization, leaving ArchitectureContextSelector's
// Metadata at its empty-dictionary default. That default is structurally valid (a role-only
// selector is a legitimate, intentional shape), so no post-deserialization check can distinguish
// "author wrote role-only on purpose" from "author's metadata typo silently vanished" - the
// dictionary looks identical either way. For context_allow_only in particular, an unintentionally
// role-only `allowed` selector silently broadens to match every type of that role (any metadata),
// turning a metadata-scoped allow-list into a false-negative that admits cross-context references.
// This raw-YAML pass, mirroring RawLayerNodeValidator's selector-key check, is the only place
// that can still see the rejected property name before deserialization discards it.
internal sealed class RawContextualContractNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    public void Validate(ArchitecturePolicyRawDocument document)
    {
        ValidateGroup(document, "strict_context_dependencies", RawYamlNodes.ForbiddenKey);
        ValidateGroup(document, "audit_context_dependencies", RawYamlNodes.ForbiddenKey);
        ValidateGroup(document, "strict_context_allow_only", "allowed");
        ValidateGroup(document, "audit_context_allow_only", "allowed");
    }

    private static void ValidateGroup(
        ArchitecturePolicyRawDocument document, string groupKey, string targetListKey)
    {
        RawYamlNodes.ForEachContract(document, groupKey,
            (contractNode, _, _) => ValidateContract(contractNode, targetListKey));
    }

    private static void ValidateContract(YamlMappingNode contractNode, string targetListKey)
    {
        string contractName = RawYamlNodes.ContractName(contractNode);

        if (RawYamlNodes.TryGetChild(contractNode, RawYamlNodes.SourceKey, out YamlNode? sourceNode)
            && sourceNode is YamlMappingNode sourceMapping)
        {
            RawContextualSelectorKeys.ValidateNodeKeys(
                sourceMapping, contractName, RawYamlNodes.SourceKey, allowWhen: true);
        }

        RawContextualSelectorKeys.ValidateListKeys(contractNode, contractName, targetListKey, allowWhen: true);
        RawContextualSelectorKeys.ValidateListKeys(
            contractNode, contractName, RawYamlNodes.ExcludeKey, allowWhen: true);
    }
}
