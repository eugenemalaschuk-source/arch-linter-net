using System.Reflection;
using System.Runtime.CompilerServices;

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
internal static partial class ArchitecturePublicApiSurfaceScanner
{
    private const string PublicVisibility = "public";
    private const string ProtectedInternalVisibility = "protected internal";
    private const string ProtectedVisibility = "protected";

    private const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static; // NOSONAR: intentional — IL scanning needs reflection access to all members

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
            if (!IsExportedType(type, completeness) || IsCompilerGenerated(type, completeness))
            {
                continue;
            }

            exportedTypes.Add(type);
            string typeName = ArchitectureTypeNames.SafeFullName(type);
            string typeSignature = NormalizeType(type);
            string typeVisibility = TypeVisibility(type);
            // A generic type's own declaration can reference a first-party type purely through a
            // constraint (`class Foo<T> where T : HiddenExported`), with no member involved at all.
            (string, string)[] typeReferenced = type.IsGenericTypeDefinition
                ? ReferencedTypes(Array.Empty<Type>(), type.GetGenericArguments())
                : Array.Empty<(string, string)>();
            entries.Add(new ArchitectureExportedApiEntry(
                typeSignature,
                ArchitecturePublicApiSignatureDetails.Compose(
                    typeSignature, ArchitecturePublicApiSignatureDetails.ForType(
                        type, typeVisibility, completeness.MarkIncomplete)),
                typeName, assemblyName, typeVisibility, false, null, typeReferenced));

            entries.AddRange(GetExportedMembers(type, assemblyName, completeness));
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
            if (!IsExportedType(type) || IsCompilerGenerated(type))
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

    // Distinct, assembly-qualified identity of every type a member's signature references
    // (parameter/return/field/property/event-handler types, plus its own generic parameters'
    // constraints when genericParameters is supplied), walking through array/pointer/byref wrappers
    // and generic instantiations. Used to fail closed when a selected member depends on an
    // unselected first-party exported type (issue #525) — not full C#-syntax rendering, just type
    // identity. Assembly-qualified because two distinct assemblies can legitimately export a type
    // under the identical full name.
    private static (string AssemblyName, string TypeFullName)[] ReferencedTypes(
        IEnumerable<Type> types, IEnumerable<Type>? genericParameters = null)
    {
        var collected = new HashSet<Type>();
        foreach (Type type in types)
        {
            CollectReferencedTypes(type, collected);
        }

        if (genericParameters != null)
        {
            foreach (Type parameter in genericParameters)
            {
                foreach (Type constraint in SafeGetGenericParameterConstraints(parameter))
                {
                    CollectReferencedTypes(constraint, collected);
                }
            }
        }

        // Ordinal-sorted, not left in HashSet<Type> enumeration order: that order is not guaranteed
        // stable across runs, and downstream escape-violation reporting depends on encountering
        // multiple escaping types for the same member in a deterministic sequence.
        return collected
            .Select(type => (AssemblyName: SafeAssemblyName(type), TypeFullName: ArchitectureTypeNames.SafeFullName(type)))
            .Distinct()
            .OrderBy(reference => reference.AssemblyName, StringComparer.Ordinal)
            .ThenBy(reference => reference.TypeFullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static void CollectReferencedTypes(Type type, HashSet<Type> collected)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            Type? element = SafeGetElementType(type);
            if (element != null)
            {
                CollectReferencedTypes(element, collected);
            }

            return;
        }

        if (type.IsGenericParameter)
        {
            return;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            collected.Add(type.GetGenericTypeDefinition());
            foreach (Type argument in type.GetGenericArguments())
            {
                CollectReferencedTypes(argument, collected);
            }

            return;
        }

        collected.Add(type);
    }

    private static string SafeAssemblyName(Type type)
    {
        try
        {
            return type.Assembly.GetName().Name ?? string.Empty;
        }
        catch (TypeLoadException)
        {
            return string.Empty;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private static Type[] SafeGetGenericParameterConstraints(Type parameter)
    {
        try
        {
            return parameter.GetGenericParameterConstraints();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<Type>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<Type>();
        }
    }

    private static Type? SafeGetElementType(Type type)
    {
        try
        {
            return type.GetElementType();
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static ParameterInfo[] SafeGetParameters(MethodBase method)
    {
        try
        {
            return method.GetParameters();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ParameterInfo>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ParameterInfo>();
        }
    }

    private static ParameterInfo[] SafeGetIndexParameters(PropertyInfo property)
    {
        try
        {
            return property.GetIndexParameters();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ParameterInfo>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ParameterInfo>();
        }
    }

    private static bool IsAccessorMethodName(string name)
    {
        return name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal)
            || name.StartsWith("add_", StringComparison.Ordinal)
            || name.StartsWith("remove_", StringComparison.Ordinal);
    }

    private static bool IsExportedVisibility(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool IsExportedVisibility(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsExportedAccessor(MethodInfo? accessor) =>
        accessor != null && IsExportedVisibility(accessor);

    // Only "public", "protected", and "protected internal" are ever passed here — the caller has
    // already filtered to IsExportedVisibility, so these three checks are exhaustive.
    private static string MemberVisibility(MethodBase method)
    {
        if (method.IsPublic)
        {
            return PublicVisibility;
        }

        return method.IsFamilyOrAssembly ? ProtectedInternalVisibility : ProtectedVisibility;
    }

    private static string MemberVisibility(FieldInfo field)
    {
        if (field.IsPublic)
        {
            return PublicVisibility;
        }

        return field.IsFamilyOrAssembly ? ProtectedInternalVisibility : ProtectedVisibility;
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

    // A property/event is exported if at least one of its accessors is; when both accessors are
    // exported but at different visibilities (e.g. `public get; protected set;`), the more open
    // accessor's visibility describes what's actually reachable from outside the assembly.
    private static string AccessorVisibility(MethodInfo? getMethod, MethodInfo? setMethod)
    {
        string? getVisibility = getMethod != null && IsExportedVisibility(getMethod) ? MemberVisibility(getMethod) : null;
        string? setVisibility = setMethod != null && IsExportedVisibility(setMethod) ? MemberVisibility(setMethod) : null;
        return MostOpenVisibility(getVisibility, setVisibility);
    }

    private static string MostOpenVisibility(string? first, string? second)
    {
        static int Rank(string? visibility) => visibility switch
        {
            PublicVisibility => 0,
            ProtectedInternalVisibility => 1,
            ProtectedVisibility => 2,
            _ => 3
        };

        return Rank(first) <= Rank(second) ? first ?? PublicVisibility : second ?? PublicVisibility;
    }

    private static bool IsCompilerGenerated(MemberInfo member, SurfaceScanCompleteness? completeness = null)
    {
        try
        {
            return member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
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
        catch (CustomAttributeFormatException)
        {
            completeness?.MarkIncomplete();
            return false;
        }
    }

    private static string NormalizeType(Type type)
    {
        return $"{TypeKind(type)} {ArchitectureTypeNames.SafeFullName(type)}";
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
