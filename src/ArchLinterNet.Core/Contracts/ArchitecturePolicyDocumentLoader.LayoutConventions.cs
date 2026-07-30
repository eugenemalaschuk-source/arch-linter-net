using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts;

// Raw-YAML key validation for layout convention contracts, split out of
// ArchitecturePolicyDocumentLoader.cs to keep both files under the repository's file-size lint
// budget (make/lint.mk CS_SIZE_LINT_ERROR_LINES). See that file's ValidateRawLayoutConventionYaml
// call site in LoadCore.
public sealed partial class ArchitecturePolicyDocumentLoader
{
    private static readonly string[] _layoutFilesMatchingAllowedKeys =
        { "folder_segment", "namespace_segment", "file_name_suffix", "file_name_prefix", WhenKey };

    private static readonly string[] _layoutRequireMatchingInterfaceAllowedKeys = { "name_prefix" };

    private static readonly string[] _layoutConventionContractAllowedKeys =
    {
        "name", "id", "files_matching", "exclude_files_matching", "require_type_kind", "forbid_type_kind",
        "required_name_suffix", "required_name_prefix", "forbidden_name_suffix", "forbidden_name_prefix",
        "require_type_name_matches_file_name", "require_matching_interface", "ignored_violations", "reason"
    };

    // Mirrors ValidateRawContextualContractYaml's rationale: IgnoreUnmatchedProperties() would
    // otherwise silently drop a typo'd files_matching key (e.g. "folder_segments" for
    // "folder_segment"), leaving the selector looking like a legitimate-but-empty field instead of
    // failing the load. ValidateRawWhenFieldLocations (WhenFields.cs) separately enforces that `when`
    // may only appear on this exact node - this pass only checks the non-`when` field names.
    private static void ValidateRawLayoutConventionYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateLayoutConventionContractGroup(contracts!, "strict_layout_conventions", provenance);
        ValidateLayoutConventionContractGroup(contracts!, "audit_layout_conventions", provenance);
    }

    private static void ValidateLayoutConventionContractGroup(
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

            // Top-level fields too: without this, a typo like "required_name_sufix" is silently
            // dropped by IgnoreUnmatchedProperties() for a monolithic (non-imported) policy - the
            // composed-policy path catches this via schema/dependencies.arch.schema.json's
            // additionalProperties: false, but that JSON-schema pass never runs for a monolithic
            // policy, so this raw-YAML check is the only place monolithic policies get the same
            // protection. Mirrors ValidatePortBoundaryContractNodeKeys's identical rationale.
            ValidateKnownKeys(contractNode, contractName, "layout convention contract", _layoutConventionContractAllowedKeys);

            if (TryGetChild(contractNode, "files_matching", out YamlNode? filesMatchingNode)
                && filesMatchingNode is YamlMappingNode filesMatchingMapping)
            {
                ValidateKnownKeys(
                    filesMatchingMapping, contractName, "files_matching", _layoutFilesMatchingAllowedKeys);
            }

            // Same rationale as files_matching above: each exclude_files_matching item is validated
            // against the same matcher key set, with the validation subject pointed at that indexed
            // item so a typo'd exclusion key reports its own location rather than the contract's.
            if (TryGetChild(contractNode, "exclude_files_matching", out YamlNode? excludeFilesMatchingNode)
                && excludeFilesMatchingNode is YamlSequenceNode excludeFilesMatchingSequence)
            {
                string excludeFilesMatchingPath = ArchitecturePolicyProvenancePath.AppendProperty(
                    ContractPath(groupKey, index), "exclude_files_matching");

                for (int exclusionIndex = 0; exclusionIndex < excludeFilesMatchingSequence.Children.Count; exclusionIndex++)
                {
                    if (excludeFilesMatchingSequence.Children[exclusionIndex] is not YamlMappingNode exclusionMapping)
                    {
                        continue;
                    }

                    provenance.SetValidationSubject(
                        ArchitecturePolicyProvenancePath.AppendIndex(excludeFilesMatchingPath, exclusionIndex));
                    ValidateKnownKeys(
                        exclusionMapping, contractName, "exclude_files_matching", _layoutFilesMatchingAllowedKeys);
                }

                provenance.SetValidationSubject(ContractPath(groupKey, index));
            }

            // Same rationale as files_matching above: require_matching_interface has exactly one
            // accepted key (name_prefix). Without this raw-YAML check, a typo like "name_prefx"
            // would be silently dropped by IgnoreUnmatchedProperties(), leaving NamePrefix null and
            // the contract quietly falling back to the default "I" prefix instead of failing to load.
            if (TryGetChild(contractNode, "require_matching_interface", out YamlNode? requireMatchingInterfaceNode)
                && requireMatchingInterfaceNode is YamlMappingNode requireMatchingInterfaceMapping)
            {
                ValidateKnownKeys(
                    requireMatchingInterfaceMapping, contractName, "require_matching_interface",
                    _layoutRequireMatchingInterfaceAllowedKeys);
            }
        }
    }
}
