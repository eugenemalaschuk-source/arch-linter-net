using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Execution;

public static class LayerTemplateExpander
{
    internal const string TemplateOwnerDataKey = "ArchLinterNet.LayerTemplateOwner";

    public static List<ArchitectureLayerContract> Expand(
        IEnumerable<ArchitectureLayerTemplateContract> templates)
    {
        List<ArchitectureLayerContract> contracts = new();

        foreach (ArchitectureLayerTemplateContract template in templates)
        {
            string templateIdBase = ArchitecturePolicyDocumentLoader.NormalizeToContractId(
                template.Id ?? template.Name);

            foreach (string container in template.Containers)
            {
                List<string> layers = new();
                HashSet<string> optionalLayers = new(StringComparer.Ordinal);

                foreach (ArchitectureTemplateLayer templateLayer in template.Layers)
                {
                    if (template.Exhaustive && templateLayer.Name.Contains('.', StringComparison.Ordinal))
                    {
                        var exception = new ArgumentException(
                            $"Dotted template layer name '{templateLayer.Name}' is not supported when exhaustive is true " +
                            $"in template '{template.Name}'. Template layer names must be single namespace segments " +
                            $"(e.g. 'Contracts', not 'Core.Execution').");
                        exception.Data[TemplateOwnerDataKey] = template;
                        throw exception;
                    }

                    string ns = $"{container}.{templateLayer.Name}";
                    layers.Add(ns);
                    if (templateLayer.Optional)
                    {
                        optionalLayers.Add(ns);
                    }
                }

                contracts.Add(new ArchitectureLayerContract
                {
                    Name = $"{template.Name} ({container})",
                    Id = $"{templateIdBase}/{ArchitecturePolicyDocumentLoader.NormalizeToContractId(container)}",
                    Layers = layers,
                    OptionalLayers = optionalLayers,
                    TemplateName = template.Name,
                    ContainerNamespace = container,
                    Exhaustive = template.Exhaustive,
                    Reason = template.Reason
                });
            }
        }

        return contracts;
    }
}
