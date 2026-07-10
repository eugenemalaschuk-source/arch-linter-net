using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayerNamespacesValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureLayer layer in document.Layers.Values.Where(l => !string.IsNullOrWhiteSpace(l.Namespace)))
        {
            _ = layer.GlobPattern;
        }
    }
}
