using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Raw-YAML key validation for semantic-role coverage exclusions (openspec/specs/yaml-contract-loading
// "Semantic coverage exclusions reject unknown keys"). A typo'd exclusion key such as "metdata" is
// silently dropped by IgnoreUnmatchedProperties(), which would widen a metadata-scoped exclusion into
// a role-wide one; only this pre-deserialization pass can still see the rejected name.
internal sealed class RawSemanticCoverageNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    public void Validate(ArchitecturePolicyRawDocument document)
    {
        RawYamlNodes.ForEachContract(document, "strict_coverage", (contract, _, _) => ValidateContract(contract));
        RawYamlNodes.ForEachContract(document, "audit_coverage", (contract, _, _) => ValidateContract(contract));
    }

    private static void ValidateContract(YamlMappingNode contract)
    {
        if (!RawYamlNodes.TryGetChild(contract, "scope", out YamlNode? scopeNode)
            || scopeNode is not YamlScalarNode scope
            || !string.Equals(scope.Value, "semantic_role", StringComparison.Ordinal)
            || !RawYamlNodes.TryGetChild(contract, RawYamlNodes.ExcludeKey, out YamlNode? excludeNode)
            || excludeNode is not YamlSequenceNode exclusions)
        {
            return;
        }

        string contractName = RawYamlNodes.ContractName(contract);
        foreach (YamlMappingNode exclusion in exclusions.Children.OfType<YamlMappingNode>())
        {
            RawYamlNodes.ValidateKnownKeys(exclusion, contractName, "semantic coverage exclusion",
                new[]
                {
                    "namespace", RawYamlNodes.NamespaceSuffixKey, "project", "assembly", "contract_id", "between",
                    "role", "metadata", "reason"
                });
        }
    }
}
