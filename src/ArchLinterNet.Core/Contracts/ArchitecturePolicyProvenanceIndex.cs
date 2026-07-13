using System.Collections;
using System.Reflection;
using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed class ArchitecturePolicyProvenanceIndex
{
    private readonly Dictionary<string, ArchitecturePolicySourceLocation> _nodes;
    private readonly Dictionary<object, ArchitecturePolicySourceLocation> _owners =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<ContractEntry> _contracts = new();
    private readonly Dictionary<string, ArchitecturePolicySourceLocation> _layers =
        new(StringComparer.Ordinal);
    private ArchitecturePolicySourceLocation? _currentValidationLocation;

    internal ArchitecturePolicyProvenanceIndex(
        IReadOnlyList<ArchitecturePolicySourceDescriptor> sources,
        IReadOnlyDictionary<string, ArchitecturePolicySourceLocation> nodes)
    {
        Sources = sources;
        _nodes = new Dictionary<string, ArchitecturePolicySourceLocation>(nodes, StringComparer.Ordinal);
    }

    public static ArchitecturePolicyProvenanceIndex Empty { get; } =
        new(Array.Empty<ArchitecturePolicySourceDescriptor>(),
            new Dictionary<string, ArchitecturePolicySourceLocation>(StringComparer.Ordinal));

    public IReadOnlyList<ArchitecturePolicySourceDescriptor> Sources { get; }

    public IReadOnlyDictionary<string, ArchitecturePolicySourceLocation> Nodes => _nodes;

    public ArchitecturePolicySourceDescriptor? RootSource =>
        Sources.FirstOrDefault(source => source.Role == ArchitecturePolicyDocumentRole.Root);

    public bool HasImports => Sources.Any(source => source.Role == ArchitecturePolicyDocumentRole.Fragment);

    public bool TryGetLocation(string effectiveYamlPath, out ArchitecturePolicySourceLocation? location)
    {
        return _nodes.TryGetValue(effectiveYamlPath, out location);
    }

    internal void Bind(ArchitectureContractDocument document)
    {
        _owners.Clear();
        _contracts.Clear();
        _layers.Clear();

        BindOwner(document, "$", null, null);
        BindOwner(document.Analysis, "analysis", null, null);
        BindOwner(document.Classification, "classification", null, null);

        foreach ((string name, ArchitectureLayer layer) in document.Layers)
        {
            string path = $"layers.{name}";
            BindOwner(layer, path, null, null);
            if (_nodes.TryGetValue(path, out ArchitecturePolicySourceLocation? location))
            {
                _layers[name] = location;
            }
        }

        foreach ((string name, ArchitectureExternalDependencyGroup group) in document.ExternalDependencies)
        {
            BindOwner(group, $"external_dependencies.{name}", null, null);
        }

        foreach ((string name, ArchitecturePackageGroup group) in document.Packages)
        {
            BindOwner(group, $"packages.{name}", null, null);
        }

        BindContracts(document);
    }

    internal IEnumerable<T> Track<T>(IEnumerable<T> values)
        where T : class
    {
        foreach (T value in values)
        {
            _currentValidationLocation = LocationFor(value);
            yield return value;
        }
    }

    internal void ResetValidationSubject()
    {
        _currentValidationLocation = null;
    }

    internal void SetValidationSubject(object owner)
    {
        _currentValidationLocation = LocationFor(owner);
    }

    internal Exception EnrichValidationException(InvalidOperationException exception)
    {
        if (!HasImports || exception is ArchitecturePolicyValidationException
            || _currentValidationLocation is null)
        {
            return exception;
        }

        ArchitecturePolicySourceLocation location = _currentValidationLocation;
        var diagnostic = new ArchitecturePolicyDiagnostic(
            ArchitecturePolicyDiagnosticKind.SemanticValidation,
            location,
            Array.Empty<ArchitecturePolicySourceLocation>(),
            location.Source.ImportChain);
        string message = $"{exception.Message} (policy: {Format(location)}; root: {location.RootPath})";
        return new ArchitecturePolicyValidationException(message, diagnostic, exception);
    }

    internal Exception EnrichValidationException(
        InvalidOperationException exception,
        IEnumerable<object> owners)
    {
        if (!HasImports || exception is ArchitecturePolicyValidationException)
        {
            return exception;
        }

        IReadOnlyList<ArchitecturePolicySourceLocation> locations = LocationsFor(owners);
        if (locations.Count == 0)
        {
            return exception;
        }

        ArchitecturePolicySourceLocation location = locations[0];
        var diagnostic = new ArchitecturePolicyDiagnostic(
            ArchitecturePolicyDiagnosticKind.SemanticValidation,
            location,
            locations.Skip(1).ToArray(),
            location.Source.ImportChain);
        string message = $"{exception.Message} (policy: {Format(location)}; root: {location.RootPath})";
        return new ArchitecturePolicyValidationException(message, diagnostic, exception);
    }

    internal ArchitectureViolation Enrich(
        ArchitectureViolation violation,
        object? owner,
        IEnumerable<object>? relatedOwners = null)
    {
        ArchitecturePolicySourceLocation? location = owner is null ? null : LocationFor(owner);
        IReadOnlyList<ArchitecturePolicySourceLocation> related = LocationsFor(relatedOwners);
        return location is null && related.Count == 0
            ? violation
            : violation with { PolicyLocation = location, RelatedPolicyLocations = related };
    }

    internal PolicyConsistencyDiagnostic Enrich(PolicyConsistencyDiagnostic diagnostic)
    {
        List<ArchitecturePolicySourceLocation> locations = FindDiagnosticLocations(diagnostic);
        if (locations.Count == 0)
        {
            return diagnostic;
        }

        return diagnostic with
        {
            PolicyLocation = locations[0],
            RelatedPolicyLocations = locations.Skip(1).ToArray()
        };
    }

    internal ArchitectureUnmatchedIgnoredViolation Enrich(ArchitectureUnmatchedIgnoredViolation unmatched)
    {
        ContractEntry? entry = _contracts.FirstOrDefault(candidate =>
            string.Equals(candidate.Group, unmatched.ContractGroup, StringComparison.Ordinal)
            && ContractMatches(candidate.Contract, unmatched.ContractName, unmatched.ContractId));
        if (entry is null)
        {
            return unmatched;
        }

        string path = $"{entry.EffectivePath}.ignored_violations[{unmatched.IgnoreIndex}]";
        ArchitecturePolicySourceLocation? location = _nodes.GetValueOrDefault(path)
            ?? LocationFor(entry.Contract);
        return location is null ? unmatched : unmatched with { PolicyLocation = location };
    }

    internal ArchitectureViolation EnrichAtPath(ArchitectureViolation violation, string effectiveYamlPath)
    {
        return _nodes.TryGetValue(effectiveYamlPath, out ArchitecturePolicySourceLocation? location)
            ? violation with { PolicyLocation = location }
            : violation;
    }

    internal ArchitecturePolicySourceLocation? LocationFor(object owner)
    {
        return _owners.GetValueOrDefault(owner);
    }

    internal IReadOnlyList<ArchitecturePolicySourceLocation> LocationsForContracts(
        IEnumerable<IArchitectureContract> contracts)
    {
        return contracts
            .Select(LocationFor)
            .OfType<ArchitecturePolicySourceLocation>()
            .Distinct()
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.YamlPath, StringComparer.Ordinal)
            .ToArray();
    }

    internal ArchitecturePolicySourceLocation? LocationForLayer(string name)
    {
        return _layers.GetValueOrDefault(name);
    }

    private void BindContracts(ArchitectureContractDocument document)
    {
        Dictionary<object, string> families = BuildContractFamilyMap(document);
        PropertyInfo[] properties = document.Contracts.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (PropertyInfo property in properties)
        {
            string? group = property.GetCustomAttribute<YamlMemberAttribute>()?.Alias;
            if (group is null || property.GetValue(document.Contracts) is not IEnumerable values)
            {
                continue;
            }

            int index = 0;
            foreach (object? value in values)
            {
                if (value is not IArchitectureContract contract)
                {
                    index++;
                    continue;
                }

                string effectivePath = $"contracts.{group}[{index}]";
                string family = families.GetValueOrDefault(contract, group);
                BindOwner(contract, effectivePath, family, contract.Id);
                UpdateContractMetadata(effectivePath, family, contract.Id);
                _contracts.Add(new ContractEntry(group, effectivePath, contract));
                BindIgnoredViolations(contract, effectivePath, family, contract.Id);
                index++;
            }
        }
    }

    private static Dictionary<object, string> BuildContractFamilyMap(ArchitectureContractDocument document)
    {
        var result = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        foreach (ArchitectureContractFamilyBinding binding in ArchitectureContractFamilyBindings.All)
        {
            foreach (IArchitectureContract contract in binding.Strict(document.Contracts)
                         .Concat(binding.Audit(document.Contracts)))
            {
                result[contract] = binding.FamilyId;
            }
        }

        return result;
    }

    private void BindIgnoredViolations(
        IArchitectureContract contract,
        string effectivePath,
        string family,
        string? contractId)
    {
        PropertyInfo? property = contract.GetType().GetProperty("IgnoredViolations");
        if (property?.GetValue(contract) is not IEnumerable values)
        {
            return;
        }

        int index = 0;
        foreach (object? value in values)
        {
            if (value is not null)
            {
                BindOwner(value, $"{effectivePath}.ignored_violations[{index}]", family, contractId);
            }

            index++;
        }
    }

    private void BindOwner(object owner, string effectivePath, string? family, string? contractId)
    {
        if (!_nodes.TryGetValue(effectivePath, out ArchitecturePolicySourceLocation? location))
        {
            return;
        }

        _owners[owner] = location with
        {
            ContractFamily = family ?? location.ContractFamily,
            ContractId = contractId ?? location.ContractId
        };
    }

    private void UpdateContractMetadata(string prefix, string family, string? contractId)
    {
        string[] paths = _nodes.Keys
            .Where(path => path == prefix || path.StartsWith(prefix + ".", StringComparison.Ordinal))
            .ToArray();
        foreach (string path in paths)
        {
            ArchitecturePolicySourceLocation location = _nodes[path];
            _nodes[path] = location with { ContractFamily = family, ContractId = contractId };
        }
    }

    private List<ArchitecturePolicySourceLocation> FindDiagnosticLocations(
        PolicyConsistencyDiagnostic diagnostic)
    {
        var locations = new List<ArchitecturePolicySourceLocation>();
        foreach (ContractEntry entry in _contracts.Where(entry =>
                     ContractMatches(entry.Contract, diagnostic.ContractName, diagnostic.ContractId)
                     || diagnostic.ConflictingContractNames.Contains(entry.Contract.Name, StringComparer.Ordinal)
                     || entry.Contract.Id is not null
                     && diagnostic.ConflictingContractIds.Contains(
                         entry.Contract.Id,
                         StringComparer.OrdinalIgnoreCase)))
        {
            AddLocation(locations, LocationFor(entry.Contract));
        }

        if (locations.Count == 0)
        {
            foreach (string layer in diagnostic.Layers)
            {
                AddLocation(locations, LocationForLayer(layer));
            }
        }

        return locations
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.YamlPath, StringComparer.Ordinal)
            .ToList();
    }

    private static bool ContractMatches(IArchitectureContract contract, string name, string? id)
    {
        return id is not null && contract.Id is not null
            ? string.Equals(contract.Id, id, StringComparison.OrdinalIgnoreCase)
            : string.Equals(contract.Name, name, StringComparison.Ordinal);
    }

    private static void AddLocation(
        List<ArchitecturePolicySourceLocation> locations,
        ArchitecturePolicySourceLocation? location)
    {
        if (location is not null && !locations.Contains(location))
        {
            locations.Add(location);
        }
    }

    private ArchitecturePolicySourceLocation[] LocationsFor(IEnumerable<object>? owners)
    {
        if (owners is null)
        {
            return Array.Empty<ArchitecturePolicySourceLocation>();
        }

        return owners
            .Select(LocationFor)
            .OfType<ArchitecturePolicySourceLocation>()
            .Distinct()
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.YamlPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Format(ArchitecturePolicySourceLocation location)
    {
        return $"{location.SourcePath}:{location.YamlPath}";
    }

    private sealed record ContractEntry(string Group, string EffectivePath, IArchitectureContract Contract);
}
