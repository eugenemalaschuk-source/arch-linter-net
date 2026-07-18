using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayoutConventionsValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureLayoutConventionContract contract in document.Provenance.Track(
                     document.Contracts.StrictLayoutConventions.Concat(document.Contracts.AuditLayoutConventions)))
        {
            ArchitectureLayoutFileMatcher matcher = contract.FilesMatching;
            bool hasSelectorField = !string.IsNullOrEmpty(matcher.FolderSegment)
                || !string.IsNullOrEmpty(matcher.NamespaceSegment)
                || !string.IsNullOrEmpty(matcher.FileNameSuffix)
                || !string.IsNullOrEmpty(matcher.FileNamePrefix);

            if (!hasSelectorField)
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contract.Name}' declares no usable files_matching selector field " +
                    "(folder_segment/namespace_segment/file_name_suffix/file_name_prefix). An empty or omitted " +
                    "selector would match every source file, turning a folder-specific rule into a global one. " +
                    "Declare at least one selector field, or check for a typo'd field name.");
            }

            bool hasExpectation = !string.IsNullOrEmpty(contract.RequireTypeKind)
                || !string.IsNullOrEmpty(contract.ForbidTypeKind)
                || !string.IsNullOrEmpty(contract.RequiredNameSuffix)
                || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
                || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
                || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix)
                || contract.RequireTypeNameMatchesFileName
                || contract.RequireMatchingInterface != null;

            if (!hasExpectation)
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contract.Name}' declares a files_matching selector but no " +
                    "expectation (require_type_kind/forbid_type_kind/required_name_suffix/required_name_prefix/" +
                    "forbidden_name_suffix/forbidden_name_prefix/require_type_name_matches_file_name/" +
                    "require_matching_interface). Declare at least one, or the rule can never produce a violation.");
            }

            ValidateTypeKind(contract.Name, "require_type_kind", contract.RequireTypeKind);
            ValidateTypeKind(contract.Name, "forbid_type_kind", contract.ForbidTypeKind);
        }
    }

    private static void ValidateTypeKind(string contractName, string fieldName, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (!Enum.TryParse(value, ignoreCase: true, out ArchitectureTypeKind _))
        {
            throw new InvalidOperationException(
                $"Layout convention contract '{contractName}' declares '{fieldName}: {value}', which is not a " +
                "recognized type kind. Expected one of: class, interface, struct, enum, record, delegate.");
        }
    }
}
