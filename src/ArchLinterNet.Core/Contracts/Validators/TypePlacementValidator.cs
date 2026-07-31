using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class TypePlacementValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureTypePlacementContract contract in document.Provenance.Track(
                     document.Contracts.StrictTypePlacement.Concat(document.Contracts.AuditTypePlacement)))
        {
            ValidateMatcher(document, contract.Name, "types_matching", contract.TypesMatching);
            foreach (ArchitectureTypeMatcher exclusion in contract.ExcludeTypesMatching)
            {
                ValidateMatcher(document, contract.Name, "exclude_types_matching", exclusion);
            }

            bool hasPlacementExpectation = contract.MustResideInLayers.Count > 0
                || contract.MustResideInNamespaces.Count > 0
                || contract.MustResideInProjects.Count > 0
                || contract.MustResideInAssemblies.Count > 0;

            bool hasNamingExpectation = !string.IsNullOrEmpty(contract.RequiredNameSuffix)
                || !string.IsNullOrEmpty(contract.RequiredNamePrefix)
                || !string.IsNullOrEmpty(contract.ForbiddenNameSuffix)
                || !string.IsNullOrEmpty(contract.ForbiddenNamePrefix);

            if (!hasPlacementExpectation && !hasNamingExpectation)
            {
                throw new InvalidOperationException($"Type placement contract '{contract.Name}' declares a selector but no expectation.");
            }
        }
    }

    private static void ValidateMatcher(string contractName, string field, ArchitectureTypeMatcher matcher)
    {
        bool hasSelectorField = !string.IsNullOrEmpty(matcher.NameSuffix)
            || !string.IsNullOrEmpty(matcher.NamePrefix)
            || !string.IsNullOrEmpty(matcher.Namespace)
            || !string.IsNullOrEmpty(matcher.Layer)
            || !string.IsNullOrEmpty(matcher.BaseType)
            || !string.IsNullOrEmpty(matcher.ImplementsInterface)
            || !string.IsNullOrEmpty(matcher.HasAttribute);

        if (!hasSelectorField)
        {
            throw new InvalidOperationException(
                $"Type placement contract '{contractName}' declares no usable {field} selector field " +
                "(name_suffix/name_prefix/namespace/layer/base_type/implements_interface/has_attribute). " +
                "An empty or omitted selector would match every loaded type, turning a role-specific rule into " +
                "a global one. Declare at least one selector field, or check for a typo'd field name.");
        }
    }

    private static void ValidateMatcher(
        ArchitectureContractDocument document,
        string contractName,
        string field,
        ArchitectureTypeMatcher matcher)
    {
        ValidateMatcher(contractName, field, matcher);
        if (!string.IsNullOrEmpty(matcher.Layer))
        {
            ArchitectureLayerResolver.ResolveLayer(document, contractName, matcher.Layer);
        }
    }
}
