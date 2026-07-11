using System.Linq;
using System.Reflection;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Scanning;

// Type-level and assembly-level attribute-based semantic role extraction, per
// openspec/specs/attribute-role-extraction/spec.md. Mirrors the defensive-reflection posture of
// ArchitectureAttributeUsageScanner: every CustomAttributeData read is guarded against
// TypeLoadException/FileNotFoundException/CustomAttributeFormatException so one malformed
// assembly/type never aborts the whole extraction pass.
//
// const:<Full.Type.NAME> metadata references are resolved against typeUniverse — every type visible
// to this extractor's caller — since a const field's declaring type need not be the matched
// attribute's own type or its declaring assembly.
public sealed class ArchitectureAttributeRoleExtractor
{
    private readonly ArchitectureClassificationConfiguration _configuration;
    private readonly Dictionary<Assembly, ArchitectureAttributeClassificationCandidate> _assemblyCandidateCache = new();
    private readonly Lazy<Dictionary<string, Type?>> _typesByFullName;

    public ArchitectureAttributeRoleExtractor(
        ArchitectureClassificationConfiguration configuration, IEnumerable<Type> typeUniverse)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ArgumentNullException.ThrowIfNull(typeUniverse);
        _typesByFullName = new Lazy<Dictionary<string, Type?>>(() => BuildTypeLookup(typeUniverse));
    }

    private const string TypeAttributeSourceName = "type_attribute";
    private const string AssemblyAttributeSourceName = "assembly_attribute";

    public ArchitectureTypeClassificationResult Extract(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        ArchitectureAttributeClassificationCandidate typeCandidate = _configuration.IsSourceEnabled(TypeAttributeSourceName)
            ? ResolveCandidate(
                SafeGetCustomAttributesData(type), _configuration.Attributes, ArchitectureClassificationSource.TypeAttribute,
                ArchitectureTypeNames.SafeFullName(type))
            : ArchitectureAttributeClassificationCandidate.Empty;

        ArchitectureAttributeClassificationCandidate assemblyCandidate = _configuration.IsSourceEnabled(AssemblyAttributeSourceName)
            ? ResolveAssemblyCandidate(type.Assembly)
            : ArchitectureAttributeClassificationCandidate.Empty;

        return Combine(typeCandidate, assemblyCandidate);
    }

    private ArchitectureAttributeClassificationCandidate ResolveAssemblyCandidate(Assembly assembly)
    {
        if (_assemblyCandidateCache.TryGetValue(assembly, out ArchitectureAttributeClassificationCandidate? cached))
        {
            return cached;
        }

        string subject = SafeGetAssemblyName(assembly);
        ArchitectureAttributeClassificationCandidate candidate = ResolveCandidate(
            SafeGetCustomAttributesData(assembly), _configuration.AssemblyAttributes, ArchitectureClassificationSource.AssemblyAttribute, subject);

        _assemblyCandidateCache[assembly] = candidate;
        return candidate;
    }

    private static ArchitectureTypeClassificationResult Combine(
        ArchitectureAttributeClassificationCandidate typeCandidate, ArchitectureAttributeClassificationCandidate assemblyCandidate)
    {
        List<ArchitectureClassificationConflict> conflicts = new(typeCandidate.Conflicts);
        conflicts.AddRange(assemblyCandidate.Conflicts);

        List<ArchitectureClassificationMetadataFailure> failures = new(typeCandidate.MetadataFailures);
        failures.AddRange(assemblyCandidate.MetadataFailures);

        if (typeCandidate.Role != null)
        {
            return new ArchitectureTypeClassificationResult(
                typeCandidate.Role, ArchitectureClassificationSource.TypeAttribute, typeCandidate.Metadata, conflicts, failures);
        }

        if (assemblyCandidate.Role != null)
        {
            return new ArchitectureTypeClassificationResult(
                assemblyCandidate.Role, ArchitectureClassificationSource.AssemblyAttribute, assemblyCandidate.Metadata, conflicts, failures);
        }

        return new ArchitectureTypeClassificationResult(null, null, new Dictionary<string, object>(), conflicts, failures);
    }

    private ArchitectureAttributeClassificationCandidate ResolveCandidate(
        IReadOnlyList<CustomAttributeData> attributeData,
        IReadOnlyList<ArchitectureAttributeClassificationMapping> mappings,
        ArchitectureClassificationSource source,
        string subject)
    {
        List<ArchitectureClassificationConflict> conflicts = new();
        List<ArchitectureClassificationMetadataFailure> failures = new();
        string? winningRole = null;
        IReadOnlyDictionary<string, object> winningMetadata = new Dictionary<string, object>();

        foreach (ArchitectureAttributeClassificationMapping mapping in mappings)
        {
            List<CustomAttributeData> matchedInstances = attributeData
                .Where(data => MatchesAttributeType(data, mapping.Attribute))
                .ToList();

            if (matchedInstances.Count == 0)
            {
                continue;
            }

            (string Role, IReadOnlyDictionary<string, object> Metadata) entryResult = ResolveEntryAcrossInstances(
                matchedInstances, mapping, source, subject, conflicts, failures);

            if (winningRole == null)
            {
                winningRole = entryResult.Role;
                winningMetadata = entryResult.Metadata;
            }
            else if (!RoleMetadataEqual(winningRole, winningMetadata, entryResult.Role, entryResult.Metadata))
            {
                conflicts.Add(new ArchitectureClassificationConflict(subject, source, winningRole, entryResult.Role));
            }
        }

        return new ArchitectureAttributeClassificationCandidate(winningRole, winningMetadata, conflicts, failures);
    }

    private (string Role, IReadOnlyDictionary<string, object> Metadata) ResolveEntryAcrossInstances(
        IReadOnlyList<CustomAttributeData> matchedInstances,
        ArchitectureAttributeClassificationMapping mapping,
        ArchitectureClassificationSource source,
        string subject,
        List<ArchitectureClassificationConflict> conflicts,
        List<ArchitectureClassificationMetadataFailure> failures)
    {
        IReadOnlyDictionary<string, object> firstMetadata = ExtractMetadata(matchedInstances[0], mapping, source, subject, failures);

        for (int i = 1; i < matchedInstances.Count; i++)
        {
            IReadOnlyDictionary<string, object> instanceMetadata = ExtractMetadata(matchedInstances[i], mapping, source, subject, failures);
            if (!RoleMetadataEqual(mapping.Role, firstMetadata, mapping.Role, instanceMetadata))
            {
                conflicts.Add(new ArchitectureClassificationConflict(subject, source, mapping.Role, mapping.Role));
            }
        }

        return (mapping.Role, firstMetadata);
    }

    private Dictionary<string, object> ExtractMetadata(
        CustomAttributeData attributeData,
        ArchitectureAttributeClassificationMapping mapping,
        ArchitectureClassificationSource source,
        string subject,
        List<ArchitectureClassificationMetadataFailure> failures)
    {
        Dictionary<string, object> metadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> entry in mapping.Metadata)
        {
            (object? canonical, string? failureReason) = ArchitectureAttributeMetadataExtraction.Extract(
                entry.Value, attributeData, ResolveTypeByFullName);

            if (failureReason != null)
            {
                failures.Add(new ArchitectureClassificationMetadataFailure(subject, source, entry.Key, failureReason));
                continue;
            }

            metadata[entry.Key] = canonical!;
        }

        return metadata;
    }

    // A null value marks a full name reached by two or more distinct types in the type universe
    // (e.g. the same namespace-qualified type compiled into more than one scanned assembly):
    // resolving to an arbitrary one of them would make const: extraction depend on enumeration
    // order, so an ambiguous full name resolves as "not found" rather than picking either.
    private Type? ResolveTypeByFullName(string fullName)
    {
        return _typesByFullName.Value.GetValueOrDefault(fullName);
    }

    private static Dictionary<string, Type?> BuildTypeLookup(IEnumerable<Type> typeUniverse)
    {
        Dictionary<string, Type?> lookup = new(StringComparer.Ordinal);
        foreach (Type type in typeUniverse)
        {
            string fullName = ArchitectureTypeNames.SafeFullName(type);
            if (fullName.Length == 0)
            {
                continue;
            }

            if (lookup.TryGetValue(fullName, out Type? existing))
            {
                if (existing != null && !ReferenceEquals(existing, type))
                {
                    lookup[fullName] = null;
                }
            }
            else
            {
                lookup[fullName] = type;
            }
        }

        return lookup;
    }

    private static bool RoleMetadataEqual(
        string roleA, IReadOnlyDictionary<string, object> metadataA, string roleB, IReadOnlyDictionary<string, object> metadataB)
    {
        if (!string.Equals(roleA, roleB, StringComparison.Ordinal) || metadataA.Count != metadataB.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, object> entry in metadataA)
        {
            if (!metadataB.TryGetValue(entry.Key, out object? otherValue) || !entry.Value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesAttributeType(CustomAttributeData data, string fullTypeName)
    {
        string? attributeName = TrySafeAttributeTypeName(data);
        return attributeName != null && string.Equals(attributeName, fullTypeName, StringComparison.Ordinal);
    }

    private static string? TrySafeAttributeTypeName(CustomAttributeData data)
    {
        try
        {
            return ArchitectureTypeNames.SafeFullName(data.AttributeType);
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (CustomAttributeFormatException)
        {
            return null;
        }
    }

    private static IReadOnlyList<CustomAttributeData> SafeGetCustomAttributesData(Type type)
    {
        try
        {
            return type.GetCustomAttributesData().ToList();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<CustomAttributeData>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<CustomAttributeData>();
        }
        catch (CustomAttributeFormatException)
        {
            return Array.Empty<CustomAttributeData>();
        }
    }

    private static IReadOnlyList<CustomAttributeData> SafeGetCustomAttributesData(Assembly assembly)
    {
        try
        {
            return assembly.GetCustomAttributesData().ToList();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<CustomAttributeData>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<CustomAttributeData>();
        }
        catch (CustomAttributeFormatException)
        {
            return Array.Empty<CustomAttributeData>();
        }
    }

    private static string SafeGetAssemblyName(Assembly assembly)
    {
        try
        {
            return assembly.GetName().Name ?? assembly.FullName ?? "<unknown assembly>";
        }
        catch (FileNotFoundException)
        {
            return assembly.FullName ?? "<unknown assembly>";
        }
    }
}
