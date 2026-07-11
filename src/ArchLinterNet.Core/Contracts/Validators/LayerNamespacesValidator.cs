using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayerNamespacesValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string name, ArchitectureLayer layer) in document.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Namespace) && layer.Selector == null)
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' must declare a non-empty namespace or selector.");
            }

            if (layer.Selector == null)
            {
                _ = layer.GlobPattern;
                continue;
            }

            if (string.IsNullOrWhiteSpace(layer.Selector.Role))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' selector must declare a non-empty role.");
            }

            if (layer.Selector.Metadata.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' selector metadata keys must be non-empty.");
            }

            if (!string.IsNullOrWhiteSpace(layer.Namespace))
            {
                _ = layer.GlobPattern;
            }
        }
    }
}
