using System.Collections;
using System.Reflection;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
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

        BindOwner(document, ArchitecturePolicyProvenancePath.Root, null, null);
        BindOwner(document.Analysis, ArchitecturePolicyProvenancePath.Property("analysis"), null, null);
        BindOwner(document.Classification, ArchitecturePolicyProvenancePath.Property("classification"), null, null);

        foreach ((string name, ArchitectureLayer layer) in document.Layers)
        {
            string path = ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("layers"), name);
            BindOwner(layer, path, null, null);
            if (_nodes.TryGetValue(path, out ArchitecturePolicySourceLocation? location))
            {
                _layers[name] = location;
            }
        }

        foreach ((string name, ArchitectureExternalDependencyGroup group) in document.ExternalDependencies)
        {
            BindOwner(group, ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("external_dependencies"), name), null, null);
        }

        foreach ((string name, ArchitecturePackageGroup group) in document.Packages)
        {
            BindOwner(group, ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("packages"), name), null, null);
        }

        foreach ((string name, ArchitectureFrameworkReferenceGroup group) in document.FrameworkReferences)
        {
            BindOwner(group, ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("framework_references"), name), null, null);
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

    internal void SetValidationSubject(string effectivePath)
    {
        _currentValidationLocation = _nodes.GetValueOrDefault(effectivePath);
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

        ArchitecturePolicySourceLocation[] locations = LocationsFor(owners);
        if (locations.Length == 0)
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
        ArchitecturePolicySourceLocation[] related = LocationsFor(relatedOwners);
        return location is null && related.Length == 0
            ? violation
            : violation with { PolicyLocation = location, RelatedPolicyLocations = related };
    }

    internal ArchitectureCycleFinding Enrich(
        ArchitectureCycleFinding cycle,
        object? owner,
        IEnumerable<object>? relatedOwners = null)
    {
        ArchitecturePolicySourceLocation? location = owner is null ? null : LocationFor(owner);
        ArchitecturePolicySourceLocation[] related = LocationsFor(relatedOwners);
        return location is null && related.Length == 0
            ? cycle
            : cycle with { PolicyLocation = location, RelatedPolicyLocations = related };
    }

    internal PolicyConsistencyDiagnostic Enrich(PolicyConsistencyDiagnostic diagnostic)
    {
        // A check that already resolved its own precise element location (e.g.
        // unmatched-layer-exclusion attaching the exact layers.<name>.exclude[<index>] location)
        // takes precedence over this generic contract/layer-level fallback - otherwise the
        // fallback below would clobber it with the coarser owning-layer location.
        if (diagnostic.PolicyLocation is not null)
        {
            return diagnostic;
        }

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

    internal ArchitectureClassificationConflict Enrich(ArchitectureClassificationConflict conflict)
    {
        ArchitecturePolicySourceLocation? location = LocationForPath(conflict.PolicyPath);
        ArchitecturePolicySourceLocation[] related = LocationsForPaths(conflict.RelatedPolicyPaths);
        return location is null && related.Length == 0
            ? conflict
            : conflict with { PolicyLocation = location, RelatedPolicyLocations = related };
    }

    internal ArchitectureClassificationMetadataFailure Enrich(ArchitectureClassificationMetadataFailure failure)
    {
        ArchitecturePolicySourceLocation? location = LocationForPath(failure.PolicyPath);
        return location is null ? failure : failure with { PolicyLocation = location };
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

        string path = ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(entry.EffectivePath, "ignored_violations"),
            unmatched.IgnoreIndex);
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
            .ThenBy(location => location.EncounterOrdinal)
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

                string effectivePath = ArchitecturePolicyProvenancePath.AppendIndex(
                    ArchitecturePolicyProvenancePath.AppendProperty(
                        ArchitecturePolicyProvenancePath.Property("contracts"), group),
                    index);
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
                BindOwner(value, ArchitecturePolicyProvenancePath.AppendIndex(
                    ArchitecturePolicyProvenancePath.AppendProperty(effectivePath, "ignored_violations"),
                    index), family, contractId);
            }

            index++;
        }
    }

    internal void BindCatalogContract(string group, string family, IArchitectureContract contract)
    {
        if (family != "layer_template" || contract is not ArchitectureLayerContract { TemplateOwnerId: { } templateId })
        {
            return;
        }

        ContractEntry? template = _contracts.FirstOrDefault(entry =>
            entry.Group == group
            && entry.Contract is ArchitectureLayerTemplateContract
            && string.Equals(entry.Contract.Id, templateId, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            return;
        }

        BindOwner(contract, template.EffectivePath, family, template.Contract.Id);
        _contracts.Add(new ContractEntry(group, template.EffectivePath, contract));
    }

    internal Exception EnrichLayerTemplateExpansionException(
        ArgumentException exception,
        ArchitectureLayerTemplateContract template)
    {
        ArchitecturePolicySourceLocation? location = LocationFor(template);
        if (!HasImports || location is null)
        {
            return exception;
        }

        var diagnostic = new ArchitecturePolicyDiagnostic(
            ArchitecturePolicyDiagnosticKind.SemanticValidation,
            location,
            Array.Empty<ArchitecturePolicySourceLocation>(),
            location.Source.ImportChain);
        return new ArchitecturePolicyValidationException(
            $"{exception.Message} (policy: {Format(location)}; root: {location.RootPath})",
            diagnostic,
            exception);
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
            .Where(path => ArchitecturePolicyProvenancePath.IsSameOrDescendant(path, prefix))
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
        bool hasParticipantIds = diagnostic.ContractId is not null
            || diagnostic.ConflictingContractIds.Count > 0;
        foreach (ContractEntry entry in _contracts.Where(entry =>
                     IsDiagnosticParticipant(entry.Contract, diagnostic, hasParticipantIds)))
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
            .ThenBy(location => location.EncounterOrdinal)
            .ToList();
    }

    private static bool IsDiagnosticParticipant(
        IArchitectureContract contract,
        PolicyConsistencyDiagnostic diagnostic,
        bool hasParticipantIds)
    {
        if (hasParticipantIds)
        {
            return contract.Id is not null
                && (string.Equals(contract.Id, diagnostic.ContractId, StringComparison.OrdinalIgnoreCase)
                    || diagnostic.ConflictingContractIds.Contains(contract.Id, StringComparer.OrdinalIgnoreCase));
        }

        return string.Equals(contract.Name, diagnostic.ContractName, StringComparison.Ordinal)
            || diagnostic.ConflictingContractNames.Contains(contract.Name, StringComparer.Ordinal);
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
            .ThenBy(location => location.EncounterOrdinal)
            .ToArray();
    }

    private ArchitecturePolicySourceLocation? LocationForPath(string? effectivePath)
    {
        return effectivePath is null ? null : _nodes.GetValueOrDefault(effectivePath);
    }

    private ArchitecturePolicySourceLocation[] LocationsForPaths(IEnumerable<string>? effectivePaths)
    {
        if (effectivePaths is null)
        {
            return Array.Empty<ArchitecturePolicySourceLocation>();
        }

        return effectivePaths
            .Select(LocationForPath)
            .OfType<ArchitecturePolicySourceLocation>()
            .Distinct()
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.EncounterOrdinal)
            .ToArray();
    }

    private static string Format(ArchitecturePolicySourceLocation location)
    {
        return $"{location.SourcePath}:{location.YamlPath}";
    }

    private sealed record ContractEntry(string Group, string EffectivePath, IArchitectureContract Contract);
}
