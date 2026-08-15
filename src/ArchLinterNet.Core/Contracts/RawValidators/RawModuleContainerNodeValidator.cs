using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Module containers have a deliberately small, closed vocabulary: a misspelled root exception
// would otherwise deserialize as an empty list and turn a reviewed migration seam into an opaque
// violation. Validate these keys before IgnoreUnmatchedProperties can discard that author intent.
internal sealed class RawModuleContainerNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _allowedKeys =
    {
        "name", "id", "container", "profile", "allowed_container_root_types", "allowed_module_root_types",
        "ignored_violations", "reason",
    };

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        RawYamlNodes.ForEachContract(document, "strict_module_containers", (contract, _, _) => ValidateContract(contract));
        RawYamlNodes.ForEachContract(document, "audit_module_containers", (contract, _, _) => ValidateContract(contract));
    }

    private static void ValidateContract(YamlMappingNode contractNode)
    {
        string contractName = RawYamlNodes.ContractName(contractNode);
        RawYamlNodes.ValidateKnownKeys(contractNode, contractName, "module container contract", _allowedKeys);
    }
}
