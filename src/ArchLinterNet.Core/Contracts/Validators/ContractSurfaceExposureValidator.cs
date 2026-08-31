using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

/// <summary>
/// Validates the policy shape for the contract-surface exposure family.
/// </summary>
/// <remarks>
/// This validator deliberately validates configuration only. Source-root resolution and exposure
/// evaluation belong to the execution slice; malformed or ambiguous declarations are rejected
/// here before they could be interpreted as an empty, successful assessment.
/// </remarks>
internal sealed class ContractSurfaceExposureValidator : IArchitecturePolicyDocumentValidator
{
    private static readonly string[] _selectorFields =
    [
        "name_suffix", "name_prefix", "namespace", "layer", "base_type", "implements_interface",
        "has_attribute", "role"
    ];

    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> targetAssemblies = new(document.Analysis.TargetAssemblies, StringComparer.Ordinal);
        List<ArchitecturePublicApiSurfaceContract> publicApiSurfaces = document.Contracts.StrictPublicApiSurface
            .Concat(document.Contracts.AuditPublicApiSurface)
            .ToList();

        foreach (ArchitectureContractSurfaceExposureContract contract in document.Provenance.Track(
                     document.Contracts.StrictContractSurfaceExposure.Concat(
                         document.Contracts.AuditContractSurfaceExposure)))
        {
            ValidateContract(document, contract, targetAssemblies, publicApiSurfaces);
        }
    }

    private static void ValidateContract(
        ArchitectureContractDocument document,
        ArchitectureContractSurfaceExposureContract contract,
        HashSet<string> targetAssemblies,
        IReadOnlyList<ArchitecturePublicApiSurfaceContract> publicApiSurfaces)
    {
        if (string.IsNullOrWhiteSpace(contract.Id))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' must declare a non-blank 'id'.");
        }

        if (string.IsNullOrWhiteSpace(contract.Name))
        {
            throw new InvalidOperationException(
                "Every contract-surface exposure contract must declare a non-blank 'name'.");
        }

        if (contract.Source is null)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' must declare a 'source' object.");
        }

        ValidateSource(document, contract, targetAssemblies, publicApiSurfaces);

        if (contract.Forbidden is null || contract.Forbidden.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' declares no 'forbidden' selectors. " +
                "Declare at least one bounded forbidden selector.");
        }

        for (int index = 0; index < contract.Forbidden.Count; index++)
        {
            ValidateSelector(
                contract.Forbidden[index],
                contract.Name,
                $"forbidden[{index}]");
        }
    }

    private static void ValidateSource(
        ArchitectureContractDocument document,
        ArchitectureContractSurfaceExposureContract contract,
        HashSet<string> targetAssemblies,
        IReadOnlyList<ArchitecturePublicApiSurfaceContract> publicApiSurfaces)
    {
        ArchitectureContractSurfaceExposureSource source = contract.Source;
        if (source.Assemblies is null || source.Assemblies.Count == 0)
        {
            ValidateOptionalSourceList(source.Projects, contract.Name, "projects");
        }
        else
        {
            ValidateSourceList(source.Assemblies, contract.Name, "assemblies");
        }

        // Validate both lists when both are populated. Empty lists are not usable constraints,
        // even when another source criterion is present: accepting one would silently turn an
        // authored conjunctive criterion into an omitted criterion.
        if (source.Assemblies is { Count: > 0 } && source.Projects is { Count: > 0 })
        {
            ValidateSourceList(source.Projects, contract.Name, "projects");
        }

        bool hasAssemblies = source.Assemblies is { Count: > 0 };
        bool hasProjects = source.Projects is { Count: > 0 };
        bool hasTypes = source.TypesMatching is not null;
        bool hasPublicApi = !string.IsNullOrWhiteSpace(source.PublicApiSurface);

        if (!hasAssemblies && !hasProjects && !hasTypes && !hasPublicApi)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' declares no usable source selector. " +
                "Declare at least one of assemblies, projects, types_matching, or public_api_surface.");
        }

        if (source.TypesMatching is not null)
        {
            ValidateSelector(source.TypesMatching, contract.Name, "source.types_matching");
            ValidateLayerReference(document, source.TypesMatching, contract.Name, "source.types_matching");
        }

        if (hasPublicApi)
        {
            ValidatePublicApiSurfaceReference(contract, source.PublicApiSurface!, publicApiSurfaces);
        }

        if (source.Assemblies is { Count: > 0 })
        {
            foreach (string assembly in source.Assemblies)
            {
                if (!targetAssemblies.Contains(assembly))
                {
                    throw new InvalidOperationException(
                        $"Contract-surface exposure contract '{contract.Name}' references source assembly '{assembly}' " +
                        "that is not declared in 'analysis.target_assemblies'.");
                }
            }
        }
    }

    private static void ValidateOptionalSourceList(
        IReadOnlyCollection<string>? values, string contractName, string fieldName)
    {
        if (values is { Count: > 0 })
        {
            ValidateSourceList(values, contractName, fieldName);
        }
    }

    private static void ValidateSourceList(
        IEnumerable<string>? values, string contractName, string fieldName)
    {
        if (values is null || !values.Any())
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares an empty '{fieldName}' list. " +
                "Every populated source list must contain at least one non-blank entry.");
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares a blank entry in '{fieldName}'. " +
                "Every entry must be non-blank.");
        }
    }

    private static void ValidateSelector(
        ArchitecturePublicApiSurfaceSelector? selector, string contractName, string fieldName)
    {
        if (selector is null || !HasUsableField(selector))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' declares an empty or unbounded '{fieldName}' " +
                $"selector. Declare at least one of {string.Join(", ", _selectorFields)}.");
        }

    }

    private static bool HasUsableField(ArchitecturePublicApiSurfaceSelector selector) =>
        !string.IsNullOrWhiteSpace(selector.NameSuffix)
        || !string.IsNullOrWhiteSpace(selector.NamePrefix)
        || !string.IsNullOrWhiteSpace(selector.Namespace)
        || !string.IsNullOrWhiteSpace(selector.Layer)
        || !string.IsNullOrWhiteSpace(selector.BaseType)
        || !string.IsNullOrWhiteSpace(selector.ImplementsInterface)
        || !string.IsNullOrWhiteSpace(selector.HasAttribute)
        || !string.IsNullOrWhiteSpace(selector.Role);

    private static void ValidateLayerReference(
        ArchitectureContractDocument document,
        ArchitecturePublicApiSurfaceSelector selector,
        string contractName,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(selector.Layer))
        {
            return;
        }

        if (!document.Layers.ContainsKey(selector.Layer))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contractName}' references unknown layer '{selector.Layer}' " +
                $"in '{fieldName}'.");
        }
    }

    private static void ValidatePublicApiSurfaceReference(
        ArchitectureContractSurfaceExposureContract contract,
        string publicApiSurfaceId,
        IReadOnlyList<ArchitecturePublicApiSurfaceContract> publicApiSurfaces)
    {
        if (string.Equals(contract.Id, publicApiSurfaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' cannot reference itself as a public API surface.");
        }

        List<ArchitecturePublicApiSurfaceContract> matches = publicApiSurfaces
            .Where(surface => string.Equals(surface.Id, publicApiSurfaceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' references unknown public API surface " +
                $"'{publicApiSurfaceId}'.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Contract-surface exposure contract '{contract.Name}' references ambiguous public API surface " +
                $"'{publicApiSurfaceId}' declared more than once.");
        }
    }
}
