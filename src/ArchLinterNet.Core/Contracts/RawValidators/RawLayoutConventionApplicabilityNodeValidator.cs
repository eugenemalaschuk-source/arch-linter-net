using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

/// <summary>
/// Keeps the closed applicability-inventory YAML surface fail-closed before deserialization would
/// otherwise discard misspelled scope, folder, or linked-convention fields.
/// </summary>
internal sealed class RawLayoutConventionApplicabilityNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _contractKeys =
        ["name", "id", "scope", "exhaustive", "expected_folders", "reason"];
    private static readonly string[] _folderKeys = ["id", "path", "convention_id"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        ValidateGroup(document, "strict_layout_convention_applicability");
        ValidateGroup(document, "audit_layout_convention_applicability");
    }

    private static void ValidateGroup(ArchitecturePolicyRawDocument document, string groupKey)
    {
        RawYamlNodes.ForEachContract(document, groupKey, (contract, _, index) =>
        {
            string name = RawYamlNodes.ContractName(contract);
            RawYamlNodes.ValidateKnownKeys(contract, name, "layout convention applicability contract", _contractKeys);
            if (!RawYamlNodes.TryGetChild(contract, "expected_folders", out YamlNode? foldersNode)
                || foldersNode is not YamlSequenceNode folders)
            {
                return;
            }

            string foldersPath = ArchitecturePolicyProvenancePath.AppendProperty(
                RawYamlNodes.ContractPath(groupKey, index), "expected_folders");
            for (int folderIndex = 0; folderIndex < folders.Children.Count; folderIndex++)
            {
                if (folders.Children[folderIndex] is not YamlMappingNode folder)
                {
                    continue;
                }

                document.Provenance.SetValidationSubject(
                    ArchitecturePolicyProvenancePath.AppendIndex(foldersPath, folderIndex));
                RawYamlNodes.ValidateKnownKeys(folder, name, "expected_folders entry", _folderKeys);
            }

            document.Provenance.SetValidationSubject(RawYamlNodes.ContractPath(groupKey, index));
        });
    }
}
