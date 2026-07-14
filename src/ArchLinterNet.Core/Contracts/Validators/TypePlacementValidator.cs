using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class TypePlacementValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureTypePlacementContract contract in document.Provenance.Track(
                     document.Contracts.StrictTypePlacement.Concat(document.Contracts.AuditTypePlacement)))
        {
            ArchitectureTypeMatcher matcher = contract.TypesMatching;
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
                    $"Type placement contract '{contract.Name}' declares no usable types_matching selector field " +
                    "(name_suffix/name_prefix/namespace/layer/base_type/implements_interface/has_attribute). " +
                    "An empty or omitted selector would match every loaded type, turning a role-specific rule into " +
                    "a global one. Declare at least one selector field, or check for a typo'd field name.");
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
                throw new InvalidOperationException(
                    $"Type placement contract '{contract.Name}' declares a types_matching selector but no placement " +
                    "(must_reside_in_layers/must_reside_in_namespaces/must_reside_in_projects/must_reside_in_assemblies) " +
                    "or naming (required_name_suffix/required_name_prefix/forbidden_name_suffix/forbidden_name_prefix) " +
                    "expectation. Declare at least one, or the rule can never produce a violation.");
            }
        }
    }
}
