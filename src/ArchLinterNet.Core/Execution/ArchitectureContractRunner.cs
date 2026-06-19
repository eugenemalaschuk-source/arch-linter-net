using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed class ArchitectureContractRunner(
    ArchitectureAnalysisContext context,
    ArchitectureContractDocument document,
    HashSet<string>? selectedContractIds = null)
{
    private readonly ArchitectureAnalysisContext _context = context ?? throw new ArgumentNullException(nameof(context));

    private readonly ArchitectureContractDocument _document =
        document ?? throw new ArgumentNullException(nameof(document));

    private readonly HashSet<string>? _selectedContractIds = selectedContractIds;

    private bool IsContractSelected(string? contractId)
    {
        return _selectedContractIds == null || _selectedContractIds.Count == 0
            || (contractId != null && _selectedContractIds.Contains(contractId));
    }

    public IEnumerable<ArchitectureDependencyContract> StrictContracts()
    {
        return _document.Contracts.Strict;
    }

    public IEnumerable<ArchitectureDependencyContract> AuditContracts()
    {
        return _document.Contracts.Audit;
    }

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts()
    {
        return _document.Contracts.StrictLayers;
    }

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts()
    {
        return _document.Contracts.AuditLayers;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts()
    {
        return _document.Contracts.StrictAllowOnly;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts()
    {
        return _document.Contracts.AuditAllowOnly;
    }

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts()
    {
        return _document.Contracts.StrictCycles;
    }

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts()
    {
        return _document.Contracts.AuditCycles;
    }

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts()
    {
        return _document.Contracts.StrictMethodBody;
    }

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts()
    {
        return _document.Contracts.AuditMethodBody;
    }

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts()
    {
        return _document.Contracts.StrictAsmdef;
    }

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts()
    {
        return _document.Contracts.AuditAsmdef;
    }

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts()
    {
        return _document.Contracts.StrictIndependence;
    }

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts()
    {
        return _document.Contracts.AuditIndependence;
    }

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts()
    {
        return _document.Contracts.StrictProtected;
    }

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts()
    {
        return _document.Contracts.AuditProtected;
    }

    public List<ArchitectureViolation> CheckConfiguration()
    {
        return CheckConfiguration(strict: true);
    }

    public List<ArchitectureViolation> CheckConfiguration(bool strict)
    {
        List<ArchitectureViolation> violations = new();

        foreach (string missingAssembly in _context.MissingAssemblyNames)
        {
            string probeInfo = _context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", _context.AssemblyProbingPaths)}"
                : string.Empty;

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" }));
        }

        HashSet<string> referencedLayers = new(StringComparer.Ordinal);

        void AddLayerNames(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                referencedLayers.Add(name);
            }
        }

        if (strict)
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Strict)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.StrictAllowOnly)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.StrictCycles)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.StrictMethodBody)
            {
                AddLayerNames(new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.StrictIndependence)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.StrictLayers)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.StrictProtected)
            {
                AddLayerNames(c.Protected);
                AddLayerNames(c.AllowedImporters);
            }
        }
        else
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Audit)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.AuditAllowOnly)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.AuditCycles)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.AuditMethodBody)
            {
                AddLayerNames(new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.AuditIndependence)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.AuditLayers)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.AuditProtected)
            {
                AddLayerNames(c.Protected);
                AddLayerNames(c.AllowedImporters);
            }
        }

        foreach (string layerName in referencedLayers)
        {
            ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(_document, "<configuration>", layerName);

            if (layer.External)
            {
                continue;
            }

            Type[] types = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, layer);

            if (types.Length == 0)
            {
                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    ArchitectureLayerResolver.DescribeLayer(layer),
                    "empty layer namespace",
                    new[] { $"Layer '{layerName}' namespace '{layer.Namespace}' contains no types in loaded assemblies." }));
            }
        }

        return violations;
    }

    public List<ArchitectureViolation> CheckContract(ArchitectureDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer = ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

        List<ArchitectureViolation> violations = new();

        foreach (string forbiddenLayerName in contract.Forbidden)
        {
            ArchitectureLayer forbiddenLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, forbiddenLayerName);
            violations.AddRange(FindNamespaceViolations(contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                contract.AllowedTypes, contract.IgnoredViolations));
        }

        if (contract.ForbiddenLegacyRuntime)
        {
            foreach (string forbiddenNamespace in _document.LegacyRuntimeLayers)
            {
                violations.AddRange(FindNamespaceViolations(contract.Name, contract.Id, sourceTypes,
                    new ArchitectureLayer { Namespace = forbiddenNamespace },
                    contract.AllowedTypes, contract.IgnoredViolations));
            }
        }

        return violations;
    }

    public List<ArchitectureViolation> CheckLayerContract(ArchitectureLayerContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();

        for (int sourceIndex = 0; sourceIndex < contract.Layers.Count; sourceIndex++)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Layers[sourceIndex]);
            Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

            for (int forbiddenIndex = 0; forbiddenIndex < sourceIndex; forbiddenIndex++)
            {
                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(_document, contract.Name,
                        contract.Layers[forbiddenIndex]);
                violations.AddRange(FindNamespaceViolations(contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), contract.IgnoredViolations));
            }
        }

        return violations;
    }

    public List<ArchitectureViolation> CheckAllowOnlyContract(ArchitectureAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

        var allowedLayers = contract.Allowed
            .Select(layerName => ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, layerName))
            .Append(sourceLayer)
            .ToList();

        return sourceTypes
            .Select(type => new ArchitectureViolation(
                contract.Name,
                contract.Id,
                ArchitectureTypeNames.SafeFullName(type),
                "outside allowed layers",
                ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(ArchitectureTypeNames.SafeFullName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Where(name => !contract.AllowedTypes.Contains(name))
                    .Where(name => ArchitectureLayerResolver.IsProjectType(_document, name))
                    .Where(name => !IsInAnyAllowedLayer(name, allowedLayers))
                    .Where(name => !ArchitectureIgnoreMatcher.IsIgnored(ArchitectureTypeNames.SafeFullName(type), name,
                        contract.IgnoredViolations))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray()))
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToList();
    }

    public IReadOnlyCollection<string> CheckCycleContract(ArchitectureCycleContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return Array.Empty<string>();
        }

        var contractLayers = contract.Layers.ToHashSet(StringComparer.Ordinal);
        var graph = contractLayers.ToDictionary(
            layer => layer,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, sourceLayerName);
            Type[] sourceTypes =
                ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

            foreach (Type sourceType in sourceTypes)
            {
                string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);

                foreach (Type referencedType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                {
                    string referencedTypeName = ArchitectureTypeNames.SafeFullName(referencedType);
                    string? referencedLayerName =
                        ArchitectureLayerResolver.ResolveContainingLayer(_document, referencedTypeName, contractLayers);

                    if (referencedLayerName == null || referencedLayerName == sourceLayerName)
                    {
                        continue;
                    }

                    if (ArchitectureIgnoreMatcher.IsIgnored(sourceTypeName, referencedTypeName,
                            contract.IgnoredViolations))
                    {
                        continue;
                    }

                    graph[sourceLayerName].Add(referencedLayerName);
                }
            }
        }

        return ArchitectureCycleDetector.FindCycles(graph);
    }

    public List<ArchitectureViolation> CheckMethodBodyContract(ArchitectureMethodBodyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, contract.Source);

        string[]? sourceRoots = _document.Analysis.SourceRoots.Count > 0
            ? _document.Analysis.SourceRoots.ToArray()
            : null;

        IReadOnlyList<ArchitectureViolation> roslynViolations = ArchitectureSourceScanner
            .FindMethodBodyViolations(contract.Name, contract.Id, _context.RepositoryRoot, sourceLayer.Namespace,
                contract.ForbiddenCalls, contract.IgnoredViolations, sourceRoots: sourceRoots,
                sourceLayer: sourceLayer)
            .ToList();

        IReadOnlyList<ArchitectureViolation> ilViolations = ArchitectureIlMethodBodyScanner.FindMethodBodyViolations(
            contract.Name,
            contract.Id,
            _context.TargetAssemblies,
            sourceLayer.Namespace,
            contract.ForbiddenCalls,
            contract.IgnoredViolations,
            sourceLayer: sourceLayer)
            .ToList();

        return MergeMethodBodyViolations(contract.Name, contract.Id, roslynViolations, ilViolations);
    }

    public List<ArchitectureViolation> CheckAsmdefContract(ArchitectureAsmdefContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        return ArchitectureAsmdefScanner.FindAsmdefViolations(contract.Name, contract.Id, _context.RepositoryRoot, contract)
            .ToList();
    }

    public List<ArchitectureViolation> CheckIndependenceContract(ArchitectureIndependenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();

        foreach (string sourceLayerName in contract.Layers)
        {
            ArchitectureLayer sourceLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, sourceLayerName);
            Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, sourceLayer);

            foreach (string forbiddenLayerName in contract.Layers)
            {
                if (string.Equals(sourceLayerName, forbiddenLayerName, StringComparison.Ordinal))
                {
                    continue;
                }

                ArchitectureLayer forbiddenLayer =
                    ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, forbiddenLayerName);
                violations.AddRange(FindNamespaceViolations(contract.Name, contract.Id, sourceTypes, forbiddenLayer,
                    Array.Empty<string>(), contract.IgnoredViolations));
            }
        }

        return violations;
    }

    public List<ArchitectureViolation> CheckProtectedContract(ArchitectureProtectedContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        HashSet<string> allowedTypes = new(contract.AllowedTypes, StringComparer.Ordinal);

        HashSet<string> allLayerNames = new(_document.Layers.Keys, StringComparer.Ordinal);

        foreach (string allowedImporter in contract.AllowedImporters)
        {
            ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, allowedImporter);
        }

        foreach (string protectedLayerName in contract.Protected)
        {
            ArchitectureLayer protectedLayer =
                ArchitectureLayerResolver.ResolveLayer(_document, contract.Name, protectedLayerName);

            HashSet<string> allowedImporterNames = new(contract.AllowedImporters, StringComparer.Ordinal);
            allowedImporterNames.Add(protectedLayerName);

            foreach (Assembly assembly in _context.TargetAssemblies)
            {
                foreach (Type sourceType in ArchitectureTypeScanner.GetLoadableTypes(assembly))
                {
                    string sourceTypeFullName = ArchitectureTypeNames.SafeFullName(sourceType);
                    if (string.IsNullOrEmpty(sourceTypeFullName))
                    {
                        continue;
                    }

                    string? sourceLayerName = ArchitectureLayerResolver.ResolveContainingLayer(
                        _document, sourceTypeFullName, allLayerNames);

                    if (sourceLayerName != null && allowedImporterNames.Contains(sourceLayerName))
                    {
                        continue;
                    }

                    List<string> matchingRefs = new();

                    foreach (Type refType in ArchitectureReferenceScanner.GetReferencedTypes(sourceType))
                    {
                        string refFullName = ArchitectureTypeNames.SafeFullName(refType);
                        if (string.IsNullOrEmpty(refFullName))
                        {
                            continue;
                        }

                        if (!ArchitectureLayerResolver.MatchesNamespace(
                                protectedLayer, ArchitectureTypeNames.SafeNamespace(refType)))
                        {
                            continue;
                        }

                        if (allowedTypes.Contains(sourceTypeFullName))
                        {
                            continue;
                        }

                        if (ArchitectureIgnoreMatcher.IsIgnored(
                                sourceTypeFullName, refFullName, contract.IgnoredViolations))
                        {
                            continue;
                        }

                        matchingRefs.Add(refFullName);
                    }

                    if (matchingRefs.Count == 0)
                    {
                        continue;
                    }

                    string[] normalizedRefs = matchingRefs
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray();

                    string? refLayerName = ArchitectureLayerResolver.ResolveContainingLayer(
                        _document, normalizedRefs[0], allLayerNames);

                    violations.Add(new ArchitectureViolation(
                        contract.Name, contract.Id,
                        sourceTypeFullName,
                        $"protected layer '{protectedLayerName}' (allowed importers: [{string.Join(", ", contract.AllowedImporters)}])",
                        normalizedRefs)
                    {
                        SourceLayer = sourceLayerName,
                        TargetLayer = refLayerName,
                        AllowedImporters = contract.AllowedImporters
                    });
                }
            }
        }

        return violations;
    }

    private static IEnumerable<ArchitectureViolation> FindNamespaceViolations(
        string contractName,
        string? contractId,
        Type[] sourceTypes,
        ArchitectureLayer forbiddenLayer,
        IReadOnlyCollection<string> allowedTypeFullNames,
        IReadOnlyCollection<ArchitectureIgnoredViolation> ignoredViolations)
    {
        return sourceTypes
            .Select(type => new ArchitectureViolation(
                contractName,
                contractId,
                ArchitectureTypeNames.SafeFullName(type),
                ArchitectureLayerResolver.DescribeLayer(forbiddenLayer),
                ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Where(reference => ArchitectureLayerResolver.MatchesNamespace(forbiddenLayer,
                        ArchitectureTypeNames.SafeNamespace(reference)))
                    .Select(ArchitectureTypeNames.SafeFullName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Where(name => !allowedTypeFullNames.Contains(name))
                    .Where(name =>
                        !ArchitectureIgnoreMatcher.IsIgnored(ArchitectureTypeNames.SafeFullName(type), name,
                            ignoredViolations))
                    .Distinct()
                    .OrderBy(name => name)
                    .ToArray()))
            .Where(violation => violation.ForbiddenReferences.Count > 0)
            .ToArray();
    }

    private static List<ArchitectureViolation> MergeMethodBodyViolations(
        string contractName,
        string? contractId,
        IReadOnlyList<ArchitectureViolation> roslynViolations,
        IReadOnlyList<ArchitectureViolation> ilViolations)
    {
        Dictionary<string, ArchitectureViolation> merged = new(StringComparer.Ordinal);

        foreach (ArchitectureViolation violation in roslynViolations)
        {
            foreach (string reference in violation.ForbiddenReferences)
            {
                string key = ExtractNormalizedKey(reference);
                if (merged.TryGetValue(key, out ArchitectureViolation? existing))
                {
                    List<string> combined = existing.ForbiddenReferences.Append(reference).ToList();
                    merged[key] = new ArchitectureViolation(contractName, contractId, existing.SourceType,
                        existing.ForbiddenNamespace, combined);
                }
                else
                {
                    merged[key] = new ArchitectureViolation(contractName, contractId, violation.SourceType,
                        violation.ForbiddenNamespace, new List<string> { reference });
                }
            }
        }

        foreach (ArchitectureViolation violation in ilViolations)
        {
            foreach (string reference in violation.ForbiddenReferences)
            {
                string key = ExtractNormalizedKey(reference);
                if (merged.TryGetValue(key, out ArchitectureViolation? existing))
                {
                    List<string> combined = existing.ForbiddenReferences.Append(reference).ToList();
                    merged[key] = new ArchitectureViolation(contractName, contractId, existing.SourceType,
                        existing.ForbiddenNamespace, combined);
                }
                else
                {
                    merged[key] = new ArchitectureViolation(contractName, contractId, violation.SourceType,
                        violation.ForbiddenNamespace, new List<string> { reference });
                }
            }
        }

        return merged.Values
            .OrderBy(v => v.SourceType, StringComparer.Ordinal)
            .ThenBy(v => v.ForbiddenNamespace, StringComparer.Ordinal)
            .ToList();
    }

    private static string ExtractNormalizedKey(string reference)
    {
        const string Marker = " -> ";
        int markerIndex = reference.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return reference;
        }

        string pattern = reference[..markerIndex].Trim();
        string target = reference[(markerIndex + Marker.Length)..].Trim();

        int lineEnd = target.IndexOf(' ');
        if (lineEnd > 0)
        {
            target = target[..lineEnd];
        }

        return $"{pattern} -> {target}";
    }

    private static bool IsInAnyAllowedLayer(string typeName, IReadOnlyList<ArchitectureLayer> allowedLayers)
    {
        return allowedLayers.Any(layer => ArchitectureLayerResolver.MatchesNamespace(layer, typeName));
    }
}
