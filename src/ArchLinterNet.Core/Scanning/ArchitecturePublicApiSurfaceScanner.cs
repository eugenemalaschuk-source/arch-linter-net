using System.Reflection;

namespace ArchLinterNet.Core.Scanning;
// Note: relies on System.Linq extension methods (Select/Where/Concat/Distinct/ToArray/ToHashSet)
// available via ArchLinterNet.Core's global usings.

// Signature is the legacy `declared_api` identity (kind + name + parameter/member types).
// ExactSignature adds the deterministic detail suffix that identity deliberately drops — constant
// values, accessor shape, static/ref/out/in, sealed/abstract, enum underlying type, generic
// constraints — and is what a reviewed snapshot records. Keeping both is what lets an existing
// inline allowlist and an exact snapshot coexist on the same contract. ReferencedTypes is
// assembly-qualified, not just full name: two distinct assemblies can legitimately export a type
// under the identical full name, so a first-party-escape check keyed on name alone could let a
// selected assembly's type mask an unselected same-named type from a different assembly.
internal readonly record struct ArchitectureExportedApiEntry(
    string Signature,
    string ExactSignature,
    string DeclaringTypeName,
    string AssemblyName,
    string Visibility,
    bool IsConst,
    string? ConstQualifiedName,
    IReadOnlyList<(string AssemblyName, string TypeFullName)> ReferencedTypes);

// Reflection-based enumeration of a type's exported (public/protected/protected-internal) surface,
// normalized into deterministic signature strings. Mirrors the defensive-reflection posture used by
// ArchitectureTypeScanner/ArchitectureTypeRoleMatcher elsewhere in this codebase: individual members
// that fail to reflect are skipped rather than crashing the whole scan.
internal static class ArchitecturePublicApiSurfaceScanner
{
    private const string PublicVisibility = "public";
    private const string ProtectedInternalVisibility = "protected internal";
    private const string ProtectedVisibility = "protected";

    public static IEnumerable<ArchitectureExportedApiEntry> GetExportedSurface(Assembly assembly)
    {
        foreach (ArchitectureExportedApiEntry entry in MaterializeExportedSurface(assembly).Entries)
        {
            yield return entry;
        }
    }

    // Materializes the exported type universe and its complete normalized surface in one traversal.
    // The session-scoped public API index retains both read-only collections so selectors can match
    // the exact types that produced the entries without repeating the exported-type reflection pass.
    internal static (
        IReadOnlyList<ArchitectureExportedApiEntry> Entries,
        IReadOnlyList<Type> ExportedTypes,
        bool IsComplete)
        MaterializeExportedSurface(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        List<ArchitectureExportedApiEntry> entries = new();
        List<Type> exportedTypes = new();
        ArchitectureLoadableTypeScan loadedTypes =
            ArchitectureTypeScanner.GetLoadableTypesWithCompleteness(assembly, CancellationToken.None);
        var completeness = new SurfaceScanCompleteness(loadedTypes.IsComplete);

        foreach (Type type in loadedTypes.Types)
        {
            if (!IsExportedType(type, completeness) ||
                ArchitecturePublicApiMemberScanner.IsCompilerGenerated(type, completeness))
            {
                continue;
            }

            if (!ArchitectureTypeNames.TryGetFullName(type, out string typeName))
            {
                completeness.MarkIncomplete();
                continue;
            }

            exportedTypes.Add(type);
            string typeSignature = NormalizeType(type, typeName);
            string typeVisibility = TypeVisibility(type);
            // A generic type's own declaration can reference a first-party type purely through a
            // constraint (`class Foo<T> where T : HiddenExported`), with no member involved at all.
            (string, string)[] typeReferenced = type.IsGenericTypeDefinition
                ? ArchitecturePublicApiMemberScanner.ReferencedTypes(
                    Array.Empty<Type>(), type.GetGenericArguments(), completeness)
                : Array.Empty<(string, string)>();
            entries.Add(new ArchitectureExportedApiEntry(
                typeSignature,
                ArchitecturePublicApiSignatureDetails.Compose(
                    typeSignature, ArchitecturePublicApiSignatureDetails.ForType(
                        type, typeVisibility, completeness.MarkIncomplete)),
                typeName, assemblyName, typeVisibility, false, null, typeReferenced));

            entries.AddRange(ArchitecturePublicApiMemberScanner.Scan(type, assemblyName, completeness));
        }

        return (entries.AsReadOnly(), exportedTypes.AsReadOnly(), completeness.IsComplete);
    }

    // The exported type universe GetExportedSurface enumerates, factored out so a surface_selector
    // predicate (issue #525) can be evaluated against exactly the same candidate types without
    // duplicating the visibility-chain filter.
    internal static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
        {
            if (!IsExportedType(type) || ArchitecturePublicApiMemberScanner.IsCompilerGenerated(type))
            {
                continue;
            }

            yield return type;
        }
    }

    // Full names of exported types selected by predicate, for the checker/session to determine
    // which of an assembly's exported types a surface_selector matched, without exposing Type
    // objects (or the selector matching engine) outside this scanning layer.
    public static HashSet<string> SelectedTypeFullNames(Assembly assembly, Func<Type, bool> predicate)
    {
        return GetExportedTypes(assembly)
            .Where(predicate)
            .Select(ArchitectureTypeNames.SafeFullName)
            .ToHashSet(StringComparer.Ordinal);
    }

    // A type is exported if it (and every enclosing type, for nested types) is itself public, or
    // protected/protected-internal nested inside an already-exported enclosing chain. A protected
    // nested type inside an internal outer type is unreachable from outside the assembly, so it is
    // not part of the exported surface even though the modifier itself says "protected".
    private static bool IsExportedType(Type type, SurfaceScanCompleteness? completeness = null)
    {
        Type current = type;
        while (true)
        {
            try
            {
                if (!current.IsNested)
                {
                    return current.IsPublic;
                }

                if (!(current.IsNestedPublic || current.IsNestedFamily || current.IsNestedFamORAssem))
                {
                    return false;
                }

                Type? declaring = current.DeclaringType;
                if (declaring == null)
                {
                    return false;
                }

                current = declaring;
            }
            catch (TypeLoadException)
            {
                completeness?.MarkIncomplete();
                return false;
            }
            catch (FileNotFoundException)
            {
                completeness?.MarkIncomplete();
                return false;
            }
        }
    }

    private static string TypeVisibility(Type type)
    {
        if (!type.IsNested)
        {
            return PublicVisibility;
        }

        if (type.IsNestedPublic)
        {
            return PublicVisibility;
        }

        return type.IsNestedFamORAssem ? ProtectedInternalVisibility : ProtectedVisibility;
    }

    private static string NormalizeType(Type type, string typeName)
    {
        return $"{TypeKind(type)} {typeName}";
    }

    private static string TypeKind(Type type)
    {
        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsEnum)
        {
            return "enum";
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return "delegate";
        }

        return type.IsValueType ? "struct" : "class";
    }

}
