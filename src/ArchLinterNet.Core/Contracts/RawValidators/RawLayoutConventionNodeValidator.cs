using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Raw-YAML key validation for layout convention contracts. Mirrors
// RawContextualContractNodeValidator's rationale: IgnoreUnmatchedProperties() would otherwise
// silently drop a typo'd files_matching key (e.g. "folder_segments" for "folder_segment"), leaving
// the selector looking like a legitimate-but-empty field instead of failing the load.
// RawWhenFieldLocationValidator separately enforces that `when` may only appear on this exact node -
// this pass only checks the non-`when` field names.
internal sealed class RawLayoutConventionNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _layoutFilesMatchingAllowedKeys =
        { "folder_segment", "namespace_segment", "file_name_suffix", "file_name_prefix", RawYamlNodes.WhenKey };

    private static readonly string[] _layoutRequireMatchingInterfaceAllowedKeys = { "name_prefix" };

    private const string ExcludeFilesMatchingKey = "exclude_files_matching";

    private static readonly string[] _layoutConventionContractAllowedKeys =
    {
        "name", "id", "files_matching", ExcludeFilesMatchingKey, "require_type_kind", "forbid_type_kind",
        "required_name_suffix", "required_name_prefix", "forbidden_name_suffix", "forbidden_name_prefix",
        "require_type_name_matches_file_name", "require_matching_interface", "ignored_violations", "reason"
    };

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (!document.TryGetSection(RawYamlNodes.ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateGroup(contracts, "strict_layout_conventions", document.Provenance);
        ValidateGroup(contracts, "audit_layout_conventions", document.Provenance);
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
            ValidateContract(contractNode, groupKey, index, provenance);
        }
    }

    private static void ValidateContract(
        YamlMappingNode contractNode, string groupKey, int index, ArchitecturePolicyProvenanceIndex provenance)
    {
        string contractName = RawYamlNodes.ContractName(contractNode);

        // Top-level fields too: without this, a typo like "required_name_sufix" is silently
        // dropped by IgnoreUnmatchedProperties() for a monolithic (non-imported) policy - the
        // composed-policy path catches this via schema/dependencies.arch.schema.json's
        // additionalProperties: false, but that JSON-schema pass never runs for a monolithic
        // policy, so this raw-YAML check is the only place monolithic policies get the same
        // protection. Mirrors RawPortBoundaryNodeValidator's identical rationale.
        RawYamlNodes.ValidateKnownKeys(contractNode, contractName, "layout convention contract", _layoutConventionContractAllowedKeys);

        if (RawYamlNodes.TryGetChild(contractNode, "files_matching", out YamlNode? filesMatchingNode)
            && filesMatchingNode is YamlMappingNode filesMatchingMapping)
        {
            RawYamlNodes.ValidateKnownKeys(
                filesMatchingMapping, contractName, "files_matching", _layoutFilesMatchingAllowedKeys);
        }

        ValidateExcludeFilesMatching(contractNode, contractName, groupKey, index, provenance);

        // Same rationale as files_matching above: require_matching_interface has exactly one
        // accepted key (name_prefix). Without this raw-YAML check, a typo like "name_prefx"
        // would be silently dropped by IgnoreUnmatchedProperties(), leaving NamePrefix null and
        // the contract quietly falling back to the default "I" prefix instead of failing to load.
        if (RawYamlNodes.TryGetChild(contractNode, "require_matching_interface", out YamlNode? requireMatchingInterfaceNode)
            && requireMatchingInterfaceNode is YamlMappingNode requireMatchingInterfaceMapping)
        {
            RawYamlNodes.ValidateKnownKeys(
                requireMatchingInterfaceMapping, contractName, "require_matching_interface",
                _layoutRequireMatchingInterfaceAllowedKeys);
        }
    }

    // Same rationale as files_matching above: each exclude_files_matching item is validated
    // against the same matcher key set, with the validation subject pointed at that indexed
    // item so a typo'd exclusion key reports its own location rather than the contract's.
    private static void ValidateExcludeFilesMatching(
        YamlMappingNode contractNode, string contractName, string groupKey, int index, ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!RawYamlNodes.TryGetChild(contractNode, ExcludeFilesMatchingKey, out YamlNode? excludeFilesMatchingNode)
            || excludeFilesMatchingNode is not YamlSequenceNode excludeFilesMatchingSequence)
        {
            return;
        }

        string excludeFilesMatchingPath = ArchitecturePolicyProvenancePath.AppendProperty(
            RawYamlNodes.ContractPath(groupKey, index), ExcludeFilesMatchingKey);

        for (int exclusionIndex = 0; exclusionIndex < excludeFilesMatchingSequence.Children.Count; exclusionIndex++)
        {
            if (excludeFilesMatchingSequence.Children[exclusionIndex] is not YamlMappingNode exclusionMapping)
            {
                continue;
            }

            provenance.SetValidationSubject(
                ArchitecturePolicyProvenancePath.AppendIndex(excludeFilesMatchingPath, exclusionIndex));
            RawYamlNodes.ValidateKnownKeys(
                exclusionMapping, contractName, ExcludeFilesMatchingKey, _layoutFilesMatchingAllowedKeys);
        }

        provenance.SetValidationSubject(RawYamlNodes.ContractPath(groupKey, index));
    }
}
