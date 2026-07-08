using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayerNamespacesValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (KeyValuePair<string, ArchitectureLayer> pair in document.Layers)
        {
            ArchitectureLayer layer = pair.Value;

            if (!string.IsNullOrWhiteSpace(layer.Namespace))
            {
                _ = layer.GlobPattern;
            }
        }
    }
}
