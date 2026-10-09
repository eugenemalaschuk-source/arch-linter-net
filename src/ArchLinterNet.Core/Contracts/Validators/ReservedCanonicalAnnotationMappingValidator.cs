using ArchLinterNet.Core.Contracts.PolicyImports;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class ReservedCanonicalAnnotationMappingValidator : IArchitecturePolicyDocumentValidator
{
    private const string ReservedNamespacePrefix = "ArchLinterNet.Annotations.";

    public void Validate(ArchitectureContractDocument document)
    {
        ValidateMappings(document.Classification.Attributes, "attributes");
        ValidateMappings(document.Classification.AssemblyAttributes, "assembly_attributes");

        void ValidateMappings(IReadOnlyList<ArchitectureAttributeClassificationMapping> mappings, string collection)
        {
            for (int index = 0; index < mappings.Count; index++)
            {
                ArchitectureAttributeClassificationMapping mapping = mappings[index];
                if (!mapping.Attribute.StartsWith(ReservedNamespacePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string path = ArchitecturePolicyProvenancePath.AppendIndex(
                    ArchitecturePolicyProvenancePath.AppendProperty(
                        ArchitecturePolicyProvenancePath.Property("classification"), collection),
                    index);
                document.Provenance.SetValidationSubject(path);
                throw new InvalidOperationException(
                    $"classification.{collection}[{index}].attribute '{mapping.Attribute}' uses the reserved "
                    + "ArchLinterNet.Annotations namespace and cannot be remapped through YAML. Canonical annotation roles are built in; "
                    + "custom attribute mappings outside the reserved namespace remain supported.");
            }
        }
    }
}
