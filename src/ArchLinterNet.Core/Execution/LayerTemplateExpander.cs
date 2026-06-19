using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Execution;

public static class LayerTemplateExpander
{
    public static List<ArchitectureLayerContract> Expand(
        IEnumerable<ArchitectureLayerTemplateContract> templates,
        IEnumerable<IArchitectureContract>? existingContracts = null)
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

        if (existingContracts != null)
        {
            ValidateNoIdConflicts(contracts, existingContracts);
        }

        return contracts;
    }

    private static void ValidateNoIdConflicts(
        List<ArchitectureLayerContract> expanded,
        IEnumerable<IArchitectureContract> existing)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);

        foreach (IArchitectureContract c in existing)
        {
            if (c.Id != null && !ids.Add(c.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate contract ID '{c.Id}' in existing contracts.");
            }
        }

        foreach (ArchitectureLayerContract c in expanded)
        {
            if (c.Id != null && !ids.Add(c.Id))
            {
                throw new InvalidOperationException(
                    $"Generated expanded template contract ID '{c.Id}' conflicts with an existing contract ID. Consider using an explicit 'id' on the template to differentiate.");
            }
        }
    }
}
