using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class VersionedContractSurfaceIsolationValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureVersionedContractSurfaceIsolationContract contract in document.Contracts.StrictVersionedContractSurfaceIsolation
            .Concat(document.Contracts.AuditVersionedContractSurfaceIsolation))
        {
            if (string.IsNullOrWhiteSpace(contract.Id) || string.IsNullOrWhiteSpace(contract.Name))
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare non-blank 'id' and 'name'.");
            if (contract.Surfaces is null || contract.Surfaces.Count == 0)
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare a non-empty 'surfaces' list.");
            var surfaces = new Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface>(StringComparer.OrdinalIgnoreCase);
            foreach (ArchitectureVersionedContractSurfaceIsolationSurface surface in contract.Surfaces)
            {
                if (surface is null || string.IsNullOrWhiteSpace(surface.Id))
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares a blank surface ID.");
                if (!surfaces.TryAdd(surface.Id, surface))
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares duplicate surface ID '{surface.Id}'.");
                if (surface.TypesMatching is null || !surface.TypesMatching.HasAnyField)
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' surface '{surface.Id}' declares an empty or unbounded 'types_matching' selector.");
                ValidateLayer(document, contract.Name, surface.Id, surface.TypesMatching);
            }
            if (string.IsNullOrWhiteSpace(contract.SourceSurface) || !surfaces.ContainsKey(contract.SourceSurface))
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' references unknown source surface '{contract.SourceSurface}'.");
            if (contract.ForbiddenSurfaces is null || contract.ForbiddenSurfaces.Count == 0)
                throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare a non-empty 'forbidden_surfaces' list.");
            var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in contract.ForbiddenSurfaces)
            {
                if (string.IsNullOrWhiteSpace(id) || !forbidden.Add(id))
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares a blank or duplicate forbidden surface reference.");
                if (string.Equals(id, contract.SourceSurface, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' cannot forbid its source surface '{contract.SourceSurface}'.");
                if (!surfaces.ContainsKey(id))
                    throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' references unknown forbidden surface '{id}'.");
            }
        }
    }

    private static void ValidateLayer(ArchitectureContractDocument document, string contractName, string surfaceId, ArchitecturePublicApiSurfaceSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.Layer) && !document.Layers.ContainsKey(selector.Layer))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contractName}' surface '{surfaceId}' references unknown layer '{selector.Layer}'.");
    }
}
