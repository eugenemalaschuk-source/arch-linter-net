using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// IgnoreUnmatchedProperties() is intentionally used by normal policy loading, but this family is
// a bounded-selection boundary. Keep its closed object shapes visible before deserialization can
// erase a typo such as `type_matching` or `regex`, and reject empty selector/list values before
// they can turn into an apparently valid no-op policy.
internal sealed class RawContractSurfaceExposureNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _contractKeys =
    ["id", "name", "source", "forbidden", "ignored_violations", "reason"];

    private static readonly string[] _sourceKeys =
    ["assemblies", "projects", "types_matching", "public_api_surface"];

    private static readonly string[] _selectorKeys =
    ["name_suffix", "name_prefix", "namespace", "layer", "base_type", "implements_interface", "has_attribute", "role"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        ValidateGroup(document, "strict_contract_surface_exposure");
        ValidateGroup(document, "audit_contract_surface_exposure");
    }

    private static void ValidateGroup(ArchitecturePolicyRawDocument document, string groupKey)
    {
        if (document.Root is null
            || !RawYamlNodes.TryGetChild(document.Root, RawYamlNodes.ContractsKey, out YamlNode? contractsNode)
            || contractsNode is not YamlMappingNode contracts
            || !RawYamlNodes.TryGetChild(contracts, groupKey, out YamlNode? groupNode))
        {
            return;
        }

        if (groupNode is not YamlSequenceNode contractsSequence)
        {
            throw new InvalidOperationException(
                $"Contract group '{groupKey}' must be a list of contract objects.");
        }

        for (int index = 0; index < contractsSequence.Children.Count; index++)
        {
            string contractPath = RawYamlNodes.ContractPath(groupKey, index);
            document.Provenance.SetValidationSubject(contractPath);

            if (contractsSequence.Children[index] is not YamlMappingNode contractNode)
            {
                throw new InvalidOperationException(
                    $"Contract group '{groupKey}' entry {index} must be an object.");
            }

            ValidateContract(contractNode, groupKey);
        }
    }

    private static void ValidateContract(YamlMappingNode contractNode, string groupKey)
    {
        string contractName = RawYamlNodes.ContractName(contractNode);
        RawYamlNodes.ValidateKnownKeys(contractNode, contractName, "contract-surface exposure contract", _contractKeys);

        RequireNonBlankScalar(contractNode, "id", contractName, "stable contract ID");
        RequireNonBlankScalar(contractNode, "name", contractName, "contract name");

        if (!RawYamlNodes.TryGetChild(contractNode, RawYamlNodes.SourceKey, out YamlNode? sourceNode)
            || sourceNode is not YamlMappingNode source)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' must declare a 'source' object.");
        }

        RawYamlNodes.ValidateKnownKeys(source, contractName, "contract-surface exposure source", _sourceKeys);
        ValidateSource(source, contractName);

        if (!RawYamlNodes.TryGetChild(contractNode, RawYamlNodes.ForbiddenKey, out YamlNode? forbiddenNode)
            || forbiddenNode is not YamlSequenceNode forbidden
            || forbidden.Children.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' must declare a non-empty 'forbidden' list.");
        }

        for (int index = 0; index < forbidden.Children.Count; index++)
        {
            if (forbidden.Children[index] is not YamlMappingNode selector)
            {
                throw new InvalidOperationException(
                    $"Contract-surface exposure contract '{contractName}' forbidden[{index}] must be a selector object.");
            }

            ValidateSelector(selector, contractName, $"forbidden[{index}]");
        }
    }

    private static void ValidateSource(YamlMappingNode source, string contractName)
    {
        bool hasAssemblies = RawYamlNodes.TryGetChild(source, "assemblies", out YamlNode? assembliesNode);
        bool hasProjects = RawYamlNodes.TryGetChild(source, "projects", out YamlNode? projectsNode);
        bool hasTypesMatching = RawYamlNodes.TryGetChild(source, "types_matching", out YamlNode? typesMatchingNode);
        bool hasPublicApiSurface = RawYamlNodes.TryGetChild(source, "public_api_surface", out YamlNode? publicApiNode);

        if (hasAssemblies)
        {
            ValidateNonBlankList(assembliesNode, contractName, "source.assemblies");
        }

        if (hasProjects)
        {
            ValidateNonBlankList(projectsNode, contractName, "source.projects");
        }

        if (hasTypesMatching)
        {
            if (typesMatchingNode is not YamlMappingNode typesMatching)
            {
                throw new InvalidOperationException(
                    $"Contract-surface exposure contract '{contractName}' source.types_matching must be a selector object.");
            }

            ValidateSelector(typesMatching, contractName, "source.types_matching");
        }

        if (hasPublicApiSurface)
        {
            RequireNonBlankScalar(source, "public_api_surface", contractName, "source public API surface ID");
        }

        if (!hasAssemblies && !hasProjects && !hasTypesMatching && !hasPublicApiSurface)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares no usable source selector. " +
                "Declare at least one of assemblies, projects, types_matching, or public_api_surface.");
        }
    }

    private static void ValidateSelector(YamlMappingNode selector, string contractName, string location)
    {
        RawYamlNodes.ValidateKnownKeys(selector, contractName, location, _selectorKeys);

        bool hasUsableField = false;
        foreach ((YamlNode keyNode, YamlNode valueNode) in selector.Children)
        {
            if (keyNode is not YamlScalarNode key || valueNode is not YamlScalarNode value)
            {
                throw new InvalidOperationException(
                    $"Contract-surface exposure contract '{contractName}' {location} selector values must be non-blank scalars.");
            }

            if (string.IsNullOrWhiteSpace(value.Value))
            {
                throw new InvalidOperationException(
                    $"Contract-surface exposure contract '{contractName}' declares a blank '{location}.{key.Value}' selector value.");
            }

            hasUsableField = true;
        }

        if (!hasUsableField)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares an empty or unbounded '{location}' selector. " +
                "Declare at least one of name_suffix, name_prefix, namespace, layer, base_type, implements_interface, has_attribute, or role.");
        }
    }

    private static void ValidateNonBlankList(YamlNode? node, string contractName, string location)
    {
        if (node is not YamlSequenceNode sequence || sequence.Children.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares an empty '{location}' list. " +
                "Every populated source list must contain at least one non-blank entry.");
        }

        foreach (YamlNode entry in sequence.Children)
        {
            if (entry is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                throw new InvalidOperationException(
                    $"Contract-surface exposure contract '{contractName}' declares a blank or non-scalar entry in '{location}'.");
            }
        }
    }

    private static void RequireNonBlankScalar(
        YamlMappingNode parent, string key, string contractName, string description)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? node)
            || node is not YamlScalarNode scalar
            || string.IsNullOrWhiteSpace(scalar.Value))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' must declare a non-blank '{key}' ({description}).");
        }
    }
}
