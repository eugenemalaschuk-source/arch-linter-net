using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class VersionedContractSurfaceIsolationValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureVersionedContractSurfaceIsolationContract contract in document.Provenance.Track(
                     document.Contracts.StrictVersionedContractSurfaceIsolation.Concat(
                         document.Contracts.AuditVersionedContractSurfaceIsolation)))
        {
            ValidateContract(document, contract);
        }
    }

    private static void ValidateContract(
        ArchitectureContractDocument document,
        ArchitectureVersionedContractSurfaceIsolationContract contract)
    {
        ValidateContractIdentity(contract);
        Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces =
            ValidateSurfaces(document, contract);
        ValidateSourceSurface(contract, surfaces);
        ValidateForbiddenSurfaces(contract, surfaces);
    }

    private static void ValidateContractIdentity(ArchitectureVersionedContractSurfaceIsolationContract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.Id) || string.IsNullOrWhiteSpace(contract.Name))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare non-blank 'id' and 'name'.");
    }

    private static Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> ValidateSurfaces(
        ArchitectureContractDocument document,
        ArchitectureVersionedContractSurfaceIsolationContract contract)
    {
        if (contract.Surfaces is null || contract.Surfaces.Count == 0)
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare a non-empty 'surfaces' list.");

        var surfaces = new Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface>(StringComparer.OrdinalIgnoreCase);
        foreach (ArchitectureVersionedContractSurfaceIsolationSurface surface in contract.Surfaces)
        {
            ValidateSurface(document, contract, surface, surfaces);
        }

        return surfaces;
    }

    private static void ValidateSurface(
        ArchitectureContractDocument document,
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        ArchitectureVersionedContractSurfaceIsolationSurface surface,
        Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces)
    {
        if (surface is null || string.IsNullOrWhiteSpace(surface.Id))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares a blank surface ID.");
        if (!surfaces.TryAdd(surface.Id, surface))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares duplicate surface ID '{surface.Id}'.");
        if (surface.TypesMatching is null || !surface.TypesMatching.HasAnyField)
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' surface '{surface.Id}' declares an empty or unbounded 'types_matching' selector.");
        ValidateLayer(document, contract.Name, surface.Id, surface.TypesMatching);
    }

    private static void ValidateSourceSurface(
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces)
    {
        if (string.IsNullOrWhiteSpace(contract.SourceSurface) || !surfaces.ContainsKey(contract.SourceSurface))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' references unknown source surface '{contract.SourceSurface}'.");
    }

    private static void ValidateForbiddenSurfaces(
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces)
    {
        if (contract.ForbiddenSurfaces is null || contract.ForbiddenSurfaces.Count == 0)
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' must declare a non-empty 'forbidden_surfaces' list.");

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in contract.ForbiddenSurfaces)
        {
            ValidateForbiddenSurface(contract, surfaces, forbidden, id);
        }
    }

    private static void ValidateForbiddenSurface(
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        Dictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces,
        HashSet<string> forbidden,
        string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !forbidden.Add(id))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' declares a blank or duplicate forbidden surface reference.");
        if (string.Equals(id, contract.SourceSurface, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' cannot forbid its source surface '{contract.SourceSurface}'.");
        if (!surfaces.ContainsKey(id))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contract.Name}' references unknown forbidden surface '{id}'.");
    }

    private static void ValidateLayer(ArchitectureContractDocument document, string contractName, string surfaceId, ArchitecturePublicApiSurfaceSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.Layer) && !document.Layers.ContainsKey(selector.Layer))
            throw new InvalidOperationException($"Versioned contract-surface isolation contract '{contractName}' surface '{surfaceId}' references unknown layer '{selector.Layer}'.");
    }
}
