using System.Globalization;
using System.Linq;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

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
    // Fixed tier order for the four sources this extractor implements — a prefix of the reviewed
    // schema's full six-source precedence (yaml_override/path are not implemented here).
    private static readonly ArchitectureClassificationSource[] _tierOrder =
    {
        ArchitectureClassificationSource.TypeAttribute,
        ArchitectureClassificationSource.AssemblyAttribute,
        ArchitectureClassificationSource.Inheritance,
        ArchitectureClassificationSource.Namespace
    };

    private readonly ArchitectureClassificationConfiguration _configuration;
    private readonly Func<ArchitectureNamespaceClassificationMapping, string, (bool Matched, string? MatchedPattern)> _matchNamespace;
    private readonly Dictionary<Assembly, ArchitectureAttributeClassificationCandidate> _assemblyCandidateCache = new();
    private readonly Lazy<Dictionary<string, Type?>> _typesByFullName;

    // Preserves the original public two-argument constructor's exact signature for binary
    // compatibility with already-compiled ArchLinterNet.Core NuGet consumers: an optional third
    // parameter on a single constructor only helps source compatibility (a default argument is
    // resolved by the caller's compiler, not embedded in the callee), so a consumer compiled
    // against the original .ctor(ArchitectureClassificationConfiguration, IEnumerable<Type>)
    // would throw MissingMethodException at load time against a DLL that replaced it with a
    // three-parameter overload. classification.namespace matching requires the namespace-glob
    // matcher only ArchitectureRoleIndex (Execution) can supply (see the internal constructor
    // below) — this constructor cannot fulfill it. Rather than silently never matching any
    // classification.namespace entry (indistinguishable from a genuinely non-matching policy), a
    // direct consumer who populates classification.namespace through this constructor is told so
    // explicitly, at construction time, so the failure surfaces immediately instead of as a
    // confusing missing role assignment later.
    public ArchitectureAttributeRoleExtractor(
        ArchitectureClassificationConfiguration configuration, IEnumerable<Type> typeUniverse)
        : this(configuration, typeUniverse, null)
    {
        if (_configuration.Namespace.Count > 0 && _configuration.IsSourceEnabled(NamespaceSourceName))
        {
            throw new InvalidOperationException(
                "classification.namespace entries are declared and the 'namespace' source is enabled, "
                + $"but {nameof(ArchitectureAttributeRoleExtractor)}(configuration, typeUniverse) has no namespace-glob "
                + "matcher to evaluate them with. Construct classification through ArchitectureAnalysisSession/"
                + "ArchitectureRoleIndex instead, which wires the namespace matcher automatically.");
        }
    }

    // Internal: only ArchitectureRoleIndex (Execution) constructs the namespace-matching delegate,
    // since Scanning must not depend on Resolution (see docs/internal/core-architecture-blueprint.md).
    internal ArchitectureAttributeRoleExtractor(
        ArchitectureClassificationConfiguration configuration,
        IEnumerable<Type> typeUniverse,
        Func<ArchitectureNamespaceClassificationMapping, string, (bool Matched, string? MatchedPattern)>? matchNamespace)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ArgumentNullException.ThrowIfNull(typeUniverse);
        _typesByFullName = new Lazy<Dictionary<string, Type?>>(() => BuildTypeLookup(typeUniverse));
        _matchNamespace = matchNamespace ?? ((_, _) => (false, null));
    }

    private const string TypeAttributeSourceName = "type_attribute";
    private const string AssemblyAttributeSourceName = "assembly_attribute";
    private const string InheritanceSourceName = "inheritance";
    private const string NamespaceSourceName = "namespace";

    public ArchitectureTypeClassificationResult Extract(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var candidates = new Dictionary<ArchitectureClassificationSource, ArchitectureAttributeClassificationCandidate>();

        if (_configuration.IsSourceEnabled(TypeAttributeSourceName))
        {
            candidates[ArchitectureClassificationSource.TypeAttribute] = ResolveCandidate(
                SafeGetCustomAttributesData(type), _configuration.Attributes, ArchitectureClassificationSource.TypeAttribute,
                ArchitectureTypeNames.SafeFullName(type));
        }

        if (_configuration.IsSourceEnabled(AssemblyAttributeSourceName))
        {
            candidates[ArchitectureClassificationSource.AssemblyAttribute] = ResolveAssemblyCandidate(type.Assembly);
        }

        if (_configuration.IsSourceEnabled(InheritanceSourceName))
        {
            candidates[ArchitectureClassificationSource.Inheritance] = ResolveInheritanceCandidate(type);
        }

        if (_configuration.IsSourceEnabled(NamespaceSourceName))
        {
            candidates[ArchitectureClassificationSource.Namespace] = ResolveNamespaceCandidate(type);
        }

        return Combine(candidates);
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

    private ArchitectureAttributeClassificationCandidate ResolveInheritanceCandidate(Type type)
    {
        List<ArchitectureClassificationConflict> conflicts = new();
        List<ArchitectureClassificationMetadataFailure> failures = new();
        string? winningRole = null;
        string? winningEvidence = null;
        IReadOnlyDictionary<string, object> winningMetadata = new Dictionary<string, object>();
        string subject = ArchitectureTypeNames.SafeFullName(type);

        foreach (ArchitectureInheritanceClassificationMapping mapping in _configuration.Inheritance)
        {
            if (!MatchesBaseType(mapping.BaseType, type))
            {
                continue;
            }

            IReadOnlyDictionary<string, object> metadata = ExtractMetadataWithoutAttributeInstance(
                mapping.Metadata, ArchitectureClassificationSource.Inheritance, subject, failures);

            if (winningRole == null)
            {
                winningRole = mapping.Role;
                winningMetadata = metadata;
                winningEvidence = mapping.BaseType;
            }
            else if (!RoleMetadataEqual(winningRole, winningMetadata, mapping.Role, metadata))
            {
                conflicts.Add(new ArchitectureClassificationConflict(
                    subject, ArchitectureClassificationSource.Inheritance, winningRole, mapping.Role,
                    DescribeMetadataDiff(winningMetadata, metadata)));
            }
        }

        return new ArchitectureAttributeClassificationCandidate(winningRole, winningMetadata, winningEvidence, conflicts, failures);
    }

    // Matches base_type by full-name comparison against type's own actual base-class chain and
    // transitive interface set (Type.GetInterfaces() already returns every interface implemented,
    // including inherited ones), NOT by resolving base_type through the typeUniverse-based lookup
    // used for const: resolution. A framework/package base type (e.g. ControllerBase, DbContext)
    // typically never appears in typeUniverse (which is built only from the scanned target
    // assemblies), so resolving it there would silently fail to match every framework-derived type
    // in the common case this feature exists for. Walking the type's own reflected chain works
    // regardless of which assembly declares the base type or interface, as long as it's loadable.
    private static bool MatchesBaseType(string baseTypeFullName, Type type)
    {
        try
        {
            for (Type? current = type.BaseType; current != null; current = current.BaseType)
            {
                if (string.Equals(NormalizedFullName(current), baseTypeFullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return type.GetInterfaces().Any(iface => string.Equals(NormalizedFullName(iface), baseTypeFullName, StringComparison.Ordinal));
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    // A closed constructed generic ancestor/interface (e.g. IRepository<Order> implemented via
    // `class OrderRepository : IRepository<Order>`) has a FullName that embeds the assembly-qualified
    // closed type argument (e.g. "MyApp.IRepository`1[[MyApp.Order, MyApp, ...]]"), which never equals
    // the open generic definition's FullName ("MyApp.IRepository`1") a base_type mapping declares.
    // Normalizing to the generic type definition before comparing lets one mapping match every closed
    // instantiation, mirroring ArchitectureRoleIndex.TryGetRole's existing open-generic-definition
    // fallback for the classified type itself.
    private static string NormalizedFullName(Type type)
    {
        return ArchitectureTypeNames.SafeFullName(type.IsGenericType ? type.GetGenericTypeDefinition() : type);
    }

    private ArchitectureAttributeClassificationCandidate ResolveNamespaceCandidate(Type type)
    {
        List<ArchitectureClassificationConflict> conflicts = new();
        List<ArchitectureClassificationMetadataFailure> failures = new();
        string? winningRole = null;
        string? winningEvidence = null;
        IReadOnlyDictionary<string, object> winningMetadata = new Dictionary<string, object>();
        string subject = ArchitectureTypeNames.SafeFullName(type);
        string candidateNamespace = ArchitectureTypeNames.SafeNamespace(type);

        foreach (ArchitectureNamespaceClassificationMapping mapping in _configuration.Namespace)
        {
            (bool matched, string? matchedPattern) = _matchNamespace(mapping, candidateNamespace);
            if (!matched)
            {
                continue;
            }

            IReadOnlyDictionary<string, object> metadata = ExtractMetadataWithoutAttributeInstance(
                mapping.Metadata, ArchitectureClassificationSource.Namespace, subject, failures);

            if (winningRole == null)
            {
                winningRole = mapping.Role;
                winningMetadata = metadata;
                winningEvidence = matchedPattern;
            }
            else if (!RoleMetadataEqual(winningRole, winningMetadata, mapping.Role, metadata))
            {
                conflicts.Add(new ArchitectureClassificationConflict(
                    subject, ArchitectureClassificationSource.Namespace, winningRole, mapping.Role,
                    DescribeMetadataDiff(winningMetadata, metadata)));
            }
        }

        return new ArchitectureAttributeClassificationCandidate(winningRole, winningMetadata, winningEvidence, conflicts, failures);
    }

    private Dictionary<string, object> ExtractMetadataWithoutAttributeInstance(
        Dictionary<string, object> metadataMappings,
        ArchitectureClassificationSource source,
        string subject,
        List<ArchitectureClassificationMetadataFailure> failures)
    {
        Dictionary<string, object> metadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> entry in metadataMappings)
        {
            (object? canonical, string? failureReason) = ArchitectureAttributeMetadataExtraction.ExtractWithoutAttributeInstance(
                entry.Value, ResolveTypeByFullName);

            if (failureReason != null)
            {
                failures.Add(new ArchitectureClassificationMetadataFailure(subject, source, entry.Key, failureReason));
                continue;
            }

            metadata[entry.Key] = canonical!;
        }

        return metadata;
    }

    // Walks the fixed tier order, first tier with a non-null Role wins. Conflicts/metadata failures
    // from every enabled tier are unioned into the result regardless of which tier's role wins —
    // cross-tier precedence itself is never recorded as a conflict fact (only same-tier disagreement
    // within one source's mapping list is), matching the spec's existing distinction.
    private static ArchitectureTypeClassificationResult Combine(
        Dictionary<ArchitectureClassificationSource, ArchitectureAttributeClassificationCandidate> candidates)
    {
        List<ArchitectureClassificationConflict> conflicts = new();
        List<ArchitectureClassificationMetadataFailure> failures = new();

        foreach (ArchitectureAttributeClassificationCandidate candidate in candidates.Values)
        {
            conflicts.AddRange(candidate.Conflicts);
            failures.AddRange(candidate.MetadataFailures);
        }

        foreach (ArchitectureClassificationSource source in _tierOrder)
        {
            if (candidates.TryGetValue(source, out ArchitectureAttributeClassificationCandidate? candidate)
                && candidate.Role != null)
            {
                return new ArchitectureTypeClassificationResult(
                    candidate.Role, source, candidate.Metadata, candidate.Evidence, conflicts, failures);
            }
        }

        return new ArchitectureTypeClassificationResult(null, null, new Dictionary<string, object>(), null, conflicts, failures);
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
        string? winningEvidence = null;
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
                winningEvidence = mapping.Attribute;
            }
            else if (!RoleMetadataEqual(winningRole, winningMetadata, entryResult.Role, entryResult.Metadata))
            {
                conflicts.Add(new ArchitectureClassificationConflict(
                    subject, source, winningRole, entryResult.Role, DescribeMetadataDiff(winningMetadata, entryResult.Metadata)));
            }
        }

        return new ArchitectureAttributeClassificationCandidate(winningRole, winningMetadata, winningEvidence, conflicts, failures);
    }

    private (string Role, IReadOnlyDictionary<string, object> Metadata) ResolveEntryAcrossInstances(
        List<CustomAttributeData> matchedInstances,
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
                conflicts.Add(new ArchitectureClassificationConflict(
                    subject, source, mapping.Role, mapping.Role, DescribeMetadataDiff(firstMetadata, instanceMetadata)));
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

    // Sorted so the description (and therefore the conflict record's equality/hash, used for
    // dedup) is deterministic regardless of dictionary enumeration order.
    private static string? DescribeMetadataDiff(
        IReadOnlyDictionary<string, object> winning, IReadOnlyDictionary<string, object> discarded)
    {
        SortedSet<string> keys = new(StringComparer.Ordinal);
        keys.UnionWith(winning.Keys);
        keys.UnionWith(discarded.Keys);

        List<string> differences = new();
        foreach (string key in keys)
        {
            bool hasWinning = winning.TryGetValue(key, out object? winningValue);
            bool hasDiscarded = discarded.TryGetValue(key, out object? discardedValue);
            if (hasWinning && hasDiscarded && winningValue!.Equals(discardedValue))
            {
                continue;
            }

            string winningText = hasWinning ? FormatMetadataValue(winningValue!) : "<absent>";
            string discardedText = hasDiscarded ? FormatMetadataValue(discardedValue!) : "<absent>";
            differences.Add($"{key}: {winningText} vs {discardedText}");
        }

        return differences.Count == 0 ? null : string.Join("; ", differences);
    }

    private static string FormatMetadataValue(object value) => value switch
    {
        string s => $"'{s}'",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>"
    };

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
