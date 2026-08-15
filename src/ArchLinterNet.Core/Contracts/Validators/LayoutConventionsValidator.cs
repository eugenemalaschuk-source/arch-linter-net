using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayoutConventionsValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureLayoutConventionContract contract in document.Provenance.Track(
                     document.Contracts.StrictLayoutConventions.Concat(document.Contracts.AuditLayoutConventions)))
        {
            ValidateMatcher(contract.Name, "files_matching", contract.FilesMatching);
            foreach (ArchitectureLayoutFileMatcher exclusion in contract.ExcludeFilesMatching)
            {
                ValidateMatcher(contract.Name, "exclude_files_matching", exclusion);
            }

            bool hasExpectation = !string.IsNullOrEmpty(contract.RequireTypeKind)
                || !string.IsNullOrEmpty(contract.ForbidTypeKind)
                || !string.IsNullOrEmpty(contract.RequiredNameSuffix)
                || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
                || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
                || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix)
                || contract.RequireTypeNameMatchesFileName
                || contract.MaxDeclarationsPerType is not null
                || contract.RequireMatchingInterface != null
                || contract.AllDeclarations != null;

            if (!hasExpectation)
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contract.Name}' declares a files_matching selector but no " +
                    "expectation (require_type_kind/forbid_type_kind/required_name_suffix/required_name_prefix/" +
                    "forbidden_name_suffix/forbidden_name_prefix/require_type_name_matches_file_name/" +
                    "max_declarations_per_type/require_matching_interface/all_declarations). Declare at least one, or the rule can never produce a violation.");
            }

            ValidateTypeKind(contract.Name, "require_type_kind", contract.RequireTypeKind);
            ValidateTypeKind(contract.Name, "forbid_type_kind", contract.ForbidTypeKind);
            ValidateMaxDeclarationsPerType(contract.Name, contract.MaxDeclarationsPerType);
            ValidateAllDeclarations(contract);
        }
    }

    private static void ValidateMatcher(string contractName, string fieldName, ArchitectureLayoutFileMatcher matcher)
    {
        bool hasSelectorField = !string.IsNullOrEmpty(matcher.FolderSegment)
            || !string.IsNullOrEmpty(matcher.NamespaceSegment)
            || !string.IsNullOrEmpty(matcher.FileNameSuffix)
            || !string.IsNullOrEmpty(matcher.FileNamePrefix);

        if (!hasSelectorField)
        {
            throw new InvalidOperationException(
                $"Layout convention contract '{contractName}' declares no usable {fieldName} selector field " +
                "(folder_segment/namespace_segment/file_name_suffix/file_name_prefix). An empty or omitted " +
                "selector would match every source file, turning a folder-specific rule into a global one. " +
                "Declare at least one selector field, or check for a typo'd field name.");
        }
    }

    private static void ValidateTypeKind(string contractName, string fieldName, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (!ArchitectureLayoutTypeKindParser.TryParse(value, out _))
        {
            throw new InvalidOperationException(
                $"Layout convention contract '{contractName}' declares '{fieldName}: {value}', which is not a " +
                "recognized type kind. Expected one of: class, interface, struct, enum, record, delegate.");
        }
    }

    private static void ValidateMaxDeclarationsPerType(string contractName, int? value)
    {
        if (value is not <= 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Layout convention contract '{contractName}' declares 'max_declarations_per_type: {value}', " +
                "which must be a positive integer.");
    }

    private static void ValidateAllDeclarations(ArchitectureLayoutConventionContract contract)
    {
        ArchitectureLayoutDeclarationShape? shape = contract.AllDeclarations;
        if (shape == null)
        {
            return;
        }

        if (shape.AllowedTypeKinds.Count == 0 && shape.AllowedRoles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Layout convention contract '{contract.Name}' declares all_declarations without an effective permitted shape. " +
                "Declare allowed_type_kinds and/or allowed_roles; require_abstract_classes only narrows an already-permitted shape.");
        }

        ValidateDistinctNonBlankTypeKinds(contract.Name, shape.AllowedTypeKinds);
        ValidateDistinctNonBlankRoles(contract.Name, shape.AllowedRoles);
    }

    private static void ValidateDistinctNonBlankTypeKinds(string contractName, IReadOnlyList<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            ValidateTypeKind(contractName, $"all_declarations.allowed_type_kinds[{index}]", value);
            if (!seen.Add(value))
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contractName}' repeats all_declarations.allowed_type_kinds value '{value}'.");
            }
        }
    }

    private static void ValidateDistinctNonBlankRoles(string contractName, IReadOnlyList<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contractName}' has a blank all_declarations.allowed_roles entry at index {index}.");
            }

            if (!seen.Add(value))
            {
                throw new InvalidOperationException(
                    $"Layout convention contract '{contractName}' repeats all_declarations.allowed_roles value '{value}'.");
            }
        }
    }
}
