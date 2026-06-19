using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public static class LayerTemplateExpander
{
    public static List<ArchitectureLayerContract> Expand(
        IEnumerable<ArchitectureLayerTemplateContract> templates)
    {
        List<ArchitectureLayerContract> contracts = new();

        foreach (ArchitectureLayerTemplateContract template in templates)
        {
            string templateIdBase = ArchitectureContractLoader.NormalizeToContractId(
                template.Id ?? template.Name);

            foreach (string container in template.Containers)
            {
                List<string> layers = new();
                HashSet<string> optionalLayers = new(StringComparer.Ordinal);

                foreach (ArchitectureTemplateLayer templateLayer in template.Layers)
                {
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
                    Id = $"{templateIdBase}/{ArchitectureContractLoader.NormalizeToContractId(container)}",
                    Layers = layers,
                    OptionalLayers = optionalLayers,
                    TemplateName = template.Name,
                    ContainerNamespace = container,
                    Reason = template.Reason
                });
            }
        }

        return contracts;
    }
}
